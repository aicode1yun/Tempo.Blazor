namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Transient selection state used by document editor UI components.</summary>
public class DocumentEditorSelectionState
{
    /// <summary>Currently active block id.</summary>
    public string? ActiveBlockId { get; set; }

    /// <summary>Focused inline range prepared for future rich selection handling.</summary>
    public DocumentEditorInlineRange? FocusedInlineRange { get; set; }

    /// <summary>Selected table cell id, when the active block is a table.</summary>
    public string? ActiveTableCellId { get; set; }

    /// <summary>Clears the current selection.</summary>
    public void Clear()
    {
        ActiveBlockId = null;
        FocusedInlineRange = null;
        ActiveTableCellId = null;
    }
}

/// <summary>Inline range inside a text block.</summary>
public class DocumentEditorInlineRange
{
    /// <summary>Block id that owns the inline range.</summary>
    public string? BlockId { get; set; }

    /// <summary>Start inline index.</summary>
    public int StartInlineIndex { get; set; }

    /// <summary>Start text offset inside the start inline.</summary>
    public int StartOffset { get; set; }

    /// <summary>End inline index.</summary>
    public int EndInlineIndex { get; set; }

    /// <summary>End text offset inside the end inline.</summary>
    public int EndOffset { get; set; }
}
