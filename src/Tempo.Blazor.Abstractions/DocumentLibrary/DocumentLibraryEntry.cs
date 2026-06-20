namespace Tempo.Blazor.DocumentLibrary;

/// <summary>
/// Metadata describing one stored document in the library. Carries everything the open
/// dialog needs to list and preview an item without loading its full payload — the payload
/// is fetched lazily through the kind-specific document provider when the user opens it.
/// </summary>
public sealed class DocumentLibraryEntry
{
    /// <summary>Stable identifier of the document.</summary>
    public required Guid Id { get; set; }

    /// <summary>Human-readable document name shown in the listing.</summary>
    public required string Name { get; set; }

    /// <summary>Which editor produced the document.</summary>
    public required TempoDocumentKind Kind { get; set; }

    /// <summary>
    /// Virtual folder path the document lives in (e.g. <c>"/Designs/Mobile"</c>).
    /// Null for flat stores that do not organise documents into folders.
    /// </summary>
    public string? FolderPath { get; set; }

    /// <summary>UTC timestamp of document creation.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp of the last modification. Used both for sorting and as the
    /// optimistic-concurrency token surfaced to MCP write tools.
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>Optional display name of the last author. Null when unknown.</summary>
    public string? Author { get; set; }

    /// <summary>
    /// Optional cached SVG thumbnail rendered from the document, shown in grid view.
    /// Null when the store does not provide previews.
    /// </summary>
    public string? PreviewSvg { get; set; }
}
