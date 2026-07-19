using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.ReportServer.Api.Storage;

namespace Tempo.ReportServer.Api.Rendering;

/// <summary>Tuning options for the SQL-Server-backed distributed render job queue and its worker.</summary>
public sealed record ReportRenderJobQueueOptions
{
    /// <summary>How often the distributed render worker polls for queued jobs.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a node's processing lease on a claimed render job is held. Must comfortably exceed the
    /// worst-case render time so a slow (but live) node's lease does not expire mid-render and let a
    /// second node re-claim and double-render. Defaults to five minutes.
    /// </summary>
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Maximum number of jobs drained in a single worker poll before yielding to the next tick.</summary>
    public int MaxJobsPerPass { get; init; } = 50;

    /// <summary>Whether the hosted distributed render worker is enabled (bound from <c>Rendering:RenderWorker:Enabled</c>).</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>
/// Stable identity of a single running render node, used as the lease owner when claiming a queued job.
/// Registered as a singleton so every scoped queue in the process shares one owner id.
/// </summary>
public sealed class ReportRenderNodeIdentity
{
    /// <summary>The lease-owner identifier for this render node.</summary>
    public string NodeId { get; init; } = $"{Environment.MachineName}:{Guid.NewGuid():N}";
}

/// <summary>
/// SQL-Server-backed distributed render job queue. Unlike <see cref="InMemoryReportRenderJobQueue"/>
/// (which holds a per-process channel and cannot coordinate more than one render node), this persists
/// every job as a <see cref="RenderJobEntity"/> row so any number of nodes coordinate a single fair
/// queue through the database. <see cref="ProcessNextAsync"/> atomically claims the oldest claimable
/// job with a single conditional UPDATE (exactly-one winner across nodes), renders through the same
/// path the in-memory queue uses, then persists the terminal outcome and releases the lease. A crashed
/// node's lease expires (<see cref="ReportRenderJobQueueOptions.LeaseDuration"/>) and the job becomes
/// re-claimable.
/// </summary>
public sealed class EfReportRenderJobQueue : IReportRenderJobQueue
{
    private const string QueuedStatus = "Queued";
    private const string RunningStatus = "Running";
    private const string CompletedStatus = "Completed";
    private const string FailedStatus = "Failed";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ReportServerDbContext _dbContext;
    private readonly ReportServerRequestContext _requestContext;
    private readonly IReportServerStore _store;
    private readonly IReportServerRenderer _renderer;
    private readonly ReportRenderJobQueueOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly string _leaseOwner;

    /// <summary>Creates the distributed queue.</summary>
    public EfReportRenderJobQueue(
        ReportServerDbContext dbContext,
        ReportServerRequestContext requestContext,
        IReportServerStore store,
        IReportServerRenderer renderer,
        ReportRenderJobQueueOptions options,
        TimeProvider timeProvider,
        ReportRenderNodeIdentity? nodeIdentity = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _leaseOwner = (nodeIdentity ?? new ReportRenderNodeIdentity()).NodeId;
    }

