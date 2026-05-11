using System.Security.Cryptography;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>In-memory offline draft store for tests and demos.</summary>
public class InMemoryDocumentOfflineStore : IDocumentOfflineStore
{
    private readonly Dictionary<string, DocumentOfflineDraft> _drafts = [];

    /// <inheritdoc />
    public Task SaveDraftAsync(DocumentOfflineDraft draft, CancellationToken cancellationToken = default)
    {
        _drafts[draft.Id] = Clone(draft);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<DocumentOfflineDraft?> LoadDraftAsync(string draftId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_drafts.TryGetValue(draftId, out var draft) ? Clone(draft) : null);
    }

    /// <inheritdoc />
    public Task DeleteDraftAsync(string draftId, CancellationToken cancellationToken = default)
    {
        _drafts.Remove(draftId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentOfflineDraft>> ListPendingDraftsAsync(
        string? documentId = null,
        CancellationToken cancellationToken = default)
    {
        var drafts = _drafts.Values
            .Where(draft => draft.State != DocumentOfflineDraftState.Synced)
            .Where(draft => documentId is null || draft.DocumentId == documentId)
            .OrderByDescending(draft => draft.UpdatedAt)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<DocumentOfflineDraft>>(drafts);
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }
}

/// <summary>Simple synchronizer that saves offline snapshots through an editor provider.</summary>
public class InMemoryDocumentSyncProvider : IDocumentSyncProvider
{
    private readonly IDocumentEditorProvider _documentProvider;
    private readonly IDocumentOfflineStore? _offlineStore;

    /// <summary>Creates a synchronizer.</summary>
    public InMemoryDocumentSyncProvider(IDocumentEditorProvider documentProvider, IDocumentOfflineStore? offlineStore = null)
    {
        _documentProvider = documentProvider;
        _offlineStore = offlineStore;
    }

    /// <inheritdoc />
    public async Task<DocumentSyncResult> SyncAsync(
        DocumentSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        var saveResult = await _documentProvider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = request.Draft.DocumentId,
            JsonSnapshot = request.Draft.JsonSnapshot,
            BaseConcurrencyToken = request.Draft.BaseVersionId,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Optional
        }, cancellationToken);

        if (saveResult.Conflict)
        {
            var mergeResult = await TryMergeOperationLogAsync(request, saveResult, cancellationToken);
            if (mergeResult is not null)
            {
                return mergeResult;
            }

            return new DocumentSyncResult
            {
                Success = false,
                SaveResult = saveResult,
                Conflict = new DocumentSyncConflict
                {
                    DocumentId = request.Draft.DocumentId,
                    LocalBaseVersionId = request.Draft.BaseVersionId,
                    ServerVersionId = saveResult.ConcurrencyToken,
                    Reason = "Base version is stale."
                }
            };
        }

        if (saveResult.Success && request.DeleteDraftOnSuccess && _offlineStore is not null)
        {
            await _offlineStore.DeleteDraftAsync(request.Draft.Id, cancellationToken);
        }

        return new DocumentSyncResult
        {
            Success = saveResult.Success,
            SaveResult = saveResult,
            ErrorMessage = saveResult.ErrorMessage
        };
    }

    private async Task<DocumentSyncResult?> TryMergeOperationLogAsync(
        DocumentSyncRequest request,
        DocumentEditorSaveResult conflictResult,
        CancellationToken cancellationToken)
    {
        var batches = request.OperationBatches.Count > 0
            ? request.OperationBatches
            : request.Draft.OperationBatches;

        if (batches.Count == 0)
        {
            return null;
        }

        var loaded = await _documentProvider.LoadAsync(request.Draft.DocumentId, new DocumentEditorLoadOptions
        {
            IncludeDocument = true,
            IncludeJson = false
        }, cancellationToken);

        if (!loaded.Found || loaded.Document is null)
        {
            return null;
        }

        var resolver = new DocumentOperationConflictResolver();
        var applier = new DocumentOperationApplier();
        foreach (var batch in batches)
        {
            var resolvedBatch = new DocumentOperationBatch
            {
                Id = batch.Id,
                DocumentId = batch.DocumentId,
                BaseVersionId = batch.BaseVersionId,
                Operations = resolver.Resolve(batch.Operations).ToList()
            };

            var applyResult = applier.Apply(loaded.Document, resolvedBatch);
            if (!applyResult.IsValid)
            {
                return null;
            }
        }

        var saveResult = await _documentProvider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = request.Draft.DocumentId,
            Document = loaded.Document,
            BaseConcurrencyToken = loaded.ConcurrencyToken,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        }, cancellationToken);

        if (saveResult.Success && request.DeleteDraftOnSuccess && _offlineStore is not null)
        {
            await _offlineStore.DeleteDraftAsync(request.Draft.Id, cancellationToken);
        }

        return new DocumentSyncResult
        {
            Success = saveResult.Success,
            SaveResult = saveResult,
            ErrorMessage = saveResult.ErrorMessage,
            Conflict = saveResult.Success
                ? null
                : new DocumentSyncConflict
                {
                    DocumentId = request.Draft.DocumentId,
                    LocalBaseVersionId = request.Draft.BaseVersionId,
                    ServerVersionId = conflictResult.ConcurrencyToken,
                    Reason = "Operation log merge failed."
                }
        };
    }

    /// <inheritdoc />
    public Task<DocumentSyncResult> SubmitOperationBatchAsync(
        DocumentOperationBatch batch,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DocumentSyncResult
        {
            Success = true
        });
    }
}

