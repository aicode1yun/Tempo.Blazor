namespace Tempo.Blazor.DocumentEditor.Performance;

/// <summary>Aggregated result of a single performance capture window. Returned from
/// <c>window.tmDocumentEditorPerformance.stopCapture</c>. All numeric fields are deltas
/// between <c>startCapture</c> and <c>stopCapture</c>.</summary>
public sealed record WysiwygPerformanceReport
{
    /// <summary>Identifier of the editor instance the capture targeted.</summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>Free-form label supplied by the caller of <c>startCapture</c>.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Wall-clock elapsed time of the capture window in milliseconds.</summary>
    public double ElapsedMs { get; init; }

    /// <summary>Number of <c>Element.getBoundingClientRect</c> / <c>getClientRects</c>
    /// calls observed during the capture (each forces synchronous layout).</summary>
    public long ForcedReflowCount { get; init; }

    /// <summary>Number of JS interop calls recorded via
    /// <c>tmDocumentEditorPerformance.noteJsInteropCall</c> during the capture.</summary>
    public long JsInteropCallCount { get; init; }

    public long KeyDownCount { get; init; }
    public long BeforeInputCount { get; init; }
    public long InputDomApplyCount { get; init; }
    public long FullRenderCount { get; init; }
    public long PartialRenderCount { get; init; }
    public long RenderSwapCount { get; init; }
    public long FullRenderSwapCount { get; init; }
    public long ModelCommitCount { get; init; }
    public long BlazorInteropCallCount { get; init; }
    public long BlazorCallbackDuringTypingCount { get; init; }
    public long LayoutPassCount { get; init; }
    public double LayoutPassTotalMs { get; init; }
    public long RenderPassCount { get; init; }
    public double RenderPassTotalMs { get; init; }
    public long InputOperationCount { get; init; }
    public double InputOperationTotalMs { get; init; }
    public long TypingLatencyCount { get; init; }
    public double TypingLatencyTotalMs { get; init; }
    public long MaxTypingBatchSize { get; init; }
    public string ActiveRegion { get; init; } = "Body";
}
