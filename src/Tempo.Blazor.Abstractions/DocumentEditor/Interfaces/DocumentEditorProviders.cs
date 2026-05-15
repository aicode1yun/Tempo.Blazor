using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Interfaces;

/// <summary>Primary provider contract used by <c>TmDocumentEditor</c>.</summary>
public interface IDocumentEditorProvider : IDocumentVersionProvider, IDocumentCommentProvider
{
    /// <summary>Loads a document by id.</summary>
    Task<DocumentEditorLoadResult> LoadAsync(
        string documentId,
        DocumentEditorLoadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a raw JSON snapshot by id.</summary>
    Task<string?> LoadJsonAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>Saves a document JSON snapshot.</summary>
    Task<DocumentEditorSaveResult> SaveAsync(
        DocumentEditorSaveRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider contract for document versions.</summary>
public interface IDocumentVersionProvider
{
    /// <summary>Creates a version from the current document snapshot.</summary>
    Task<DocumentVersion> CreateVersionAsync(
        DocumentVersionCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Gets versions for a document.</summary>
    Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(
        string documentId,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider contract for document comments.</summary>
public interface IDocumentCommentProvider
{
    /// <summary>Gets comments for a document.</summary>
    Task<IReadOnlyList<DocumentComment>> GetCommentsAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a comment thread.</summary>
    Task<DocumentComment> CreateCommentAsync(
        string documentId,
        DocumentComment comment,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a reply to an existing comment thread.</summary>
    Task<DocumentComment> AddCommentReplyAsync(
        string documentId,
        string commentId,
        DocumentCommentEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves a comment thread.</summary>
    Task<DocumentComment> ResolveCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor resolvedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Reopens a resolved comment thread.</summary>
    Task<DocumentComment> ReopenCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor reopenedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a comment thread.</summary>
    Task DeleteCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor deletedBy,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider contract for resolving document template token values.</summary>
public interface IDocumentTokenValueProvider
{
    /// <summary>Resolves one or more token values for a document preview or export flow.</summary>
    Task<IReadOnlyDictionary<string, DocumentTokenValue>> ResolveTokenValuesAsync(
        DocumentTokenResolutionContext context,
        IReadOnlyList<TokenRun> tokens,
        CancellationToken cancellationToken = default);
}

/// <summary>Audit sink for host applications that persist editor events.</summary>
public interface IDocumentAuditSink
{
    /// <summary>Records an audit event.</summary>
    Task RecordAsync(
        DocumentEditorAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}

/// <summary>Offline draft storage boundary.</summary>
public interface IDocumentOfflineStore
{
    /// <summary>Saves or replaces an offline draft.</summary>
    Task SaveDraftAsync(
        DocumentOfflineDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>Loads an offline draft by id.</summary>
    Task<DocumentOfflineDraft?> LoadDraftAsync(
        string draftId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an offline draft by id.</summary>
    Task DeleteDraftAsync(
        string draftId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists pending drafts, optionally for a single document.</summary>
    Task<IReadOnlyList<DocumentOfflineDraft>> ListPendingDraftsAsync(
        string? documentId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider boundary for synchronizing offline changes.</summary>
public interface IDocumentSyncProvider
{
    /// <summary>Synchronizes an offline draft.</summary>
    Task<DocumentSyncResult> SyncAsync(
        DocumentSyncRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Submits a batch of document operations.</summary>
    Task<DocumentSyncResult> SubmitOperationBatchAsync(
        DocumentOperationBatch batch,
        CancellationToken cancellationToken = default);
}

/// <summary>Document image provider boundary.</summary>
public interface IDocumentImageProvider
{
    /// <summary>Uploads an image stream and returns a provider-managed asset.</summary>
    Task<DocumentImageUploadResult> UploadAsync(
        DocumentImageUploadRequest request,
        Stream stream,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves an asset id to a URL or access ticket.</summary>
    Task<DocumentImageResolveResult> ResolveAsync(
        DocumentImageResolveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an unused draft asset.</summary>
    Task DeleteDraftAssetAsync(
        string documentId,
        string assetId,
        CancellationToken cancellationToken = default);

    /// <summary>Commits assets that are now referenced by a saved document.</summary>
    Task<DocumentImageCommitResult> CommitAssetsAsync(
        string documentId,
        IReadOnlyList<string> assetIds,
        CancellationToken cancellationToken = default);

    /// <summary>Refreshes a short-lived URL for an asset.</summary>
    Task<DocumentImageResolveResult> RefreshUrlAsync(
        DocumentImageResolveRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Dedicated URL resolver for document image assets.</summary>
public interface IDocumentImageUrlResolver
{
    /// <summary>Resolves a document image asset to a display URL.</summary>
    Task<string> ResolveUrlAsync(
        string documentId,
        string assetId,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider boundary for immutable document renditions.</summary>
public interface IDocumentRenditionProvider
{
    /// <summary>Creates an immutable rendition from a versioned document.</summary>
    Task<DocumentRenditionResult> CreateRenditionAsync(
        DocumentRenditionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a rendition by id.</summary>
    Task<DocumentRendition?> GetRenditionAsync(
        string renditionId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets pages for a rendition.</summary>
    Task<IReadOnlyList<DocumentRenditionPage>> GetPagesAsync(
        string renditionId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the anchor map for a rendition.</summary>
    Task<IReadOnlyList<DocumentRenditionAnchor>> GetAnchorMapAsync(
        string renditionId,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider boundary for host-managed PDF export.</summary>
public interface IDocumentPdfExportProvider
{
    /// <summary>Exports a document snapshot to PDF bytes.</summary>
    Task<DocumentPdfExportResult> ExportPdfAsync(
        DocumentPdfExportRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider boundary for comparing arbitrary document snapshots outside version history.</summary>
public interface IDocumentComparisonProvider
{
    /// <summary>Compares two document sources.</summary>
    Task<DocumentCompareResult> CompareAsync(
        DocumentCompareRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider boundary for host-managed external document format import and export.</summary>
public interface IDocumentFormatProvider
{
    /// <summary>Gets the external formats and operations supported by the provider.</summary>
    Task<IReadOnlyList<DocumentFormatProviderCapability>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Imports an external document package into the editor document model.</summary>
    Task<DocumentFormatImportProviderResult> ImportAsync(
        DocumentFormatImportProviderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Exports an editor document snapshot to an external document package.</summary>
    Task<DocumentFormatExportProviderResult> ExportAsync(
        DocumentFormatExportProviderRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider boundary for review suggestions kept outside the core document model.</summary>
public interface IDocumentSuggestionProvider
{
    /// <summary>Lists suggestions for a document.</summary>
    Task<IReadOnlyList<DocumentSuggestion>> GetSuggestionsAsync(
        DocumentSuggestionQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a suggestion.</summary>
    Task<DocumentSuggestion> CreateSuggestionAsync(
        DocumentSuggestion suggestion,
        CancellationToken cancellationToken = default);

    /// <summary>Accepts or rejects a suggestion.</summary>
    Task<DocumentSuggestion> ReviewSuggestionAsync(
        DocumentSuggestionReviewRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider boundary for realtime document collaboration.</summary>
public interface IDocumentCollaborationProvider
{
    /// <summary>Joins a collaboration session.</summary>
    Task<DocumentCollaborationSession> JoinAsync(
        DocumentCollaborationJoinRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Leaves a collaboration session.</summary>
    Task LeaveAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Broadcasts an operation batch to the collaboration stream.</summary>
    Task<DocumentCollaborationOperationBatch> BroadcastOperationBatchAsync(
        string sessionId,
        DocumentOperationBatch batch,
        CancellationToken cancellationToken = default);

    /// <summary>Gets operation batches after a server sequence.</summary>
    Task<IReadOnlyList<DocumentCollaborationOperationBatch>> GetOperationBatchesAsync(
        string documentId,
        long afterSequence,
        CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a cursor position.</summary>
    Task BroadcastCursorAsync(
        DocumentCollaborationCursor cursor,
        CancellationToken cancellationToken = default);

    /// <summary>Gets currently known cursors for a document.</summary>
    Task<IReadOnlyList<DocumentCollaborationCursor>> GetCursorsAsync(
        string documentId,
        CancellationToken cancellationToken = default);
}