/// <summary>In-memory image provider for tests and demos.</summary>
public class InMemoryDocumentImageProvider : IDocumentImageProvider, IDocumentImageUrlResolver
{
    private readonly Dictionary<string, StoredImage> _images = [];

    /// <summary>Provider options.</summary>
    public DocumentImageProviderOptions Options { get; }

    /// <summary>Creates an image provider.</summary>
    public InMemoryDocumentImageProvider(DocumentImageProviderOptions? options = null)
    {
        Options = options ?? new DocumentImageProviderOptions();
    }

    /// <inheritdoc />
    public async Task<DocumentImageUploadResult> UploadAsync(
        DocumentImageUploadRequest request,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        if (!Options.Validation.IsAllowed(request.ContentType, request.SizeBytes))
        {
            return new DocumentImageUploadResult
            {
                Success = false,
                ErrorMessage = "Image content type or size is not allowed."
            };
        }

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        var assetId = string.IsNullOrWhiteSpace(request.LocalAssetId)
            ? Guid.NewGuid().ToString("N")
            : request.LocalAssetId;

        _images[assetId] = new StoredImage(
            request.DocumentId,
            assetId!,
            request.FileName,
            request.ContentType,
            memory.ToArray(),
            true);

        return new DocumentImageUploadResult
        {
            Success = true,
            AssetId = assetId,
            Url = BuildUrl(request.DocumentId, assetId!)
        };
    }

    /// <inheritdoc />
    public Task<DocumentImageResolveResult> ResolveAsync(
        DocumentImageResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_images.TryGetValue(request.AssetId, out var image) || image.DocumentId != request.DocumentId)
        {
            return Task.FromResult(new DocumentImageResolveResult
            {
                Success = false,
                ErrorMessage = "Image asset was not found."
            });
        }

