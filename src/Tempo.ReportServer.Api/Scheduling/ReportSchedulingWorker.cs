using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Api.Scheduling;

/// <summary>Tuning options for the scheduling worker and retry policy.</summary>
public sealed record ReportSchedulingOptions
{
    /// <summary>How often the worker polls for due schedules.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Base delay for the first delivery retry.</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Maximum retry backoff.</summary>
    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Maximum missed occurrences backfilled under the catch-up policy in a single pass.</summary>
    public int MaxCatchUpRuns { get; init; } = 1000;

    /// <summary>Whether the hosted background worker is enabled.</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>
/// Clock-free core of the scheduling worker: evaluates due schedules against a supplied instant,
/// renders and delivers reports, and persists the run outcome atomically. Kept separate from the
/// hosted <see cref="ReportSchedulingWorker"/> so it can be unit/integration tested with a fixed time.
/// </summary>
public interface IReportScheduleProcessor
{
    /// <summary>Processes every schedule that is due at <paramref name="nowUtc"/>. Returns the count processed.</summary>
    Task<int> ProcessDueSchedulesAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ReportScheduleProcessor : IReportScheduleProcessor
{
    private readonly IReportScheduleStore _store;
    private readonly IScheduledReportRenderer _renderer;
    private readonly ScheduledReportDeliveryRouter _router;
    private readonly ReportSchedulingOptions _options;
    private readonly ILogger<ReportScheduleProcessor> _logger;

    /// <summary>Creates the processor.</summary>
    public ReportScheduleProcessor(
        IReportScheduleStore store,
        IScheduledReportRenderer renderer,
        ScheduledReportDeliveryRouter router,
        IOptions<ReportSchedulingOptions> options,
        ILogger<ReportScheduleProcessor> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<int> ProcessDueSchedulesAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        var due = await _store.GetDueSchedulesAsync(nowUtc, cancellationToken).ConfigureAwait(false);
        var processed = 0;
        foreach (var schedule in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessOneAsync(schedule, nowUtc, cancellationToken).ConfigureAwait(false);
            processed++;
        }

        return processed;
    }

    private async Task ProcessOneAsync(ReportScheduleDto schedule, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var occurrences = ResolveOccurrences(schedule, nowUtc);
        if (occurrences.Count == 0)
        {
            return;
        }

        var attempt = schedule.FailureCount + 1;
        try
        {
            var runs = new List<ScheduleRunRecord>(occurrences.Count);
            foreach (var occurrence in occurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var artifact = await _renderer.RenderAsync(schedule, cancellationToken).ConfigureAwait(false);
                var delivery = new ScheduledReportDelivery(
                    schedule.TenantId,
                    schedule.ScheduleId,
                    schedule.Name,
                    schedule.ReportId,
                    occurrence,
                    schedule.DeliveryTarget,
                    artifact);
                await _router.DeliverAsync(schedule.DeliveryKind, delivery, cancellationToken).ConfigureAwait(false);
                runs.Add(new ScheduleRunRecord(
                    occurrence,
                    nowUtc,
                    nowUtc,
                    ReportScheduleRunStatus.Delivered,
                    attempt,
                    schedule.DeliveryKind,
                    schedule.DeliveryTarget,
                    artifact.FileName,
                    artifact.ContentType,
                    artifact.Bytes.Length,
                    ErrorMessage: null));
            }

            var nextRun = ReportScheduleCalculator.ComputeNextRun(schedule.CronExpression, nowUtc);
            var update = new ScheduleStateUpdate(
                LastRunUtc: nowUtc,
                LastDeliveredUtc: nowUtc,
                NextRunUtc: nextRun,
                RetryAfterUtc: null,
                FailureCount: 0,
                LastStatus: ReportScheduleRunStatus.Delivered,
                LastStatusMessage: $"Delivered {runs.Count} run(s) at {nowUtc:u}",
                PendingOccurrences: []);
            await _store.ApplyRunOutcomeAsync(schedule.TenantId, schedule.ScheduleId, update, runs, cancellationToken).ConfigureAwait(false);
        }
        catch (ReportScheduleConcurrencyException ex)
        {
            // Another worker already advanced this schedule. Skip the losing pass; the winning worker
            // owns the run history and next-run state. See docs/report-server-deployment.md for the
            // multi-instance delivery caveat.
            _logger.LogInformation(
                ex,
                "Scheduled report {ScheduleId} (tenant {TenantId}) was processed concurrently by another worker; skipping this pass.",
                schedule.ScheduleId,
                schedule.TenantId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ApplyFailureAsync(schedule, occurrences, attempt, nowUtc, ex, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ApplyFailureAsync(
        ReportScheduleDto schedule,
        IReadOnlyList<DateTimeOffset> occurrences,
        int attempt,
        DateTimeOffset nowUtc,
        Exception error,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            error,
            "Scheduled report {ScheduleId} (tenant {TenantId}) delivery attempt {Attempt} failed.",
            schedule.ScheduleId,
            schedule.TenantId,
            attempt);

        var retryAt = ReportScheduleCalculator.ComputeRetryAt(
            attempt,
            schedule.MaxAttempts,
            nowUtc,
            _options.RetryBaseDelay,
            _options.RetryMaxDelay);
        var abandoning = retryAt is null;
        var status = abandoning ? ReportScheduleRunStatus.Failed : ReportScheduleRunStatus.Retrying;
        // On retry the same occurrence set is re-attempted; on abandon we advance past it.
        var nextRun = retryAt ?? ReportScheduleCalculator.ComputeNextRun(schedule.CronExpression, nowUtc);
        var pending = abandoning ? Array.Empty<DateTimeOffset>() : occurrences.ToArray();
        var message = abandoning
            ? $"{error.Message}; abandoned after {attempt} attempt(s)"
            : $"{error.Message}; retry at {retryAt:u}";

        var run = new ScheduleRunRecord(
            occurrences[0],
            nowUtc,
            nowUtc,
            status,
            attempt,
            schedule.DeliveryKind,
            schedule.DeliveryTarget,
            ArtifactFileName: null,
            ArtifactContentType: null,
            ArtifactByteCount: 0,
            ErrorMessage: error.Message);

        var update = new ScheduleStateUpdate(
            LastRunUtc: nowUtc,
            LastDeliveredUtc: schedule.LastDeliveredUtc,
            NextRunUtc: nextRun,
            RetryAfterUtc: retryAt,
            FailureCount: attempt,
            LastStatus: status,
            LastStatusMessage: message,
            PendingOccurrences: pending);
        try
        {
            await _store.ApplyRunOutcomeAsync(schedule.TenantId, schedule.ScheduleId, update, [run], cancellationToken).ConfigureAwait(false);
        }
        catch (ReportScheduleConcurrencyException concurrency)
        {
            _logger.LogInformation(
                concurrency,
                "Scheduled report {ScheduleId} (tenant {TenantId}) failure outcome was superseded by another worker; skipping.",
                schedule.ScheduleId,
                schedule.TenantId);
        }
    }

    private IReadOnlyList<DateTimeOffset> ResolveOccurrences(ReportScheduleDto schedule, DateTimeOffset nowUtc)
    {
        // A pending set means a previous attempt failed and its exact occurrences must be retried.
        if (schedule.PendingOccurrencesUtc.Count > 0)
        {
            return schedule.PendingOccurrencesUtc;
        }

        var policy = schedule.MissedRunPolicy == ReportScheduleMissedRunPolicy.CatchUp
            ? MissedRunPolicy.CatchUp
            : MissedRunPolicy.Skip;
        var decision = ReportScheduleCalculator.ResolveDueRuns(
            schedule.CronExpression,
            schedule.LastRunUtc,
            schedule.NextRunUtc,
            nowUtc,
            policy,
            _options.MaxCatchUpRuns);
        return decision.Occurrences;
    }
}

/// <summary>
/// Hosted background worker that periodically drives <see cref="IReportScheduleProcessor"/> using the
/// injected <see cref="TimeProvider"/>. A fresh DI scope is created per poll so the scoped EF store,
/// renderer and request context are correctly disposed each pass.
/// </summary>
public sealed class ReportSchedulingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ReportSchedulingOptions _options;
    private readonly ILogger<ReportSchedulingWorker> _logger;

    /// <summary>Creates the hosted worker.</summary>
    public ReportSchedulingWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<ReportSchedulingOptions> options,
        ILogger<ReportSchedulingWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Report scheduling worker is disabled by configuration.");
            return;
        }

        _logger.LogInformation("Report scheduling worker started (poll interval {Interval}).", _options.PollInterval);
        using var timer = new PeriodicTimer(_options.PollInterval, _timeProvider);
        do
        {
            try
            {
                await RunPassAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failing pass must not kill the worker; log and continue on the next tick.
                _logger.LogError(ex, "Report scheduling worker pass failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunPassAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IReportScheduleProcessor>();
        var processed = await processor.ProcessDueSchedulesAsync(_timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (processed > 0)
        {
            _logger.LogInformation("Report scheduling worker processed {Count} due schedule(s).", processed);
        }
    }
}
