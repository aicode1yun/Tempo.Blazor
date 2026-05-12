using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Command emitted by editable document blocks.</summary>
public enum DocumentEditorBlockCommand
{
    /// <summary>Insert a paragraph after the current block.</summary>
    InsertParagraphAfter,

    /// <summary>Insert a list item after the current list block.</summary>
    InsertListAfter,

    /// <summary>Merge an empty block into the previous block.</summary>
    MergeWithPreviousIfEmpty,

    /// <summary>Increase list indentation.</summary>
    IncreaseIndent,

    /// <summary>Decrease list indentation.</summary>
    DecreaseIndent,

    /// <summary>Add a table row.</summary>
    AddTableRow,

    /// <summary>Add a table column.</summary>
    AddTableColumn,

    /// <summary>Delete a table row.</summary>
    DeleteTableRow,

    /// <summary>Delete a table column.</summary>
    DeleteTableColumn,

    /// <summary>Merge the selected table cell with the cell to the right.</summary>
    MergeCellRight,

    /// <summary>Split the selected merged table cell.</summary>
    SplitCell,

    /// <summary>Delete the current block.</summary>
    DeleteBlock,

    /// <summary>Insert a footnote reference after the block text.</summary>
    InsertFootnote,

    /// <summary>Insert an endnote reference after the block text.</summary>
    InsertEndnote,

    /// <summary>Toggle floating layout for an image block.</summary>
    ToggleFloatingImage
}

/// <summary>Editable document block command request.</summary>
public class DocumentEditorBlockCommandRequest
{
    /// <summary>Block id that emitted the command.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Command to execute.</summary>
    public DocumentEditorBlockCommand Command { get; set; }

    /// <summary>Optional row index for table commands.</summary>
    public int? RowIndex { get; set; }

    /// <summary>Optional column index for table commands.</summary>
    public int? ColumnIndex { get; set; }

    /// <summary>Optional table cell id for table commands.</summary>
    public string? CellId { get; set; }
}

/// <summary>Editable block change event.</summary>
public class DocumentEditorBlockChangedEventArgs
{
    /// <summary>Changed block.</summary>
    public DocumentBlock Block { get; set; } = new();

    /// <summary>Text before the change.</summary>
    public string? OriginalText { get; set; }

    /// <summary>Text after the change.</summary>
    public string? NewText { get; set; }

    /// <summary>Whether the change represents formatting instead of text replacement.</summary>
    public bool IsFormattingChange { get; set; }
}

/// <summary>Reason that triggered a document editor save.</summary>
public enum DocumentEditorSaveTrigger
{
    /// <summary>User explicitly requested a save.</summary>
    Explicit,

    /// <summary>Autosave timer requested a save.</summary>
    AutoSave
}

/// <summary>Draft values entered in the create-version dialog.</summary>
public sealed class DocumentVersionDialogResult
{
    /// <summary>Version kind to create.</summary>
    public DocumentVersionKind Kind { get; set; } = DocumentVersionKind.Minor;

    /// <summary>Optional version label.</summary>
    public string? Label { get; set; }

    /// <summary>Optional version description.</summary>
    public string? Description { get; set; }
}

/// <summary>Request emitted when the user wants to add a comment anchored to the document.</summary>
public sealed class DocumentCommentCreateRequest
{
    /// <summary>Anchor for the new comment thread.</summary>
    public DocumentCommentAnchor Anchor { get; set; } = new();

    /// <summary>Initial comment text.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>Request emitted when the user replies to an existing comment thread.</summary>
public sealed class DocumentEditorCommentReplyRequest
{
    /// <summary>Target comment thread id.</summary>
    public string CommentId { get; set; } = string.Empty;

    /// <summary>Reply text.</summary>
    public string Text { get; set; } = string.Empty;
}
