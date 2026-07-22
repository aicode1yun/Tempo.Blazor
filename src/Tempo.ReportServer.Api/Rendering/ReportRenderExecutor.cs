using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.ReportServer.Api.Storage;

namespace Tempo.ReportServer.Api.Rendering;

/// <summary>The classification of a synchronous render pipeline execution.</summary>
public enum ReportRenderOutcome
{
    /// <summary>The render completed within all limits.</summary>
    Succeeded,

    /// <summary>The render exceeded the synchronous page quota.</summary>
    PageQuotaExceeded,

    /// <summary>The render payload exceeded the maximum output size.</summary>
    OutputTooLarge,

    /// <summary>The render exceeded the configured timeout and was cancelled.</summary>
    TimedOut,

    /// <summary>The concurrency queue was full; the render was rejected without executing.</summary>
    Overloaded,
}

/// <summary>The result of a synchronous render pipeline execution.</summary>
public sealed record ReportRenderExecutionResult(ReportRenderOutcome Outcome, RenderReportResultDto? Result, string Message)
{
    /// <summary>Convenience factory for a successful outcome.</summary>
    public static ReportRenderExecutionResult Success(RenderReportResultDto result)
        => new(ReportRenderOutcome.Succeeded, result, "OK");
}

/// <summary>
/// Applies operational limits around a synchronous render: bounded concurrency (a shared semaphore),
/// a bounded wait queue, a per-render timeout, and an output-size cap; records OpenTelemetry metrics
/// and structured logs. A singleton so the concurrency gate and queue counter are shared process-wide;
/// the scoped <see cref="IReportServerRenderer"/> is passed per call rather than injected.
/// </summary>
public interface IReportRenderExecutor
{
    /// <summary>Executes a render under the configured concurrency, timeout, and output-size limits.</summary>
    Task<ReportRenderExecutionResult> ExecuteAsync(
        IReportServerRenderer renderer,
        ReportDetailDto report,
        RenderReportRequestDto request,
        ReportExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ReportRenderExecutor : IReportRenderExecutor, IDisposable
{
    private readonly ReportServerQuotaOptions _options;
    private readonly ReportRenderMetrics _metrics;
    private readonly ILogger<ReportRenderExecutor> _logger;
    private readonly SemaphoreSlim _gate;
    private int _pending;

    /// <summary>Creates the executor.</summary>
    public ReportRenderExecutor(
        IOptions<ReportServerQuotaOptions> options,
        ReportRenderMetrics metrics,
        ILogger<ReportRenderExecutor> logger)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var maxConcurrent = Math.Max(1, _options.MaxConcurrentRenders);
        _gate = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    /// <inheritdoc />
    public async Task<ReportRenderExecutionResult> ExecuteAsync(
        IReportServerRenderer renderer,
        ReportDetailDto report,
        RenderReportRequestDto request,
        ReportExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var maxConcurrent = Math.Max(1, _options.MaxConcurrentRenders);
        var capacity = maxConcurrent + Math.Max(0, _options.MaxRenderQueueLength);
        var format = request.Format.ToString();

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = request.TenantId,
            ["ReportId"] = request.ReportId,
            ["Format"] = format,
        });

        var pending = Interlocked.Increment(ref _pending);
        _metrics.EnterQueue();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (pending > capacity)
            {
                _logger.LogWarning(
                    "Render rejected: {Pending} pending renders exceed capacity {Capacity}.", pending, capacity);
                _metrics.RecordRender(request.TenantId, format, "overloaded", stopwatch.Elapsed.TotalMilliseconds, succeeded: false);
                return new ReportRenderExecutionResult(
                    ReportRenderOutcome.Overloaded,
                    Result: null,
                    "The render server is at capacity. Retry shortly or use an async render job.");
            }

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.Timeout);
                try
                {
                    var result = await renderer.RenderAsync(report, request, context, timeoutCts.Token).ConfigureAwait(false);
                    return Evaluate(request, format, result, stopwatch);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Render timed out after {TimeoutMs} ms.", _options.Timeout.TotalMilliseconds);
                    _metrics.RecordRender(request.TenantId, format, "timeout", stopwatch.Elapsed.TotalMilliseconds, succeeded: false);
                    return new ReportRenderExecutionResult(
                        ReportRenderOutcome.TimedOut,
                        Result: null,
                        $"The render exceeded the {_options.Timeout.TotalSeconds:0}s timeout.");
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pending);
            _metrics.LeaveQueue();
        }
    }

    private ReportRenderExecutionResult Evaluate(
        RenderReportRequestDto request,
        string format,
        RenderReportResultDto result,
        Stopwatch stopwatch)
    {
        var durationMs = stopwatch.Elapsed.TotalMilliseconds;
        if (result.PageCount > _options.MaxSynchronousPages)
        {
            _logger.LogWarning(
                "Render exceeded page quota: {PageCount} > {MaxPages}.", result.PageCount, _options.MaxSynchronousPages);
            _metrics.RecordRender(request.TenantId, format, "page_quota", durationMs, succeeded: false);
            return new ReportRenderExecutionResult(
                ReportRenderOutcome.PageQuotaExceeded,
                Result: null,
                "The report exceeded the synchronous page quota.");
        }

        var byteCount = result.Bytes?.LongLength ?? 0L;
        if (byteCount > _options.MaxOutputBytes)
        {
            _logger.LogWarning(
                "Render exceeded output size: {ByteCount} > {MaxBytes} bytes.", byteCount, _options.MaxOutputBytes);
            _metrics.RecordRender(request.TenantId, format, "output_too_large", durationMs, succeeded: false);
            return new ReportRenderExecutionResult(
                ReportRenderOutcome.OutputTooLarge,
                Result: null,
                "The render output exceeded the maximum allowed size.");
        }

        _logger.LogInformation(
            "Render succeeded: {PageCount} page(s), {ByteCount} byte(s) in {DurationMs:0} ms.",
            result.PageCount,
            byteCount,
            durationMs);
        _metrics.RecordRender(request.TenantId, format, "succeeded", durationMs, succeeded: true);
        return ReportRenderExecutionResult.Success(result);
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