        return Task.FromResult(new DocumentImageResolveResult
        {
            Success = true,
            Url = BuildUrl(request.DocumentId, request.AssetId),
            ContentType = image.ContentType
        });
    }

    /// <inheritdoc />
    public Task DeleteDraftAssetAsync(
        string documentId,
        string assetId,
        CancellationToken cancellationToken = default)
    {
        if (_images.TryGetValue(assetId, out var image) && image.DocumentId == documentId && image.IsDraft)
        {
            _images.Remove(assetId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<DocumentImageCommitResult> CommitAssetsAsync(
        string documentId,
        IReadOnlyList<string> assetIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var assetId in assetIds)
        {
            if (_images.TryGetValue(assetId, out var image) && image.DocumentId == documentId)
            {
                _images[assetId] = image with { IsDraft = false };
            }
        }

        return Task.FromResult(new DocumentImageCommitResult
        {
            Success = true,
            AssetIds = assetIds.ToList()
        });
    }

    /// <inheritdoc />
    public Task<DocumentImageResolveResult> RefreshUrlAsync(
        DocumentImageResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        return ResolveAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ResolveUrlAsync(
        string documentId,
        string assetId,
        CancellationToken cancellationToken = default)
    {
        var result = await ResolveAsync(new DocumentImageResolveRequest
        {
            DocumentId = documentId,
            AssetId = assetId
        }, cancellationToken);

        return result.Success ? result.Url! : string.Empty;
    }

    private static string BuildUrl(string documentId, string assetId)
    {
        return $"memory://document-images/{Uri.EscapeDataString(documentId)}/{Uri.EscapeDataString(assetId)}?ticket={Guid.NewGuid():N}";
    }

    private sealed record StoredImage(
        string DocumentId,
        string AssetId,
        string FileName,
        string ContentType,
        byte[] Bytes,
        bool IsDraft);
}

/// <summary>In-memory rendition provider for tests and demos.</summary>
public class InMemoryDocumentRenditionProvider : IDocumentRenditionProvider
{
    private readonly IDocumentVersionProvider _versionProvider;
    private readonly IDocumentAuditSink? _auditSink;
    private readonly Dictionary<string, DocumentRendition> _renditions = [];

    /// <summary>Creates a rendition provider.</summary>
    public InMemoryDocumentRenditionProvider(IDocumentVersionProvider versionProvider, IDocumentAuditSink? auditSink = null)
    {
        _versionProvider = versionProvider;
        _auditSink = auditSink;
    }

    /// <inheritdoc />
    public async Task<DocumentRenditionResult> CreateRenditionAsync(
        DocumentRenditionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentVersionId))
        {
            return new DocumentRenditionResult
            {
                Success = false,
                ErrorMessage = "A rendition can only be created from a saved document version."
            };
        }

        var versions = await _versionProvider.GetVersionsAsync(request.DocumentId, cancellationToken);
        var version = versions.FirstOrDefault(item => item.Id == request.DocumentVersionId);
        if (version is null)
        {
            return new DocumentRenditionResult
            {
                Success = false,
                ErrorMessage = "Document version was not found."
            };
        }

        var rendition = new DocumentRendition
        {
            DocumentId = request.DocumentId,
            DocumentVersionId = request.DocumentVersionId,
            Status = DocumentRenditionStatus.Finalized,
            PdfAttachmentId = request.Options.IncludePdfAttachment ? $"pdf-{Guid.NewGuid():N}" : null,
            Pages =
            [
                new DocumentRenditionPage
                {
                    PageNumber = 1,
                    Width = 595.276,
                    Height = 841.89,
                    PreviewImageAssetId = request.Options.IncludePreviewImages ? $"preview-{Guid.NewGuid():N}" : null
                }
            ],
            Anchors = request.Options.IncludeAnchorMap ? BuildAnchorMap(version).ToList() : []
        };

        rendition.Hash.SourceSnapshotHash = version.Snapshot.Hash;
        rendition.Hash.Value = ComputeHash(version.Snapshot.Hash, rendition.Id);
        _renditions[rendition.Id] = Clone(rendition);

        if (_auditSink is not null)
        {
            await _auditSink.RecordAsync(new DocumentEditorAuditEvent
            {
                DocumentId = request.DocumentId,
                Action = DocumentEditorAuditAction.CreateRendition,
                Actor = request.Actor,
                Target = new DocumentEditorAuditTarget { Type = "rendition", Id = rendition.Id }
            }, cancellationToken);
        }

        return new DocumentRenditionResult
        {
            Success = true,
            Rendition = Clone(rendition)
        };
    }

    /// <inheritdoc />
    public Task<DocumentRendition?> GetRenditionAsync(
        string renditionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_renditions.TryGetValue(renditionId, out var rendition) ? Clone(rendition) : null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentRenditionPage>> GetPagesAsync(
        string renditionId,
        CancellationToken cancellationToken = default)
    {
        var pages = _renditions.TryGetValue(renditionId, out var rendition)
            ? rendition.Pages.Select(Clone).ToList()
            : [];

        return Task.FromResult<IReadOnlyList<DocumentRenditionPage>>(pages);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentRenditionAnchor>> GetAnchorMapAsync(
        string renditionId,
        CancellationToken cancellationToken = default)
    {
        var anchors = _renditions.TryGetValue(renditionId, out var rendition)
            ? rendition.Anchors.Select(Clone).ToList()
            : [];

        return Task.FromResult<IReadOnlyList<DocumentRenditionAnchor>>(anchors);
    }

    private static IEnumerable<DocumentRenditionAnchor> BuildAnchorMap(DocumentVersion version)
    {
        var document = DocumentEditorJson.Deserialize(version.Snapshot.Json);
        return new DocumentAnchorMapBuilder().Build(document);
    }

    private static string ComputeHash(string versionHash, string renditionId)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{versionHash}:{renditionId}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }
}
