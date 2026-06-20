using Microsoft.JSInterop;
using Tempo.Blazor.DocumentEditor.Performance;

namespace Tempo.Blazor.Components.DocumentEditor.Performance;

/// <summary>Thin C# wrapper over <c>window.tmDocumentEditorPerformance</c>. Use from
/// benchmarks, manual profiling, or automated regression guards. The probe is not
/// part of the editor's normal hot path — instantiate around a discrete action and
/// dispose / stop when finished.</summary>
public sealed class WysiwygPerformanceProbe
{
    private readonly IJSRuntime _jsRuntime;

    public WysiwygPerformanceProbe(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <summary>Starts a capture window for the given editor instance. Re-starting an
    /// existing capture overwrites the previous one without emitting a report.</summary>
    public ValueTask StartCaptureAsync(string instanceId, string label, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Instance id is required.", nameof(instanceId));
        return _jsRuntime.InvokeVoidAsync(
            "tmDocumentEditorPerformance.startCapture",
            cancellationToken,
            instanceId,
            label ?? string.Empty);
    }

    /// <summary>Stops the capture window and returns the aggregated report. Returns
    /// <c>null</c> if no capture was active for the supplied instance.</summary>
    public ValueTask<WysiwygPerformanceReport?> StopCaptureAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Instance id is required.", nameof(instanceId));
        return _jsRuntime.InvokeAsync<WysiwygPerformanceReport?>(
            "tmDocumentEditorPerformance.stopCapture",
            cancellationToken,
            instanceId);
    }

    /// <summary>Notes an out-of-band JS interop call so the next capture's
    /// <see cref="WysiwygPerformanceReport.JsInteropCallCount"/> reflects it.</summary>
    public ValueTask NoteInteropCallAsync(int count = 1, CancellationToken cancellationToken = default)
    {
        return _jsRuntime.InvokeVoidAsync(
            "tmDocumentEditorPerformance.noteJsInteropCall",
            cancellationToken,
            count);
    }

    /// <summary>Clears all active captures globally. Mainly used by tests.</summary>
    public ValueTask ClearAllAsync(CancellationToken cancellationToken = default)
    {
        return _jsRuntime.InvokeVoidAsync("tmDocumentEditorPerformance.clearAll", cancellationToken);
    }
}
