namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>First-class marker type used by comments, revisions, search, remote cursors, and protected regions.</summary>
public enum DocumentMarkerType
{
    /// <summary>Search result marker.</summary>
    Search,

    /// <summary>Active search result marker.</summary>
    SearchActive,

    /// <summary>Comment anchor marker.</summary>
    Comment,

    /// <summary>Insertion revision marker.</summary>
    RevisionInsertion,

    /// <summary>Deletion revision marker.</summary>
    RevisionDeletion,

    /// <summary>Formatting revision marker.</summary>
    RevisionFormatting,

    /// <summary>Remote collaborator selection or caret marker.</summary>
    RemoteSelection,

    /// <summary>Protected editable region marker.</summary>
    RestrictedRegion,

    /// <summary>Mention query marker.</summary>
    MentionQuery,

    /// <summary>Token query marker.</summary>
    TokenQuery
}

/// <summary>Origin of a marker.</summary>
public enum DocumentMarkerSource
{
    /// <summary>Marker was created by the local editor runtime.</summary>
    LocalRuntime,

    /// <summary>Marker was created by the host application.</summary>
    Host,

    /// <summary>Marker was imported from a persisted document model.</summary>
    Document,

    /// <summary>Marker represents remote collaboration state.</summary>
    Collaboration,

    /// <summary>Marker is a transient search or query result.</summary>
    Transient
}

/// <summary>Range covered by a first-class document marker.</summary>
public sealed record DocumentMarkerRange
{
    /// <summary>Identifier of the block where the marker starts.</summary>
    public string StartBlockId { get; init; } = string.Empty;

    /// <summary>Optional inline identifier where the marker starts.</summary>
    public string? StartInlineId { get; init; }

    /// <summary>Character offset inside the start block or inline.</summary>
    public int StartOffset { get; init; }

    /// <summary>Identifier of the block where the marker ends. Defaults to <see cref="StartBlockId"/>.</summary>
    public string EndBlockId { get; init; } = string.Empty;

    /// <summary>Optional inline identifier where the marker ends.</summary>
    public string? EndInlineId { get; init; }

    /// <summary>Character offset inside the end block or inline.</summary>
    public int EndOffset { get; init; }

    /// <summary>Whether the range is collapsed to a caret.</summary>
    public bool IsCollapsed => StartBlockId == EffectiveEndBlockId && StartOffset == EndOffset;

    /// <summary>Effective end block id.</summary>
    public string EffectiveEndBlockId => string.IsNullOrWhiteSpace(EndBlockId) ? StartBlockId : EndBlockId;

    /// <summary>Creates a range inside one block.</summary>
    public static DocumentMarkerRange InBlock(string blockId, int startOffset, int endOffset, string? inlineId = null) =>
        new()
        {
            StartBlockId = blockId,
            EndBlockId = blockId,
            StartInlineId = inlineId,
            EndInlineId = inlineId,
            StartOffset = Math.Max(0, startOffset),
            EndOffset = Math.Max(0, endOffset)
        };

    /// <summary>Returns whether this range touches the block.</summary>
    public bool TouchesBlock(string blockId) =>
        string.Equals(StartBlockId, blockId, StringComparison.Ordinal)
        || string.Equals(EffectiveEndBlockId, blockId, StringComparison.Ordinal);

    /// <summary>Returns whether this range overlaps another range in the same block.</summary>
    public bool Overlaps(DocumentMarkerRange other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!string.Equals(StartBlockId, EffectiveEndBlockId, StringComparison.Ordinal)
            || !string.Equals(other.StartBlockId, other.EffectiveEndBlockId, StringComparison.Ordinal)
            || !string.Equals(StartBlockId, other.StartBlockId, StringComparison.Ordinal))
        {
            return TouchesBlock(other.StartBlockId) || other.TouchesBlock(StartBlockId);
        }

        return StartOffset < other.EndOffset && other.StartOffset < EndOffset;
    }
}

/// <summary>First-class marker used by editor UI layers and persistent document data.</summary>
public sealed record DocumentMarker
{
    /// <summary>Stable marker identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Marker type.</summary>
    public DocumentMarkerType Type { get; init; }

    /// <summary>Marker range.</summary>
    public DocumentMarkerRange Range { get; init; } = new();

    /// <summary>Whether this marker affects persisted document data.</summary>
    public bool AffectsData { get; init; }

    /// <summary>Marker priority. Higher values render and query before lower values.</summary>
    public int Priority { get; init; }

    /// <summary>Marker source.</summary>
    public DocumentMarkerSource Source { get; init; } = DocumentMarkerSource.LocalRuntime;

    /// <summary>Optional related entity id, for example comment id, revision id, or remote session id.</summary>
    public string? TargetId { get; init; }

    /// <summary>Optional display label.</summary>
    public string? Label { get; init; }

    /// <summary>Arbitrary serializable metadata.</summary>
    public Dictionary<string, string> Metadata { get; init; } = [];
}

/// <summary>Presentation mapping for a document marker.</summary>
public sealed record DocumentMarkerPresentation
{
    /// <summary>CSS class used for the marker decoration.</summary>
    public string CssClass { get; init; } = string.Empty;

    /// <summary>Optional active CSS class.</summary>
    public string? ActiveCssClass { get; init; }

    /// <summary>Optional data-testid for rendered marker decorations.</summary>
    public string? TestId { get; init; }

