using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Mcp.Tests.Fixtures;

public sealed class FakeDocumentEditorProvider : IDocumentEditorProvider
{
    private sealed class Entry
    {
        public required DocumentEditorDocument Document { get; set; }
        public required string Json { get; set; }
        public int Version { get; set; } = 1;
        public List<DocumentVersion> Versions { get; } = [];
    }

    private readonly Dictionary<string, Entry> _docs = new(StringComparer.Ordinal);

    public string Add(DocumentEditorDocument document)
    {
        var json = DocumentEditorJson.Serialize(document);
        _docs[document.DocumentId] = new Entry { Document = document, Json = json };
        return document.DocumentId;
    }

    public string ConcurrencyToken(string documentId) => $"v{_docs[documentId].Version}";

    public void AddVersion(string documentId, DocumentVersion version)
        => _docs[documentId].Versions.Add(version);

    public Task<DocumentEditorLoadResult> LoadAsync(
        string documentId,
        DocumentEditorLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!_docs.TryGetValue(documentId, out var entry))
        {
            return Task.FromResult(DocumentEditorLoadResult.NotFound());
        }

        options ??= new DocumentEditorLoadOptions();
        return Task.FromResult(new DocumentEditorLoadResult
        {
            Found = true,
            Document = options.IncludeDocument ? entry.Document : null,
            JsonSnapshot = options.IncludeJson ? entry.Json : null,
            ConcurrencyToken = $"v{entry.Version}"
        });
    }

    public Task<string?> LoadJsonAsync(string documentId, CancellationToken cancellationToken = default)
        => Task.FromResult(_docs.TryGetValue(documentId, out var entry) ? entry.Json : null);

    public Task<DocumentEditorSaveResult> SaveAsync(
        DocumentEditorSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_docs.TryGetValue(request.DocumentId, out var entry))
        {
            entry = new Entry
            {
                Document = request.Document ?? DocumentEditorDocument.Empty(request.DocumentId),
                Json = request.JsonSnapshot ?? string.Empty
            };
            _docs[request.DocumentId] = entry;
        }

        var currentToken = $"v{entry.Version}";
        var shouldCheck = request.ConcurrencyMode == DocumentEditorConcurrencyMode.Required
            || (request.ConcurrencyMode == DocumentEditorConcurrencyMode.Optional
                && !string.IsNullOrEmpty(request.BaseConcurrencyToken));

        if (shouldCheck && !string.Equals(request.BaseConcurrencyToken, currentToken, StringComparison.Ordinal))
        {
            return Task.FromResult(DocumentEditorSaveResult.ConcurrencyConflict(currentToken));
        }

        var document = request.Document
            ?? DocumentEditorJson.Deserialize(request.JsonSnapshot ?? throw new InvalidOperationException("Missing snapshot."));
        var json = request.NormalizeJson
            ? DocumentEditorJson.Serialize(document)
            : request.JsonSnapshot ?? DocumentEditorJson.Serialize(document);

        entry.Document = document;
        entry.Json = json;
        entry.Version++;

        return Task.FromResult(DocumentEditorSaveResult.Saved(document, json, $"v{entry.Version}"));
    }

    public Task<DocumentVersion> CreateVersionAsync(
        DocumentVersionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_docs.TryGetValue(request.DocumentId, out var entry))
        {
            throw new InvalidOperationException("Document not found.");
        }

        var version = new DocumentVersion
        {
            DocumentId = request.DocumentId,
            Kind = request.Kind,
            Label = request.Label,
            Description = request.Description,
            Author = request.Author,
            Snapshot = new DocumentVersionSnapshot
            {
                DocumentId = request.DocumentId,
                SchemaVersion = entry.Document.SchemaVersion,
                Json = entry.Json
            }
        };
        version.Snapshot.Hash = DocumentVersionHashHelper.ComputeSnapshotHash(version.Snapshot);
        entry.Versions.Add(version);
        return Task.FromResult(version);
    }

    public Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DocumentVersion> versions = _docs.TryGetValue(documentId, out var entry)
            ? entry.Versions
            : [];
        return Task.FromResult(versions);
    }

    public Task<IReadOnlyList<DocumentComment>> GetCommentsAsync(string documentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DocumentComment>>([]);

    public Task<DocumentComment> CreateCommentAsync(string documentId, DocumentComment comment, CancellationToken cancellationToken = default)
        => Task.FromResult(comment);

    public Task<DocumentComment> AddCommentReplyAsync(string documentId, string commentId, DocumentCommentEntry entry, CancellationToken cancellationToken = default)
        => Task.FromResult(new DocumentComment { Id = commentId, Entries = [entry] });

    public Task<DocumentComment> UpdateCommentEntryAsync(string documentId, string commentId, string entryId, string text, DocumentEditorAuthor updatedBy, CancellationToken cancellationToken = default)
        => Task.FromResult(new DocumentComment { Id = commentId });

    public Task<DocumentComment> ResolveCommentAsync(string documentId, string commentId, DocumentEditorAuthor resolvedBy, CancellationToken cancellationToken = default)
        => Task.FromResult(new DocumentComment { Id = commentId, Status = DocumentCommentStatus.Resolved });

    public Task<DocumentComment> ReopenCommentAsync(string documentId, string commentId, DocumentEditorAuthor reopenedBy, CancellationToken cancellationToken = default)
        => Task.FromResult(new DocumentComment { Id = commentId, Status = DocumentCommentStatus.Open });

    public Task DeleteCommentAsync(string documentId, string commentId, DocumentEditorAuthor deletedBy, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
