namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Selection state snapshot produced by the WYSIWYG JS engine.</summary>
public sealed class WysiwygSelectionSnapshot
{
    /// <summary>Block id of the anchor (start) position.</summary>
    public string? AnchorBlockId { get; set; }

    /// <summary>Inline id of the anchor (start) position.</summary>
    public string? AnchorInlineId { get; set; }

    /// <summary>Text offset within the anchor inline.</summary>
    public int AnchorOffset { get; set; }

    /// <summary>Block id of the focus (end) position.</summary>
    public string? FocusBlockId { get; set; }

    /// <summary>Inline id of the focus (end) position.</summary>
    public string? FocusInlineId { get; set; }

    /// <summary>Text offset within the focus inline.</summary>
    public int FocusOffset { get; set; }

    /// <summary>Whether the selection is collapsed (caret).</summary>
    public bool IsCollapsed { get; set; }

    /// <summary>Direction of the selection: forward or backward.</summary>
    public string Direction { get; set; } = "forward";

    /// <summary>Phase 13: When the selection is inside a table cell, this is the cell id.</summary>
    public string? ActiveTableCellId { get; set; }
}
