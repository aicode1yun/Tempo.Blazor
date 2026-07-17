using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Tempo.ReportServer.Api.Rendering;

/// <summary>
/// OpenTelemetry-compatible instruments for the render pipeline, published through a
/// <see cref="System.Diagnostics.Metrics.Meter"/> so any OTel exporter (or <c>dotnet-counters</c>)
/// can observe them without an extra dependency. Records total and failed renders, a duration
/// histogram, and an observable gauge of the current in-flight + queued render depth.
/// </summary>
public sealed class ReportRenderMetrics : IDisposable
{
    /// <summary>Meter name; register it with an OTel <c>MeterProvider</c> to export these instruments.</summary>
    public const string MeterName = "Tempo.ReportServer.Rendering";

    private readonly Meter _meter;
    private readonly Counter<long> _rendersTotal;
    private readonly Counter<long> _rendersFailed;
    private readonly Histogram<double> _renderDuration;
    private int _queueDepth;

    /// <summary>Creates the metrics and registers the observable queue-depth gauge.</summary>
    public ReportRenderMetrics()
    {
        _meter = new Meter(MeterName);
        _rendersTotal = _meter.CreateCounter<long>(
            "reportserver.renders.total",
            unit: "{render}",
            description: "Total number of synchronous renders that reached the render pipeline.");
        _rendersFailed = _meter.CreateCounter<long>(
            "reportserver.renders.failed",
            unit: "{render}",
            description: "Number of renders that failed, timed out, were rejected, or exceeded a quota.");
        _renderDuration = _meter.CreateHistogram<double>(
            "reportserver.render.duration",
            unit: "ms",
            description: "Wall-clock duration of a synchronous render in milliseconds.");
        _meter.CreateObservableGauge(
            "reportserver.render.queue.depth",
            () => Volatile.Read(ref _queueDepth),
            unit: "{render}",
            description: "Renders currently executing plus renders waiting for a concurrency slot.");
    }

    /// <summary>Increments the in-flight + queued depth. Call on pipeline entry.</summary>
    public void EnterQueue() => Interlocked.Increment(ref _queueDepth);

    /// <summary>Decrements the in-flight + queued depth. Call on pipeline exit.</summary>
    public void LeaveQueue() => Interlocked.Decrement(ref _queueDepth);

    /// <summary>Records a completed render (success or otherwise) with its duration and outcome.</summary>
    public void RecordRender(string tenantId, string format, string outcome, double durationMs, bool succeeded)
    {
        var tags = new TagList
        {
            { "tenant", tenantId },
            { "format", format },
            { "outcome", outcome },
        };
        _rendersTotal.Add(1, tags);
        _renderDuration.Record(durationMs, tags);
        if (!succeeded)
        {
            _rendersFailed.Add(1, tags);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