    /// <summary>Whether this marker should be rendered above overlapping markers with the same priority.</summary>
    public bool IsInteractive { get; init; }

    /// <summary>Creates a presentation mapping for a marker type.</summary>
    public static DocumentMarkerPresentation For(DocumentMarkerType type) =>
        type switch
        {
            DocumentMarkerType.Search => new() { CssClass = "tm-wysiwyg-marker--search", ActiveCssClass = "tm-wysiwyg-marker--search-active", TestId = "document-search-marker" },
            DocumentMarkerType.SearchActive => new() { CssClass = "tm-wysiwyg-marker--search tm-wysiwyg-marker--search-active", TestId = "document-search-marker-active", IsInteractive = true },
            DocumentMarkerType.Comment => new() { CssClass = "tm-wysiwyg-marker--comment", ActiveCssClass = "tm-wysiwyg-marker--comment-active", TestId = "document-comment-marker", IsInteractive = true },
            DocumentMarkerType.RevisionInsertion => new() { CssClass = "tm-wysiwyg-marker--revision-insert", TestId = "document-revision-marker", IsInteractive = true },
            DocumentMarkerType.RevisionDeletion => new() { CssClass = "tm-wysiwyg-marker--revision-delete", TestId = "document-revision-marker", IsInteractive = true },
            DocumentMarkerType.RevisionFormatting => new() { CssClass = "tm-wysiwyg-marker--revision-format", TestId = "document-revision-marker", IsInteractive = true },
            DocumentMarkerType.RemoteSelection => new() { CssClass = "tm-wysiwyg-marker--remote-selection", TestId = "document-remote-selection-marker" },
            DocumentMarkerType.RestrictedRegion => new() { CssClass = "tm-wysiwyg-marker--restricted-region", TestId = "document-restricted-region-marker" },
            DocumentMarkerType.MentionQuery => new() { CssClass = "tm-wysiwyg-marker--mention-query", TestId = "document-mention-query-marker" },
            DocumentMarkerType.TokenQuery => new() { CssClass = "tm-wysiwyg-marker--token-query", TestId = "document-token-query-marker" },
            _ => new() { CssClass = "tm-wysiwyg-marker" }
        };
}

/// <summary>In-memory first-class marker store independent of DOM rendering.</summary>
public sealed class DocumentMarkerStore
{
    private readonly Dictionary<string, DocumentMarker> _markers = new(StringComparer.Ordinal);

    /// <summary>Adds or replaces a marker.</summary>
    public DocumentMarker Add(DocumentMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        if (string.IsNullOrWhiteSpace(marker.Id))
        {
            throw new ArgumentException("Marker id cannot be empty.", nameof(marker));
        }

        _markers[marker.Id] = marker;
        return marker;
    }

    /// <summary>Removes a marker by id.</summary>
    public bool Remove(string markerId) =>
        !string.IsNullOrWhiteSpace(markerId) && _markers.Remove(markerId);

    /// <summary>Updates a marker range.</summary>
    public bool UpdateRange(string markerId, DocumentMarkerRange range)
    {
        if (!_markers.TryGetValue(markerId, out var marker))
        {
            return false;
        }

        _markers[markerId] = marker with { Range = range };
        return true;
    }

    /// <summary>Gets all markers sorted by priority.</summary>
    public IReadOnlyList<DocumentMarker> GetAll() =>
        SortMarkers(_markers.Values);

    /// <summary>Gets markers touching a block.</summary>
    public IReadOnlyList<DocumentMarker> GetByBlock(string blockId) =>
        SortMarkers(_markers.Values.Where(marker => marker.Range.TouchesBlock(blockId)));

    /// <summary>Gets markers of a given type.</summary>
    public IReadOnlyList<DocumentMarker> GetByType(DocumentMarkerType type) =>
        SortMarkers(_markers.Values.Where(marker => marker.Type == type));

    /// <summary>Gets markers whose range overlaps the provided range.</summary>
    public IReadOnlyList<DocumentMarker> GetOverlapping(DocumentMarkerRange range) =>
        SortMarkers(_markers.Values.Where(marker => marker.Range.Overlaps(range)));

    /// <summary>Gets markers that should be written to persistent document data.</summary>
    public IReadOnlyList<DocumentMarker> GetPersistentMarkers() =>
        SortMarkers(_markers.Values.Where(marker => marker.AffectsData));

    private static IReadOnlyList<DocumentMarker> SortMarkers(IEnumerable<DocumentMarker> markers) =>
        markers
            .OrderByDescending(marker => marker.Priority)
            .ThenBy(marker => marker.Id, StringComparer.Ordinal)
            .ToArray();
}

/// <summary>An editable region within an otherwise-protected document.</summary>
public sealed record DocumentRestrictedMarker
{
    /// <summary>Stable marker identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Identifier of the block where the editable region starts.</summary>
    public string StartBlockId { get; init; } = string.Empty;

    /// <summary>Character offset within the start block (inclusive).</summary>
    public int StartOffset { get; init; }

    /// <summary>Identifier of the block where the editable region ends.</summary>
    public string EndBlockId { get; init; } = string.Empty;

    /// <summary>Character offset within the end block (exclusive).</summary>
    public int EndOffset { get; init; }

    /// <summary>Optional label shown in the editor UI for this editable region.</summary>
    public string? Label { get; init; }
}
