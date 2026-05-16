namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Selection state snapshot produced by the WYSIWYG JS engine.</summary>
public sealed class WysiwygSelectionSnapshot
{
    /// <summary>Logical editor region: Body, Header, Footer, Caption, Footnote, Endnote, Image, or TableCell.</summary>
    public string Region { get; set; } = "Body";

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

    /// <summary>Stable table cell path when the selection is inside a table cell.</summary>
    public string? TableCellPath { get; set; }

    /// <summary>Selected image block id when the active region is an image object.</summary>
    public string? ActiveImageBlockId { get; set; }
}
