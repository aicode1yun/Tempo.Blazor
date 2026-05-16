namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Debug snapshot of the live WYSIWYG editor state for failed E2E diagnostics.</summary>
public sealed class WysiwygDebugSnapshot
{
    /// <summary>WYSIWYG engine instance id.</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Whether the JavaScript engine instance was found.</summary>
    public bool HasInstance { get; set; }

    /// <summary>Whether the JavaScript engine instance has been disposed.</summary>
    public bool IsDisposed { get; set; }

    /// <summary>Whether the live surface is read-only.</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>Whether track changes is currently enabled in the live surface.</summary>
    public bool TrackChangesEnabled { get; set; }

    /// <summary>Whether IME/composition input is currently active.</summary>
    public bool CompositionActive { get; set; }

    /// <summary>Whether the engine is currently accepting browser-native input.</summary>
    public bool AcceptingNativeInput { get; set; }

    /// <summary>Current typing transaction id, if any.</summary>
    public string? CurrentTransactionId { get; set; }

    /// <summary>Pending patch transaction id, if a patch is queued.</summary>
    public string? PendingTransactionId { get; set; }

    /// <summary>Pending patch type, if a patch is queued.</summary>
    public string? PendingPatchType { get; set; }

    /// <summary>Number of remote operation batches waiting for the local input transaction to finish.</summary>
    public int QueuedRemoteBatchCount { get; set; }

    /// <summary>Last browser input type observed by the engine.</summary>
    public string? LastInputType { get; set; }

    /// <summary>Length of the last browser input data payload.</summary>
    public int LastInputDataLength { get; set; }

    /// <summary>Last patch type dispatched to Blazor.</summary>
    public string? LastPatchType { get; set; }

    /// <summary>Last patch id, if the patch carried one.</summary>
    public string? LastPatchId { get; set; }

    /// <summary>Last patch transaction id.</summary>
    public string? LastPatchTransactionId { get; set; }

    /// <summary>ISO timestamp of the last patch dispatch.</summary>
    public string? LastPatchAt { get; set; }

    /// <summary>Current browser selection mapped to document ids.</summary>
    public WysiwygSelectionSnapshot? CurrentSelection { get; set; }

    /// <summary>Last selection snapshot remembered by the engine.</summary>
    public WysiwygSelectionSnapshot? LastSelection { get; set; }

    /// <summary>Active block id inferred from selection or focused element.</summary>
    public string? ActiveBlockId { get; set; }

    /// <summary>Active inline id inferred from selection or focused element.</summary>
    public string? ActiveInlineId { get; set; }

    /// <summary>Tag name of the active DOM element.</summary>
    public string? ActiveElementTagName { get; set; }

    /// <summary>Test id of the active DOM element, if present.</summary>
    public string? ActiveElementTestId { get; set; }

    /// <summary>CSS classes of the active DOM element.</summary>
    public string? ActiveElementClasses { get; set; }

    /// <summary>Compact DOM path from the host root to the selected/focused node.</summary>
    public string? ActiveDomPath { get; set; }

    /// <summary>Text offset of the active caret within the active inline.</summary>
    public int ActiveTextOffset { get; set; }

    /// <summary>Whether focus is currently inside the WYSIWYG host root.</summary>
    public bool RootHasFocus { get; set; }

    /// <summary>Number of rendered document blocks.</summary>
    public int RenderedBlockCount { get; set; }

    /// <summary>Number of rendered revision decorations.</summary>
    public int RevisionElementCount { get; set; }

    /// <summary>Number of rendered image elements.</summary>
    public int ImageElementCount { get; set; }

    /// <summary>Total text length visible in the WYSIWYG host root.</summary>
    public int BodyTextLength { get; set; }
}
