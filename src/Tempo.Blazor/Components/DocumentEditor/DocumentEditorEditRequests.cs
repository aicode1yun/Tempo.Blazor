using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor;

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
