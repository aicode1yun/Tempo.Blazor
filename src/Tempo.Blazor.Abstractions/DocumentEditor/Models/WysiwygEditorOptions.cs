namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Options passed to the WYSIWYG JavaScript editing engine during initialization.</summary>
public sealed class WysiwygEditorOptions
{
    /// <summary>Stable identifier for this editor instance.</summary>
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Whether the surface should be read-only.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Schema version of the snapshot protocol.</summary>
    public int ProtocolVersion { get; set; } = 1;

    /// <summary>Whether to enable the MutationObserver guard/fallback.</summary>
    public bool EnableMutationGuard { get; set; } = true;

    /// <summary>Debounce interval in milliseconds for typing batching.</summary>
    public int TypingBatchMs { get; set; } = 500;

    /// <summary>Screen reader help text announced by the editable surface.</summary>
    public string AccessibilityHelp { get; set; } = string.Empty;

    /// <summary>Page label template. Use {0} for the one-based page number.</summary>
    public string PageLabel { get; set; } = "Page {0}";

    /// <summary>Body label template. Use {0} for the one-based page number.</summary>
    public string BodyLabel { get; set; } = "Document body, page {0}";

    /// <summary>Header label template. Use {0} for the one-based page number.</summary>
    public string HeaderLabel { get; set; } = "Header, page {0}";

    /// <summary>Footer label template. Use {0} for the one-based page number.</summary>
    public string FooterLabel { get; set; } = "Footer, page {0}";

    /// <summary>Accessible label for floating image resize handles.</summary>
    public string ImageResizeHandleLabel { get; set; } = "Resize image";

    /// <summary>Label for retrying a failed image load.</summary>
    public string ImageRetryLabel { get; set; } = "Retry";

    /// <summary>Font families allowed for font family marks.</summary>
    public IReadOnlyList<DocumentFontFamily> FontFamilies { get; set; } = [];

    /// <summary>Current editing author used by JS-owned runtime revisions.</summary>
    public DocumentEditorAuthor? Author { get; set; }

    /// <summary>Accessible label used for insertion suggestion decorations.</summary>
    public string SuggestionInsertLabel { get; set; } = "Suggested insertion";

    /// <summary>Accessible label used for deletion suggestion decorations.</summary>
    public string SuggestionDeleteLabel { get; set; } = "Suggested deletion";

    /// <summary>Whether the JS surface should render live track-changes decorations.</summary>
    public bool TrackChangesEnabled { get; set; }

    /// <summary>How tracked changes should be displayed in the JS-owned surface.</summary>
    public DocumentReviewDisplayMode ReviewDisplayMode { get; set; } = DocumentReviewDisplayMode.AllMarkup;
}
