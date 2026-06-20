namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Selection state snapshot produced by the WYSIWYG JS engine.</summary>
public sealed class WysiwygSelectionSnapshot
{
    /// <summary>Logical editor region: Body, Header, Footer, Caption, Footnote, Endnote, Image, or TableCell.</summary>
    public string Region { get; set; } = "Body";

    /// <summary>Current selection mode. Text keeps a caret/range; Object targets a drawing or block object.</summary>
    public string SelectionMode { get; set; } = "Text";

    /// <summary>Text caret or range snapshot preserved independently from object selection.</summary>
    public WysiwygTextSelectionSnapshot? TextSelection { get; set; }

    /// <summary>Object selection snapshot when <see cref="SelectionMode"/> is Object.</summary>
    public WysiwygObjectSelectionSnapshot? ObjectSelection { get; set; }

    /// <summary>Zero-based rendered page index when the region can be resolved.</summary>
    public int? PageIndex { get; set; }

    /// <summary>Header/footer definition id when the selection is inside a header or footer region.</summary>
    public string? HeaderFooterId { get; set; }

    /// <summary>Runtime node id for the anchor position, usually the inline id.</summary>
    public string? AnchorNodeId { get; set; }

    /// <summary>Runtime node id for the focus position, usually the inline id.</summary>
    public string? FocusNodeId { get; set; }

    /// <summary>Block id of the anchor (start) position.</summary>
    public string? AnchorBlockId { get; set; }

    /// <summary>Inline id of the anchor (start) position.</summary>
    public string? AnchorInlineId { get; set; }

    /// <summary>Text offset within the anchor inline.</summary>
    public int AnchorOffset { get; set; }

    /// <summary>Absolute text offset within the anchor block for restore fallback after inline split or merge.</summary>
    public int AnchorBlockOffset { get; set; }

    /// <summary>Block id of the focus (end) position.</summary>
    public string? FocusBlockId { get; set; }

    /// <summary>Inline id of the focus (end) position.</summary>
    public string? FocusInlineId { get; set; }

    /// <summary>Text offset within the focus inline.</summary>
    public int FocusOffset { get; set; }

    /// <summary>Absolute text offset within the focus block for restore fallback after inline split or merge.</summary>
    public int FocusBlockOffset { get; set; }

    /// <summary>Whether the selection is collapsed (caret).</summary>
    public bool IsCollapsed { get; set; }

    /// <summary>Direction of the selection: forward or backward.</summary>
    public string Direction { get; set; } = "forward";

    /// <summary>Phase 13: When the selection is inside a table cell, this is the cell id.</summary>
    public string? ActiveTableCellId { get; set; }

    /// <summary>Table block id when the selection is inside or targets a table.</summary>
    public string? ActiveTableId { get; set; }

    /// <summary>Stable table cell path when the selection is inside a table cell.</summary>
    public string? TableCellPath { get; set; }

    /// <summary>Selected image block id when the active region is an image object.</summary>
    public string? ActiveImageBlockId { get; set; }

    /// <summary>Comment id when the active selection targets a comment anchor.</summary>
    public string? ActiveCommentId { get; set; }

    /// <summary>Revision id when the active selection targets a tracked-change marker.</summary>
    public string? ActiveRevisionId { get; set; }

    /// <summary>Layout line id that produced the active caret hit-test, when available.</summary>
    public string? LayoutLineId { get; set; }

    /// <summary>Layout segment id that produced the active caret hit-test, when available.</summary>
    public string? LayoutSegmentId { get; set; }

    /// <summary>Zero-based visual line index within the paragraph layout, when available.</summary>
    public int? VisualLineIndex { get; set; }

    /// <summary>Selected layout object id, when the active hit target is an object.</summary>
    public string? ActiveObjectId { get; set; }

    /// <summary>Kind of target found by the WYSIWYG hit-test service.</summary>
    public string? HitTargetKind { get; set; }

    /// <summary>Stable runtime-owned selection token used to execute toolbar commands after focus moves away from the surface.</summary>
    public string? SelectionToken { get; set; }

    /// <summary>Compatibility alias for <see cref="SelectionToken"/> used by JavaScript diagnostics and older test probes.</summary>
    public string? StableSelectionToken { get; set; }

    /// <summary>Structured token diagnostics emitted by the JavaScript runtime for failed command analysis.</summary>
    public object? SelectionTokenData { get; set; }
}

/// <summary>Text selection snapshot embedded in a WYSIWYG selection payload.</summary>
public sealed class WysiwygTextSelectionSnapshot
{
    /// <summary>Logical editor region that owns the text selection.</summary>
    public string Region { get; set; } = "Body";

    /// <summary>Block id of the text anchor position.</summary>
    public string? AnchorBlockId { get; set; }

    /// <summary>Inline id of the text anchor position.</summary>
    public string? AnchorInlineId { get; set; }

    /// <summary>Absolute text offset of the anchor inside its block.</summary>
    public int AnchorOffset { get; set; }

    /// <summary>Block id of the text focus position.</summary>
    public string? FocusBlockId { get; set; }

    /// <summary>Inline id of the text focus position.</summary>
    public string? FocusInlineId { get; set; }

    /// <summary>Absolute text offset of the focus inside its block.</summary>
    public int FocusOffset { get; set; }

    /// <summary>Whether the text selection is collapsed to a caret.</summary>
    public bool IsCollapsed { get; set; } = true;

    /// <summary>Selection direction: none, forward, or backward.</summary>
    public string Direction { get; set; } = "none";

    /// <summary>Header/footer definition id when the selection is inside a header or footer.</summary>
    public string? HeaderFooterId { get; set; }
}

/// <summary>Object selection snapshot embedded in a WYSIWYG selection payload.</summary>
public sealed class WysiwygObjectSelectionSnapshot
{
    /// <summary>Logical editor region that owns the selected object.</summary>
    public string Region { get; set; } = "Body";

    /// <summary>Object kind, for example image.</summary>
    public string Kind { get; set; } = "image";

    /// <summary>Stable selected object id used as the source of truth for drawing objects.</summary>
    public string? ObjectId { get; set; }

    /// <summary>Block id currently associated with the object, usually the anchor paragraph id for drawing runs.</summary>
    public string? BlockId { get; set; }

    /// <summary>Paragraph block id that anchors the object.</summary>
    public string? AnchorBlockId { get; set; }

    /// <summary>Inline id that anchors the object when available.</summary>
    public string? AnchorInlineId { get; set; }

    /// <summary>Zero-based inline index that anchors the object.</summary>
    public int AnchorInlineIndex { get; set; } = -1;

    /// <summary>Text offset in the anchor paragraph.</summary>
    public int AnchorOffset { get; set; }

    /// <summary>Zero-based inline index of the drawing run.</summary>
    public int InlineIndex { get; set; } = -1;

    /// <summary>Inline run id of the selected drawing object.</summary>
    public string? RunId { get; set; }

    /// <summary>Text selection that should be restored when object selection is dismissed.</summary>
    public WysiwygTextSelectionSnapshot? TextSelection { get; set; }
}
