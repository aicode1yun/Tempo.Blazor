using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tempo.ReportServer.Api.Rendering;

/// <summary>
/// Hosted background worker that drains the SQL-Server-backed distributed render job queue. On each
/// tick it opens a fresh DI scope (so the scoped EF queue, store, renderer and request context are
/// disposed each pass) and calls <see cref="IReportRenderJobQueue.ProcessNextAsync"/> in a bounded loop
/// until the queue is empty or the per-pass cap is reached. Runs on every render node; the atomic claim
/// inside the queue guarantees each queued job is rendered by exactly one node. Mirrors the shape of
/// <c>ReportSchedulingWorker</c> (injected <see cref="TimeProvider"/>, bounded, self-healing pass).
/// </summary>
public sealed class DistributedRenderJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ReportRenderJobQueueOptions _options;
    private readonly ILogger<DistributedRenderJobWorker> _logger;

    /// <summary>Creates the hosted worker.</summary>
    public DistributedRenderJobWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<ReportRenderJobQueueOptions> options,
        ILogger<DistributedRenderJobWorker> logger)
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
            _logger.LogInformation("Distributed render worker is disabled by configuration.");
            return;
        }

        _logger.LogInformation("Distributed render worker started (poll interval {Interval}).", _options.PollInterval);
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
                _logger.LogError(ex, "Distributed render worker pass failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunPassAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IReportRenderJobQueue>();
        var processed = 0;
        var cap = Math.Max(1, _options.MaxJobsPerPass);
        while (processed < cap)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var job = await queue.ProcessNextAsync(cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                break;
            }

            processed++;
        }

        if (processed > 0)
        {
            _logger.LogInformation("Distributed render worker processed {Count} render job(s).", processed);
        }
    }
}
