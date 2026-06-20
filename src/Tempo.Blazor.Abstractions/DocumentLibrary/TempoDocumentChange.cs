namespace Tempo.Blazor.DocumentLibrary;

/// <summary>
/// Notification that a stored document changed, so that open editors and embedded blocks
/// can refresh (or, on delete, degrade) without polling.
/// </summary>
public sealed class TempoDocumentChange
{
    /// <summary>Kind of the document that changed.</summary>
    public required TempoDocumentKind Kind { get; set; }

    /// <summary>Identifier of the document that changed.</summary>
    public required Guid DocumentId { get; set; }

    /// <summary>What happened to the document.</summary>
    public TempoDocumentChangeType ChangeType { get; set; } = TempoDocumentChangeType.Saved;

    /// <summary>The document's new last-modified timestamp (after the change).</summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Optional identifier of who/what produced the change (user, session, or "mcp"),
    /// so a subscriber can ignore echoes of its own writes.
    /// </summary>
    public string? Origin { get; set; }
}