    /// <inheritdoc />
    public async Task<RenderJobDto> EnqueueAsync(RenderReportRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var queuedAt = _timeProvider.GetUtcNow();
        // Per-tenant queue position for round-robin fairness: one past the tenant's current highest
        // pending (Queued/Running) position, so it resets to 1 once the tenant's queue drains and never
        // grows unbounded across history. A same-tenant enqueue race may reuse a value; QueuedSequence
        // breaks the tie deterministically, so only fairness ordering (not correctness) is affected.
        var maxTenantSequence = await _dbContext.RenderJobs
            .Where(job => job.TenantId == request.TenantId
                && (job.Status == QueuedStatus || job.Status == RunningStatus))
            .Select(job => (long?)job.TenantSequence)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0L;
        var entity = new RenderJobEntity
        {
            JobId = $"job_{Guid.NewGuid():N}",
            TenantId = request.TenantId,
            ReportId = request.ReportId,
            Format = request.Format.ToString(),
            Status = QueuedStatus,
            RequestJson = JsonSerializer.Serialize(request, JsonOptions),
            QueuedAt = queuedAt,
            QueuedSequence = queuedAt.UtcTicks,
            TenantSequence = maxTenantSequence + 1,
        };
        _dbContext.RenderJobs.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<RenderJobDto?> GetAsync(string tenantId, string jobId, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.RenderJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(job => job.TenantId == tenantId && job.JobId == jobId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToDto(row);
    }

    /// <inheritdoc />
    public async Task<RenderJobDto?> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var claimed = await ClaimNextAsync(cancellationToken).ConfigureAwait(false);
        if (claimed is null)
        {
            return null;
        }

        int terminalAffected;
        try
        {
            var request = JsonSerializer.Deserialize<RenderReportRequestDto>(claimed.RequestJson, JsonOptions)
                ?? throw new InvalidOperationException($"Render job '{claimed.JobId}' has an unreadable request payload.");
            // Reuse the exact render path the in-memory queue uses: set the ambient tenant, resolve the
            // published report through the tenant-scoped store, and render with the shared renderer.
            _requestContext.Set(new ReportExecutionContext(claimed.TenantId, "render-worker", request.CultureName));
            var report = await _store.GetReportAsync(claimed.TenantId, claimed.ReportId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Report '{claimed.ReportId}' was not found.");
            var result = await _renderer.RenderAsync(report, request, _requestContext.ExecutionContext, cancellationToken).ConfigureAwait(false);

            var completedAt = _timeProvider.GetUtcNow();
            var downloadUrl = $"api/render/jobs/{Uri.EscapeDataString(claimed.JobId)}/result";
            var snapshotUrl = result.Format == ReportRenderFormat.Snapshot ? downloadUrl : null;
            // The terminal write is guarded on LeaseOwner: if this node's lease expired mid-render and
            // another node re-claimed the job, our UPDATE matches 0 rows and we must NOT stomp the new
            // owner's in-progress row with a stale "Completed". Releasing the lease in the same statement
            // atomically frees the row.
            terminalAffected = await _dbContext.RenderJobs
                .Where(job => job.JobId == claimed.JobId && job.LeaseOwner == _leaseOwner)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(job => job.Status, CompletedStatus)
                        .SetProperty(job => job.CompletedAt, completedAt)
                        .SetProperty(job => job.DownloadUrl, downloadUrl)
                        .SetProperty(job => job.SnapshotUrl, snapshotUrl)
                        .SetProperty(job => job.ErrorMessage, (string?)null)
                        .SetProperty(job => job.LeaseOwner, (string?)null)
                        .SetProperty(job => job.LeasedUntilTicks, (long?)null),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var completedAt = _timeProvider.GetUtcNow();
            var message = ex.Message.Length > 1024 ? ex.Message[..1024] : ex.Message;
            terminalAffected = await _dbContext.RenderJobs
                .Where(job => job.JobId == claimed.JobId && job.LeaseOwner == _leaseOwner)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(job => job.Status, FailedStatus)
                        .SetProperty(job => job.CompletedAt, completedAt)
                        .SetProperty(job => job.ErrorMessage, message)
                        .SetProperty(job => job.LeaseOwner, (string?)null)
                        .SetProperty(job => job.LeasedUntilTicks, (long?)null),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (terminalAffected == 0)
        {
            // We lost the lease mid-render (a render that outran LeaseDuration); another node now owns this
            // job. Do not claim success on a row we no longer own — return the row as it currently stands.
            var current = await _dbContext.RenderJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(job => job.JobId == claimed.JobId, cancellationToken)
                .ConfigureAwait(false);
            return current is null ? null : ToDto(current);
        }

        var final = await _dbContext.RenderJobs
            .AsNoTracking()
            .FirstAsync(job => job.JobId == claimed.JobId, cancellationToken)
            .ConfigureAwait(false);
        return ToDto(final);
    }

    /// <summary>
    /// Atomically claims the next claimable job. A claimable job is in a non-terminal state
    /// (<c>Queued</c>, or <c>Running</c> whose lease has expired because its node crashed) and is
    /// currently unleased or lease-expired. The single conditional UPDATE re-checks that predicate, so
    /// with two nodes racing the same row SQL Server serialises the UPDATEs on the row lock and the loser
    /// updates 0 rows — exactly one node wins. Ordering is <c>(TenantSequence, QueuedSequence)</c>: tenants
    /// interleave round-robin (every tenant's job #1 before any tenant's job #2) with the global enqueue
    /// tick as the oldest-first tie-break, so one tenant's backlog cannot starve the others. On a lost
    /// race the next candidate is tried.
    /// </summary>
    /// <remarks>
    /// Terminal (Completed/Failed) rows are retained, not reaped — the job history doubles as the
    /// result-lookup source for <c>GET /api/render/jobs/{id}</c>. A future retention sweep should prune
    /// old terminal rows; until then the RenderJobs table grows with completed jobs (see plan backlog).
    /// </remarks>
    private async Task<RenderJobEntity?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var nowTicks = now.UtcTicks;
        var leaseUntilTicks = (now + _options.LeaseDuration).UtcTicks;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidateId = await _dbContext.RenderJobs
                .AsNoTracking()
                .Where(job => (job.Status == QueuedStatus || job.Status == RunningStatus)
                    && (job.LeasedUntilTicks == null || job.LeasedUntilTicks <= nowTicks))
                .OrderBy(job => job.TenantSequence)
                .ThenBy(job => job.QueuedSequence)
                .ThenBy(job => job.JobId)
                .Select(job => job.JobId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (candidateId is null)
            {
                return null;
            }

            var affected = await _dbContext.RenderJobs
                .Where(job => job.JobId == candidateId
                    && (job.Status == QueuedStatus || job.Status == RunningStatus)
                    && (job.LeasedUntilTicks == null || job.LeasedUntilTicks <= nowTicks))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(job => job.Status, RunningStatus)
                        .SetProperty(job => job.LeaseOwner, _leaseOwner)
                        .SetProperty(job => job.LeasedUntilTicks, leaseUntilTicks)
                        .SetProperty(job => job.StartedAt, now),
                    cancellationToken)
                .ConfigureAwait(false);
            if (affected != 1)
            {
                // Another node won this row (or it advanced past the claimable predicate). Try the next.
                continue;
            }

            return await _dbContext.RenderJobs
                .AsNoTracking()
                .FirstAsync(job => job.JobId == candidateId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static RenderJobDto ToDto(RenderJobEntity entity)
        => new()
        {
            TenantId = entity.TenantId,
            JobId = entity.JobId,
            ReportId = entity.ReportId,
            Format = ParseEnum(entity.Format, ReportRenderFormat.Snapshot),
            Status = ParseEnum(entity.Status, RenderJobStatus.Queued),
            QueuedAt = entity.QueuedAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            ErrorMessage = entity.ErrorMessage,
            SnapshotUrl = entity.SnapshotUrl,
            DownloadUrl = entity.DownloadUrl,
        };

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
