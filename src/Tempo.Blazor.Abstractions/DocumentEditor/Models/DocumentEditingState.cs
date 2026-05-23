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

    /// <summary>Selected table block id, when the active block is a table.</summary>
    public string? ActiveTableId { get; set; }

    /// <summary>Selected image block id, when the active selection is an image.</summary>
    public string? ActiveImageBlockId { get; set; }

    /// <summary>Selected comment id when the active selection targets a comment anchor.</summary>
    public string? ActiveCommentId { get; set; }

    /// <summary>Selected revision id when the active selection targets a tracked-change marker.</summary>
    public string? ActiveRevisionId { get; set; }

    /// <summary>Layout line id for the active caret hit-test, when available.</summary>
    public string? LayoutLineId { get; set; }

    /// <summary>Layout segment id for the active caret hit-test, when available.</summary>
    public string? LayoutSegmentId { get; set; }

    /// <summary>Zero-based visual line index within the active paragraph layout.</summary>
    public int? VisualLineIndex { get; set; }

    /// <summary>Selected layout object id, when the active hit target is an object.</summary>
    public string? ActiveObjectId { get; set; }

    /// <summary>Kind of target found by the WYSIWYG hit-test service.</summary>
    public string? HitTargetKind { get; set; }

    /// <summary>Logical editor region that owns the active selection.</summary>
    public string Region { get; set; } = "Body";

    /// <summary>Active header/footer id when <see cref="Region"/> is Header or Footer.</summary>
    public string? HeaderFooterId { get; set; }

    /// <summary>Zero-based rendered page index for the active selection.</summary>
    public int? PageIndex { get; set; }

    /// <summary>Clears the current selection.</summary>
    public void Clear()
    {
        ActiveBlockId = null;
        FocusedInlineRange = null;
        ActiveTableCellId = null;
        ActiveTableId = null;
        ActiveImageBlockId = null;
        ActiveCommentId = null;
        ActiveRevisionId = null;
        LayoutLineId = null;
        LayoutSegmentId = null;
        VisualLineIndex = null;
        ActiveObjectId = null;
        HitTargetKind = null;
        Region = "Body";
        HeaderFooterId = null;
        PageIndex = null;
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
