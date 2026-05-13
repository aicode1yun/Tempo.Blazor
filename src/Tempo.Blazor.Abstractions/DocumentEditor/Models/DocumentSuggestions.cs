namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Suggestion proposed outside the core document snapshot.</summary>
public sealed class DocumentSuggestion
{
    /// <summary>Stable suggestion id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Suggestion type.</summary>
    public DocumentSuggestionType Type { get; set; } = DocumentSuggestionType.ReplaceText;

    /// <summary>Target range for the suggestion.</summary>
    public DocumentRevisionRange Range { get; set; } = new();

    /// <summary>Suggested replacement text or inserted text.</summary>
    public string? SuggestedText { get; set; }

    /// <summary>Original text captured by the suggestion provider.</summary>
    public string? OriginalText { get; set; }

    /// <summary>Suggestion author.</summary>
    public DocumentEditorAuthor Author { get; set; } = new();

    /// <summary>Suggestion status.</summary>
    public DocumentSuggestionStatus Status { get; set; } = DocumentSuggestionStatus.Pending;

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional hash of the document snapshot the suggestion was created against.</summary>
    public string? BaseSnapshotHash { get; set; }

    /// <summary>Structured operations that apply the suggestion when accepted.</summary>
    public List<DocumentOperation> Operations { get; set; } = [];

    /// <summary>Author who reviewed the suggestion.</summary>
    public DocumentEditorAuthor? Reviewer { get; set; }

    /// <summary>Timestamp when the suggestion was accepted or rejected.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Optional provider-specific payload.</summary>
    public string? PayloadJson { get; set; }
}

/// <summary>Suggestion type.</summary>
public enum DocumentSuggestionType
{
    /// <summary>Insert text at the range.</summary>
    InsertText,

    /// <summary>Delete text in the range.</summary>
    DeleteText,

    /// <summary>Replace text in the range.</summary>
    ReplaceText,

    /// <summary>Apply a formatting change.</summary>
    Formatting,

    /// <summary>Move a block or text range.</summary>
    Move
}

/// <summary>Suggestion review status.</summary>
public enum DocumentSuggestionStatus
{
    /// <summary>Suggestion is awaiting review.</summary>
    Pending,

    /// <summary>Suggestion was accepted by the host application.</summary>
    Accepted,

    /// <summary>Suggestion was rejected by the host application.</summary>
    Rejected
}

/// <summary>Request for listing document suggestions.</summary>
public sealed class DocumentSuggestionQuery
{
    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Optional status filter.</summary>
    public DocumentSuggestionStatus? Status { get; set; }
}

/// <summary>Request for applying a suggestion review decision.</summary>
public sealed class DocumentSuggestionReviewRequest
{
    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Suggestion id.</summary>
    public string SuggestionId { get; set; } = string.Empty;

    /// <summary>New status.</summary>
    public DocumentSuggestionStatus Status { get; set; }

    /// <summary>Author who reviewed the suggestion.</summary>
    public DocumentEditorAuthor Reviewer { get; set; } = new();
}
