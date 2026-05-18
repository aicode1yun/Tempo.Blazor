namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Offline draft stored by the host application.</summary>
public class DocumentOfflineDraft
{
    /// <summary>Stable draft id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Base version id used for reconciliation.</summary>
    public string? BaseVersionId { get; set; }

    /// <summary>Raw JSON snapshot.</summary>
    public string JsonSnapshot { get; set; } = string.Empty;

    /// <summary>Pending operation batches.</summary>
    public List<DocumentOperationBatch> OperationBatches { get; set; } = [];

    /// <summary>Serialized JS-owned runtime undo/dirty state used to restore an offline editing session.</summary>
    public string? RuntimeStateJson { get; set; }

    /// <summary>Runtime dirty epoch captured with this draft.</summary>
    public int RuntimeDirtyEpoch { get; set; }

    /// <summary>Runtime undo epoch captured with this draft.</summary>
    public int RuntimeUndoEpoch { get; set; }

    /// <summary>Pending local image assets referenced by the offline draft.</summary>
    public List<DocumentImageAsset> PendingAssets { get; set; } = [];

    /// <summary>Clipboard image payloads that still need a provider upload.</summary>
    public List<DocumentClipboardImage> PendingClipboardImages { get; set; } = [];

    /// <summary>Draft state.</summary>
    public DocumentOfflineDraftState State { get; set; } = DocumentOfflineDraftState.PendingSync;

    /// <summary>Sync status.</summary>
    public DocumentSyncStatus SyncStatus { get; set; } = DocumentSyncStatus.Offline;

    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Offline draft state.</summary>
public enum DocumentOfflineDraftState
{
    /// <summary>Draft has pending local changes.</summary>
    PendingSync,

    /// <summary>Draft is syncing.</summary>
    Syncing,

    /// <summary>Draft was synced.</summary>
    Synced,

    /// <summary>Draft has a conflict.</summary>
    Conflict
}

/// <summary>Document synchronization status.</summary>
public enum DocumentSyncStatus
{
    /// <summary>Offline or not connected.</summary>
    Offline,

    /// <summary>Online and idle.</summary>
    Online,

    /// <summary>Sync is in progress.</summary>
    Syncing,

    /// <summary>Sync requires user or host resolution.</summary>
    Conflict,

    /// <summary>Last sync failed.</summary>
    Failed
}

/// <summary>Synchronization conflict.</summary>
public class DocumentSyncConflict
{
    /// <summary>Stable conflict id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Base version id used by the local draft.</summary>
    public string? LocalBaseVersionId { get; set; }

    /// <summary>Current server version id.</summary>
    public string? ServerVersionId { get; set; }

    /// <summary>Conflict reason.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Selected resolution strategy.</summary>
    public DocumentSyncConflictResolution Resolution { get; set; } = DocumentSyncConflictResolution.Unresolved;
}

/// <summary>Synchronization conflict resolution strategy.</summary>
public enum DocumentSyncConflictResolution
{
    /// <summary>Conflict is unresolved.</summary>
    Unresolved,

    /// <summary>Keep local changes.</summary>
    KeepLocal,

    /// <summary>Use server copy.</summary>
    UseServer,

    /// <summary>Merge local and server changes.</summary>
    Merge
}

/// <summary>Provider-managed image asset.</summary>
public class DocumentImageAsset
{
    /// <summary>Stable asset id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document id.</summary>
    public string? DocumentId { get; set; }

    /// <summary>Source kind.</summary>
    public DocumentImageSource Source { get; set; } = DocumentImageSource.Asset;

    /// <summary>Resolved or original URL.</summary>
    public string? Url { get; set; }

    /// <summary>Content type.</summary>
    public string ContentType { get; set; } = "image/png";

    /// <summary>File name.</summary>
    public string? FileName { get; set; }

    /// <summary>Asset size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Alternative text.</summary>
    public string? AltText { get; set; }

    /// <summary>Caption.</summary>
    public string? Caption { get; set; }

    /// <summary>Image size.</summary>
    public DocumentImageSize ImageSize { get; set; } = new();

    /// <summary>Whether the asset exists only locally until uploaded.</summary>
    public bool IsLocalDraft { get; set; }

    /// <summary>Whether a local draft asset is no longer referenced by the editable document.</summary>
    public bool IsUnusedDraft { get; set; }
}

/// <summary>Image upload request sent to a host/provider.</summary>
public class DocumentImageUploadRequest
{
    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Optional local draft asset id.</summary>
    public string? LocalAssetId { get; set; }

    /// <summary>File name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Content type.</summary>
    public string ContentType { get; set; } = "image/png";

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }
}

/// <summary>Image upload result returned by a host/provider.</summary>
public class DocumentImageUploadResult
{
    /// <summary>Whether upload succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Provider-managed asset id.</summary>
    public string? AssetId { get; set; }

    /// <summary>Resolved URL when available.</summary>
    public string? Url { get; set; }

    /// <summary>Error message when upload failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Image resolve request.</summary>
public class DocumentImageResolveRequest
{
    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Asset id.</summary>
    public string AssetId { get; set; } = string.Empty;
}

/// <summary>Image resolve result.</summary>
public class DocumentImageResolveResult
{
    /// <summary>Whether the asset was resolved.</summary>
    public bool Success { get; set; }

    /// <summary>Resolved URL.</summary>
    public string? Url { get; set; }

    /// <summary>Content type.</summary>
    public string? ContentType { get; set; }

    /// <summary>Error message when resolve failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Clipboard image captured before upload.</summary>
public class DocumentClipboardImage
{
    /// <summary>Local draft asset id.</summary>
    public string LocalAssetId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Content type.</summary>
    public string ContentType { get; set; } = "image/png";

