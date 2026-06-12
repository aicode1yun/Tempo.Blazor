namespace Tempo.Blazor.DocumentLibrary;

/// <summary>
/// Thrown when a write is attempted with an optimistic-concurrency token
/// (<c>expectedModifiedAt</c>) that no longer matches the stored document — i.e. the
/// document changed since it was read. Carries the current timestamp so the caller can
/// re-read and retry.
/// </summary>
public sealed class TempoDocumentConflictException : Exception
{
    /// <summary>Kind of the document that conflicted.</summary>
    public TempoDocumentKind Kind { get; }

    /// <summary>Identifier of the document that conflicted.</summary>
    public Guid DocumentId { get; }

    /// <summary>The document's current last-modified timestamp at the time of the failed write.</summary>
    public DateTime CurrentModifiedAt { get; }

    /// <summary>Creates a conflict exception for the given document and its current timestamp.</summary>
    public TempoDocumentConflictException(
        TempoDocumentKind kind, Guid documentId, DateTime currentModifiedAt)
        : base($"Document {documentId} ({kind}) was modified by someone else " +
               $"(current modified at {currentModifiedAt:O}).")
    {
        Kind = kind;
        DocumentId = documentId;
        CurrentModifiedAt = currentModifiedAt;
    }
}
