namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Single entry inside a shared comment thread.</summary>
public sealed class TmCommentEntry
{
    /// <summary>Stable entry identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Parent thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Optional parent entry identifier for threaded replies.</summary>
    public string? ParentEntryId { get; set; }

    /// <summary>Author snapshot captured when the entry was created.</summary>
    public TmUserRef Author { get; set; } = new();

    /// <summary>Entry body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Entry body format.</summary>
    public TmCommentBodyFormat BodyFormat { get; set; } = TmCommentBodyFormat.PlainText;

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last edit timestamp, when edited.</summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>Users mentioned by this entry.</summary>
    public List<TmCommentMention> Mentions { get; set; } = [];

    /// <summary>Reactions applied to this entry.</summary>
    public List<TmCommentReaction> Reactions { get; set; } = [];

    /// <summary>Whether the current viewer may edit this entry.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Whether the current viewer may delete this entry.</summary>
    public bool CanDelete { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Returns true when the required identity and body fields are populated.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(Id)
        && !string.IsNullOrWhiteSpace(Body)
        && Author.IsValid;
}
