namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Shared comment thread attached to an entity.</summary>
public sealed class TmCommentThread
{
    /// <summary>Stable thread identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Entity this thread belongs to.</summary>
    public TmEntityRef EntityRef { get; set; } = new();

    /// <summary>Optional structured anchor within the entity.</summary>
    public TmCommentAnchor? Anchor { get; set; }

    /// <summary>Thread status.</summary>
    public TmCommentThreadStatus Status { get; set; } = TmCommentThreadStatus.Open;

    /// <summary>Optional visibility scope.</summary>
    public TmCommentVisibility? Visibility { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Timestamp when the thread was resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>User that resolved the thread, when available.</summary>
    public TmUserRef? ResolvedBy { get; set; }

    /// <summary>User ids that have read this thread.</summary>
    public List<string> ReadByUserIds { get; set; } = [];

    /// <summary>User ids subscribed to this thread.</summary>
    public List<string> SubscribedUserIds { get; set; } = [];

    /// <summary>External id from an imported or host-owned system.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Source format when imported from an external system.</summary>
    public string? SourceFormat { get; set; }

    /// <summary>Entries in this thread.</summary>
    public List<TmCommentEntry> Entries { get; set; } = [];

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Returns true when the thread contains a valid entity reference and identifier.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(Id)
        && EntityRef.IsValid
        && (Anchor is null || Anchor.IsValid());
}