    /// <summary>Binary payload.</summary>
    public byte[] Bytes { get; set; } = [];

    /// <summary>Optional file name.</summary>
    public string? FileName { get; set; }
}

/// <summary>Image validation options.</summary>
public class DocumentImageValidationOptions
{
    /// <summary>Allowed content types.</summary>
    public List<string> AllowedContentTypes { get; set; } = ["image/png", "image/jpeg", "image/webp", "image/gif"];

    /// <summary>Maximum accepted file size in bytes.</summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Returns true when the supplied content type and size are allowed.</summary>
    public bool IsAllowed(string contentType, long sizeBytes)
    {
        return AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)
            && sizeBytes <= MaxFileSizeBytes;
    }
}

/// <summary>Immutable rendition produced from a specific document version.</summary>
public class DocumentRendition
{
    /// <summary>Stable rendition id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Source document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Source document version id.</summary>
    public string DocumentVersionId { get; set; } = string.Empty;

    /// <summary>Rendition status.</summary>
    public DocumentRenditionStatus Status { get; set; } = DocumentRenditionStatus.Draft;

    /// <summary>Stable rendition hash.</summary>
    public DocumentRenditionHash Hash { get; set; } = new();

    /// <summary>Pages in the rendition.</summary>
    public List<DocumentRenditionPage> Pages { get; set; } = [];

    /// <summary>Anchor map for tokens, placeholders, and signing fields.</summary>
    public List<DocumentRenditionAnchor> Anchors { get; set; } = [];

    /// <summary>Optional PDF attachment id.</summary>
    public string? PdfAttachmentId { get; set; }

    /// <summary>Immutable renditions must not be edited in-place.</summary>
    public bool IsImmutable { get; set; } = true;
}

/// <summary>Rendered rendition page.</summary>
public class DocumentRenditionPage
{
    /// <summary>One-based page number.</summary>
    public int PageNumber { get; set; }

    /// <summary>Page width in points.</summary>
    public double Width { get; set; }

    /// <summary>Page height in points.</summary>
    public double Height { get; set; }

    /// <summary>Preview image URL.</summary>
    public string? PreviewImageUrl { get; set; }

    /// <summary>Preview image provider asset id.</summary>
    public string? PreviewImageAssetId { get; set; }
}

/// <summary>Normalized anchor within a rendition page.</summary>
public class DocumentRenditionAnchor
{
    /// <summary>Stable anchor id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Rendition anchor type.</summary>
    public DocumentRenditionAnchorType Type { get; set; } = DocumentRenditionAnchorType.Token;

    /// <summary>Optional key for token or placeholder anchors.</summary>
    public string? Key { get; set; }

    /// <summary>Document region that produced this anchor.</summary>
    public DocumentRenditionAnchorScope Scope { get; set; } = DocumentRenditionAnchorScope.Body;

    /// <summary>Optional source section id.</summary>
    public string? SectionId { get; set; }

    /// <summary>Optional source block id.</summary>
    public string? SourceBlockId { get; set; }

    /// <summary>Optional source table cell id.</summary>
    public string? SourceCellId { get; set; }

    /// <summary>Optional header/footer id when the anchor came from a header or footer.</summary>
    public string? HeaderFooterId { get; set; }

    /// <summary>One-based page number.</summary>
    public int PageNumber { get; set; }

    /// <summary>Normalized X coordinate from 0 to 1.</summary>
    public double X { get; set; }

    /// <summary>Normalized Y coordinate from 0 to 1.</summary>
    public double Y { get; set; }

    /// <summary>Normalized width from 0 to 1.</summary>
    public double Width { get; set; }

    /// <summary>Normalized height from 0 to 1.</summary>
    public double Height { get; set; }

    /// <summary>Column span used when the anchor came from a merged table cell.</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>Row span used when the anchor came from a merged table cell.</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>Optional signing placeholder metadata for generated signing fields.</summary>
    public DocumentSigningPlaceholder? SigningPlaceholder { get; set; }
}

/// <summary>Rendition anchor type.</summary>
public enum DocumentRenditionAnchorType
{
    /// <summary>Token anchor.</summary>
    Token,

    /// <summary>Placeholder anchor.</summary>
    Placeholder,

    /// <summary>Signing field anchor.</summary>
    SigningField,

    /// <summary>Comment anchor.</summary>
    Comment
}

/// <summary>Document region that produced a rendition anchor.</summary>
public enum DocumentRenditionAnchorScope
{
    /// <summary>Main document body.</summary>
    Body,

    /// <summary>Header content.</summary>
    Header,

    /// <summary>Footer content.</summary>
    Footer,

    /// <summary>Footnote content.</summary>
    Footnote,

    /// <summary>Endnote content.</summary>
    Endnote,

    /// <summary>Floating or anchored object content.</summary>
    FloatingObject
}

/// <summary>Rendition hash metadata.</summary>
public class DocumentRenditionHash
{
    /// <summary>Hash algorithm.</summary>
    public string Algorithm { get; set; } = "SHA-256";

    /// <summary>Hash value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Hash of the source document JSON snapshot used to create the rendition.</summary>
    public string SourceSnapshotHash { get; set; } = string.Empty;
}

/// <summary>Rendition lifecycle status.</summary>
public enum DocumentRenditionStatus
{
    /// <summary>Rendition is being prepared.</summary>
    Draft,

    /// <summary>Rendition is finalized.</summary>
    Finalized,

    /// <summary>Rendition generation failed.</summary>
    Failed
}
