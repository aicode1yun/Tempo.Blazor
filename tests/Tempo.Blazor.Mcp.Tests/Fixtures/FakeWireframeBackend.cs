using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Tests.Fixtures;

/// <summary>
/// In-memory backend implementing both the library and the wireframe document provider, so the
/// MCP tools can be exercised end to end without a server.
/// </summary>
public sealed class FakeWireframeBackend : ITempoDocumentLibraryProvider, IWireframeDocumentProvider
{
    private sealed class Entry
    {
        public required WireframeDocument Document { get; set; }
        public required string Name { get; set; }
        public required string FolderPath { get; set; }
        public DateTime CreatedAt { get; init; }
        public DateTime ModifiedAt { get; set; }
    }

    private readonly Dictionary<Guid, Entry> _docs = new();
    private readonly HashSet<string> _folders = new() { "/" };

    /// <summary>Last scopeAppId received by <see cref="CreateWireframeDocumentAsync"/> (for asserting MCP forwarding).</summary>
    public string? LastCreateScopeAppId { get; private set; }

    public DocumentLibraryCapabilities Capabilities => DocumentLibraryCapabilities.All;

    // ── Seeding helper ──────────────────────────────────────────────────────────

    public Guid Add(string name, string folderPath, WireframeDocument? document = null)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        _docs[id] = new Entry
        {
            Document = document ?? Build(name),
            Name = name,
            FolderPath = folderPath,
            CreatedAt = now,
            ModifiedAt = now
        };
        _folders.Add(folderPath);
        return id;
    }

    private static WireframeDocument Build(string title)
    {
        var doc = new WireframeDocument { Title = title };
        doc.EnsureActivePage();
        return doc;
    }

    // ── IWireframeDocumentProvider ───────────────────────────────────────────────

    public Task<(Guid Id, WireframeDocument Document)> CreateWireframeDocumentAsync(string title, string? scopeAppId = null)
    {
        LastCreateScopeAppId = scopeAppId;
        var doc = Build(string.IsNullOrWhiteSpace(title) ? "Untitled wireframe" : title);
        var id = Add(doc.Title, "/", doc);
        return Task.FromResult((id, doc));
    }

    public Task<WireframeDocument?> GetWireframeDocumentAsync(Guid documentId)
        => Task.FromResult(_docs.TryGetValue(documentId, out var e) ? e.Document : null);

    public Task<WireframeDocument> SaveWireframeDocumentAsync(Guid documentId, WireframeDocument document)
    {
        if (_docs.TryGetValue(documentId, out var e))
        {
            e.Document = document;
            e.Name = document.Title;
            e.ModifiedAt = DateTime.UtcNow;
        }
        return Task.FromResult(document);
    }

    // ── ITempoDocumentLibraryProvider ────────────────────────────────────────────

    public Task<DocumentLibraryFolder> GetFolderTreeAsync(TempoDocumentKind kind, CancellationToken ct = default)
        => Task.FromResult(new DocumentLibraryFolder { Path = "/", Name = "/" });

    /// <summary>Last scopeAppId received by <see cref="BrowseAsync"/> (for asserting MCP list forwarding).</summary>
    public string? LastBrowseScopeAppId { get; private set; }

    public Task<DocumentLibraryPage> BrowseAsync(DocumentLibraryQuery query, CancellationToken ct = default)
    {
        LastBrowseScopeAppId = query.ScopeAppId;
        IEnumerable<KeyValuePair<Guid, Entry>> docs = _docs;
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            docs = docs.Where(d => d.Value.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
        }
        else if (!string.IsNullOrEmpty(query.FolderPath))
        {
            docs = docs.Where(d => d.Value.FolderPath == query.FolderPath);
        }

        var matched = docs.ToList();
        var items = matched
            .OrderBy(d => d.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(d => ToEntry(d.Key, d.Value))
            .ToList();

        return Task.FromResult(new DocumentLibraryPage { Items = items, TotalCount = matched.Count });
    }

    public Task<DocumentLibraryEntry?> GetEntryAsync(TempoDocumentKind kind, Guid documentId, CancellationToken ct = default)
        => Task.FromResult(_docs.TryGetValue(documentId, out var e) ? ToEntry(documentId, e) : null);

    private static DocumentLibraryEntry ToEntry(Guid id, Entry e) => new()
    {
        Id = id,
        Name = e.Name,
        Kind = TempoDocumentKind.Wireframe,
        FolderPath = e.FolderPath,
        CreatedAt = e.CreatedAt,
        ModifiedAt = e.ModifiedAt
    };

    public Task<DocumentLibraryFolder> CreateFolderAsync(TempoDocumentKind kind, string parentPath, string name, CancellationToken ct = default)
    {
        var path = parentPath == "/" ? "/" + name : parentPath + "/" + name;
        _folders.Add(path);
        return Task.FromResult(new DocumentLibraryFolder { Path = path, Name = name });
    }

    public Task RenameDocumentAsync(TempoDocumentKind kind, Guid documentId, string newName, CancellationToken ct = default)
    {
        if (_docs.TryGetValue(documentId, out var e)) { e.Name = newName; e.ModifiedAt = DateTime.UtcNow; }
        return Task.CompletedTask;
    }

    public Task RenameFolderAsync(TempoDocumentKind kind, string folderPath, string newName, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteDocumentsAsync(TempoDocumentKind kind, IReadOnlyList<Guid> documentIds, CancellationToken ct = default)
    {
        foreach (var id in documentIds) _docs.Remove(id);
        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(TempoDocumentKind kind, string folderPath, CancellationToken ct = default)
        => Task.CompletedTask;
}
