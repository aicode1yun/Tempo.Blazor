namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Describes the JS-owned undo/redo stack state for a WYSIWYG editor instance.</summary>
public sealed class WysiwygUndoState
{
    /// <summary>Whether an undo operation can currently run.</summary>
    public bool CanUndo { get; set; }

    /// <summary>Whether a redo operation can currently run.</summary>
    public bool CanRedo { get; set; }

    /// <summary>Number of undoable transactions, including an open typing transaction.</summary>
    public int UndoDepth { get; set; }

    /// <summary>Number of redoable transactions.</summary>
    public int RedoDepth { get; set; }

    /// <summary>Description of the next undo operation.</summary>
    public string? NextUndoDescription { get; set; }

    /// <summary>Description of the next redo operation.</summary>
    public string? NextRedoDescription { get; set; }

    /// <summary>Runtime epoch used to discard stale local patches after undo/redo.</summary>
    public int Epoch { get; set; }

    /// <summary>Identifier of the currently open transaction, if any.</summary>
    public string? PendingTransactionId { get; set; }

    /// <summary>Identifier of the latest committed transaction, if any.</summary>
    public string? LastTransactionId { get; set; }

    /// <summary>Whether the state comes from the JS-owned runtime undo stack.</summary>
    public bool JsOwnedUndo { get; set; }
}
