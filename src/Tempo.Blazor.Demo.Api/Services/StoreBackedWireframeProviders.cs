using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// <see cref="IWireframeDocumentProvider"/> over the in-process <see cref="DocumentLibraryStore"/>,
/// so the MCP tools running inside Demo.Api edit the same documents the UI shows (and saves publish
/// live-change notifications). Regenerates a server-side preview on every write.
/// </summary>
public sealed class StoreWireframeDocumentProvider : IWireframeDocumentProvider
{
    private readonly DocumentLibraryStore _store;

    public StoreWireframeDocumentProvider(DocumentLibraryStore store) => _store = store;

    public Task<(Guid Id, WireframeDocument Document)> CreateWireframeDocumentAsync(string title, string? scopeAppId = null)
    {
        _ = scopeAppId;
        var doc = new WireframeDocument { Title = string.IsNullOrWhiteSpace(title) ? "Untitled wireframe" : title };
        doc.EnsureActivePage();
        var stored = _store.CreateDocument(
            TempoDocumentKind.Wireframe, doc.Title, "/",
            WireframeSerializer.Serialize(doc), WireframeThumbnailRenderer.Render(doc));
        return Task.FromResult((stored.Id, doc));
    }

    public Task<WireframeDocument?> GetWireframeDocumentAsync(Guid documentId)
    {
        var stored = _store.GetDocument(TempoDocumentKind.Wireframe, documentId);
        return Task.FromResult(stored is null ? null : WireframeSerializer.Deserialize(stored.PayloadJson));
    }

    public Task<WireframeDocument> SaveWireframeDocumentAsync(Guid documentId, WireframeDocument document)
    {
        _store.SaveDocument(
            TempoDocumentKind.Wireframe, documentId,
            WireframeSerializer.Serialize(document),
            WireframeThumbnailRenderer.Render(document),
            name: document.Title);
        return Task.FromResult(document);
    }
}

/// <summary>
/// <see cref="ITempoDocumentLibraryProvider"/> over the in-process <see cref="DocumentLibraryStore"/>
/// for the MCP tools.
/// </summary>
public sealed class StoreDocumentLibraryProvider : ITempoDocumentLibraryProvider
{
    private readonly DocumentLibraryStore _store;

    public StoreDocumentLibraryProvider(DocumentLibraryStore store) => _store = store;

    public DocumentLibraryCapabilities Capabilities => DocumentLibraryCapabilities.All;

    public Task<DocumentLibraryFolder> GetFolderTreeAsync(TempoDocumentKind kind, CancellationToken ct = default)
        => Task.FromResult(_store.GetFolderTree(kind));

    public Task<DocumentLibraryPage> BrowseAsync(DocumentLibraryQuery query, CancellationToken ct = default)
        => Task.FromResult(_store.Browse(query));

    public Task<DocumentLibraryEntry?> GetEntryAsync(TempoDocumentKind kind, Guid documentId, CancellationToken ct = default)
    {
        var doc = _store.GetDocument(kind, documentId);
        return Task.FromResult(doc is null ? null : new DocumentLibraryEntry
        {
            Id = doc.Id,
            Name = doc.Name,
            Kind = kind,
            FolderPath = doc.FolderPath,
            CreatedAt = doc.CreatedAt,
            ModifiedAt = doc.ModifiedAt,
            Author = doc.Author,
            PreviewSvg = doc.PreviewSvg
        });
    }

    public Task<DocumentLibraryFolder> CreateFolderAsync(TempoDocumentKind kind, string parentPath, string name, CancellationToken ct = default)
    {
        _store.CreateFolder(kind, parentPath, name);
        var path = parentPath == "/" ? "/" + name : parentPath + "/" + name;
        return Task.FromResult(new DocumentLibraryFolder { Path = path, Name = name });
    }

    public Task RenameDocumentAsync(TempoDocumentKind kind, Guid documentId, string newName, CancellationToken ct = default)
    {
        _store.RenameDocument(kind, documentId, newName);
        return Task.CompletedTask;
    }

    public Task RenameFolderAsync(TempoDocumentKind kind, string folderPath, string newName, CancellationToken ct = default)
    {
        _store.RenameFolder(kind, folderPath, newName);
        return Task.CompletedTask;
    }

    public Task DeleteDocumentsAsync(TempoDocumentKind kind, IReadOnlyList<Guid> documentIds, CancellationToken ct = default)
    {
        _store.DeleteDocuments(kind, documentIds);
        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(TempoDocumentKind kind, string folderPath, CancellationToken ct = default)
    {
        _store.DeleteFolder(kind, folderPath);
        return Task.CompletedTask;
    }
}
