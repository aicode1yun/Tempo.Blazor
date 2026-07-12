using Microsoft.Extensions.Options;
using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Hosted service that periodically builds and sends notification digests. Wraps the hosting-free
/// <see cref="NotificationDigestRunner"/> with a timer and exposes <see cref="RunNowAsync"/> so a
/// demo/E2E can trigger a digest immediately instead of waiting for the interval.
/// </summary>
public sealed class TmNotificationDigestService : BackgroundService
{
    private readonly NotificationDigestRunner _runner;
    private readonly TmNotificationDigestOptions _options;
    private readonly ILogger<TmNotificationDigestService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastRun;

    public TmNotificationDigestService(
        ITmNotificationService notifications,
        INotificationRecipientSource recipients,
        INotificationDigestSender sender,
        IOptions<TmNotificationDigestOptions> options,
        ILogger<TmNotificationDigestService> logger)
    {
        _options = options.Value;
        _runner = new NotificationDigestRunner(notifications, recipients, sender, _options);
        _logger = logger;
        _lastRun = DateTimeOffset.UtcNow - _options.Interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            await SafeRunAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs one digest pass immediately over the window since the last run.</summary>
    public Task<IReadOnlyList<TmNotificationDigest>> RunNowAsync(CancellationToken cancellationToken = default)
        => SafeRunAsync(cancellationToken);

    private async Task<IReadOnlyList<TmNotificationDigest>> SafeRunAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var end = DateTimeOffset.UtcNow;
            var start = _lastRun;
            _lastRun = end;
            // Small forward buffer so a notification created "now" is inside the window.
            return await _runner.RunOnceAsync(start, end.AddMinutes(1), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification digest run failed.");
            return [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
    }
}
