using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.ReportServer.Api.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Tempo.ReportServer.Api.Rendering;

/// <summary>Tenant-fair render job queue.</summary>
public interface IReportRenderJobQueue
{
    /// <summary>Queues a render job.</summary>
    Task<RenderJobDto> EnqueueAsync(RenderReportRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Gets a job.</summary>
    Task<RenderJobDto?> GetAsync(string tenantId, string jobId, CancellationToken cancellationToken = default);

    /// <summary>Processes the next queued job using round-robin tenant fairness.</summary>
    Task<RenderJobDto?> ProcessNextAsync(CancellationToken cancellationToken = default);
}

/// <summary>In-memory tenant-fair render job queue.</summary>
public sealed class InMemoryReportRenderJobQueue : IReportRenderJobQueue
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<string>> _tenantQueues = new(StringComparer.Ordinal);
    private readonly Queue<string> _tenantOrder = new();
    private readonly Dictionary<(string TenantId, string JobId), JobState> _jobs = new();

    /// <summary>Creates a queue.</summary>
    public InMemoryReportRenderJobQueue(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    public Task<RenderJobDto> EnqueueAsync(RenderReportRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var state = new JobState
        {
            TenantId = request.TenantId,
            JobId = $"job_{Guid.NewGuid():N}",
            ReportId = request.ReportId,
            Format = request.Format,
            Status = RenderJobStatus.Queued,
            QueuedAt = DateTimeOffset.UtcNow,
            Request = request,
        };
        lock (_gate)
        {
            _jobs[(state.TenantId, state.JobId)] = state;
            if (!_tenantQueues.TryGetValue(state.TenantId, out var queue))
            {
                queue = new Queue<string>();
                _tenantQueues[state.TenantId] = queue;
                _tenantOrder.Enqueue(state.TenantId);
            }

            queue.Enqueue(state.JobId);
        }

        return Task.FromResult(ToDto(state));
    }

    /// <inheritdoc />
    public Task<RenderJobDto?> GetAsync(string tenantId, string jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_jobs.TryGetValue((tenantId, jobId), out var state) ? ToDto(state) : null);
        }
    }

    /// <inheritdoc />
    public async Task<RenderJobDto?> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        JobState? state;
        lock (_gate)
        {
            state = DequeueNext();
            if (state is not null)
            {
                state.Status = RenderJobStatus.Running;
                state.StartedAt = DateTimeOffset.UtcNow;
            }
        }

        if (state is null)
        {
            return null;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var requestContext = scope.ServiceProvider.GetRequiredService<ReportServerRequestContext>();
            var store = scope.ServiceProvider.GetRequiredService<IReportServerStore>();
            var renderer = scope.ServiceProvider.GetRequiredService<IReportServerRenderer>();
            requestContext.Set(new ReportExecutionContext(state.TenantId, "render-worker", state.Request.CultureName));
            var report = await store.GetReportAsync(state.TenantId, state.ReportId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Report '{state.ReportId}' was not found.");
            var result = await renderer.RenderAsync(report, state.Request, requestContext.ExecutionContext, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                state.Status = RenderJobStatus.Completed;
                state.CompletedAt = DateTimeOffset.UtcNow;
                state.DownloadUrl = $"api/render/jobs/{Uri.EscapeDataString(state.JobId)}/result";
                state.SnapshotUrl = result.Format == ReportRenderFormat.Snapshot ? state.DownloadUrl : null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (_gate)
            {
                state.Status = RenderJobStatus.Failed;
                state.CompletedAt = DateTimeOffset.UtcNow;
                state.ErrorMessage = ex.Message;
            }
        }

        return ToDto(state);
    }

    private JobState? DequeueNext()
    {
        var inspected = 0;
        while (_tenantOrder.Count > 0 && inspected <= _tenantQueues.Count)
        {
            inspected++;
            var tenantId = _tenantOrder.Dequeue();
            if (!_tenantQueues.TryGetValue(tenantId, out var queue) || queue.Count == 0)
            {
                _tenantQueues.Remove(tenantId);
                continue;
            }

            var jobId = queue.Dequeue();
            if (queue.Count > 0)
            {
                _tenantOrder.Enqueue(tenantId);
            }
            else
            {
                _tenantQueues.Remove(tenantId);
            }

            return _jobs[(tenantId, jobId)];
        }

        return null;
    }

    private static RenderJobDto ToDto(JobState state)
        => new()
        {
            TenantId = state.TenantId,
            JobId = state.JobId,
            ReportId = state.ReportId,
            Format = state.Format,
            Status = state.Status,
            QueuedAt = state.QueuedAt,
            StartedAt = state.StartedAt,
            CompletedAt = state.CompletedAt,
            ErrorMessage = state.ErrorMessage,
            SnapshotUrl = state.SnapshotUrl,
            DownloadUrl = state.DownloadUrl,
        };

    private sealed class JobState
    {
        public string TenantId { get; init; } = string.Empty;

        public string JobId { get; init; } = string.Empty;

        public string ReportId { get; init; } = string.Empty;

        public ReportRenderFormat Format { get; init; }

        public RenderJobStatus Status { get; set; }

        public DateTimeOffset QueuedAt { get; init; }

        public DateTimeOffset? StartedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public string? ErrorMessage { get; set; }

        public string? SnapshotUrl { get; set; }

        public string? DownloadUrl { get; set; }

        public RenderReportRequestDto Request { get; init; } = new();
    }
}
