namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Tri-state value describing whether a formatting mark is active in the current selection.</summary>
public enum WysiwygFormattingValue
{
    /// <summary>The mark is not present in the selection.</summary>
    Inactive,

    /// <summary>The mark is present for the entire selection.</summary>
    Active,

    /// <summary>The mark is present for part of the selection.</summary>
    Mixed
}

/// <summary>Snapshot of the active formatting marks and paragraph properties at the current selection.</summary>
public class WysiwygFormattingState
{
    /// <summary>Bold mark state.</summary>
    public WysiwygFormattingValue Bold { get; set; }

    /// <summary>Italic mark state.</summary>
    public WysiwygFormattingValue Italic { get; set; }

    /// <summary>Underline mark state.</summary>
    public WysiwygFormattingValue Underline { get; set; }

    /// <summary>Strikethrough mark state.</summary>
    public WysiwygFormattingValue Strikethrough { get; set; }

    /// <summary>Superscript mark state.</summary>
    public WysiwygFormattingValue Superscript { get; set; }

    /// <summary>Subscript mark state.</summary>
    public WysiwygFormattingValue Subscript { get; set; }

    /// <summary>Paragraph alignment of the selected blocks.</summary>
    public DocumentTextAlignment ParagraphAlignment { get; set; } = DocumentTextAlignment.Left;

    /// <summary>Whether the selected blocks have mixed paragraph alignment.</summary>
    public bool ParagraphAlignmentMixed { get; set; }

    /// <summary>Font family key active at the caret, or null when mixed or absent.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Font size in points active at the caret, or null when mixed or absent.</summary>
    public double? FontSize { get; set; }
}

/// <summary>Position of a floating UI element in CSS pixels relative to the editor viewport.</summary>
public class WysiwygFloatingUiPosition
{
    /// <summary>Left offset in CSS pixels.</summary>
    public double Left { get; set; }

    /// <summary>Top offset in CSS pixels.</summary>
    public double Top { get; set; }

    /// <summary>Optional width.</summary>
    public double? Width { get; set; }

    /// <summary>Optional height.</summary>
    public double? Height { get; set; }
}

/// <summary>Request raised by the JS engine when the user triggers a text context menu.</summary>
public class WysiwygTextContextMenuRequest : WysiwygFloatingUiPosition
{
    /// <summary>Selection snapshot at the time of the request.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }

    /// <summary>Client X coordinate of the pointer event.</summary>
    public double ClientX { get; set; }

    /// <summary>Client Y coordinate of the pointer event.</summary>
    public double ClientY { get; set; }
}

/// <summary>Request raised by the JS engine when the user triggers a table context menu.</summary>
public class WysiwygTableContextMenuRequest : WysiwygFloatingUiPosition
{
    /// <summary>Selection snapshot at the time of the request.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }

    /// <summary>Client X coordinate of the pointer event.</summary>
    public double ClientX { get; set; }

    /// <summary>Client Y coordinate of the pointer event.</summary>
    public double ClientY { get; set; }
}

/// <summary>State of the inline mini-toolbar (position + visibility) reported by the JS engine.</summary>
public class WysiwygMiniToolbarRequest : WysiwygFloatingUiPosition
{
    /// <summary>Whether the mini-toolbar should be visible.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Selection snapshot when the toolbar became visible.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }
}

/// <summary>Request raised by the JS engine when the user interacts with a tracked revision decoration.</summary>
public class WysiwygRevisionReviewRequest
{
    /// <summary>Stable revision identifier.</summary>
    public string RevisionId { get; set; } = string.Empty;

    /// <summary>Requested review action.</summary>
    public DocumentRevisionAction Action { get; set; }
}

/// <summary>Hyperlink data sent to the JS engine when applying a link mark.</summary>
public class WysiwygLinkPayload
{
    /// <summary>Target URL.</summary>
    public string Href { get; set; } = string.Empty;

    /// <summary>Optional link title / tooltip text.</summary>
    public string? Title { get; set; }
}

/// <summary>Hyperlink metadata returned by the JS engine for the selection at the caret.</summary>
public class WysiwygLinkInfo
{
    /// <summary>Target URL of the link mark at the caret.</summary>
    public string Href { get; set; } = string.Empty;

    /// <summary>Optional link title.</summary>
    public string? Title { get; set; }
}

/// <summary>Result of applying a remote operation batch directly to the WYSIWYG DOM.</summary>
public class WysiwygRemoteOperationBatchApplyResult
{
    /// <summary>Whether the batch was applied successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Number of operations successfully applied to the DOM.</summary>
    public int Applied { get; set; }

    /// <summary>Number of operations skipped because they were already reflected in the DOM state.</summary>
    public int Skipped { get; set; }

    /// <summary>Number of operations queued for deferred application.</summary>
    public int Queued { get; set; }

    /// <summary>Operation ids that could not be applied.</summary>
    public IReadOnlyList<string> FailedOperationIds { get; set; } = [];

    /// <summary>Error code when the batch could not be applied.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Creates a successful result.</summary>
    public static WysiwygRemoteOperationBatchApplyResult Ok(int applied = 0, int skipped = 0, int queued = 0) =>
        new() { Success = true, Applied = applied, Skipped = skipped, Queued = queued };

    /// <summary>Creates a failed result with an error code.</summary>
    public static WysiwygRemoteOperationBatchApplyResult Failed(string errorCode) =>
        new() { Success = false, ErrorCode = errorCode };
}

/// <summary>Debug snapshot of the WYSIWYG JS engine state.</summary>
public class WysiwygDebugSnapshot
{
    /// <summary>Whether the JS engine instance is active.</summary>
    public bool HasInstance { get; set; }

    /// <summary>Engine instance identifier.</summary>
    public string? InstanceId { get; set; }

    /// <summary>Active block id at the time the snapshot was taken.</summary>
    public string? ActiveBlockId { get; set; }

    /// <summary>Active inline id at the time the snapshot was taken.</summary>
    public string? ActiveInlineId { get; set; }

    /// <summary>DOM path string for the active node.</summary>
    public string? ActiveDomPath { get; set; }

    /// <summary>Pending transaction id (if a transaction is in progress).</summary>
    public string? PendingTransactionId { get; set; }

    /// <summary>Serialized selection snapshot from the JS engine.</summary>
    public WysiwygSelectionSnapshot? CurrentSelection { get; set; }

    /// <summary>Raw JSON representation of the JS engine document model.</summary>
    public string? Json { get; set; }

    /// <summary>Engine version string.</summary>
    public string? EngineVersion { get; set; }
}

/// <summary>Identifies which tab is active in the document editor side panel.</summary>
public enum DocumentSidePanelTab
{
    /// <summary>Version history tab.</summary>
    Versions,

    /// <summary>Tracked revisions / track-changes tab.</summary>
    Revisions,

    /// <summary>Comments tab.</summary>
    Comments,

    /// <summary>Document properties tab.</summary>
    Properties
}
