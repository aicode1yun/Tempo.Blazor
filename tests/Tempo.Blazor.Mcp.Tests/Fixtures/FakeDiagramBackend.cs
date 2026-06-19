using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Tests.Fixtures;

/// <summary>In-memory backend for diagram MCP tests.</summary>
public sealed class FakeDiagramBackend : ITempoDocumentLibraryProvider, IDiagramDocumentProvider
{
    private sealed class Entry
    {
        public required DiagramDocument Document { get; set; }
        public required string Name { get; set; }
        public required string FolderPath { get; set; }
        public DateTime CreatedAt { get; init; }
        public DateTime ModifiedAt { get; set; }
    }

    private readonly Dictionary<Guid, Entry> _docs = new();
    private readonly HashSet<string> _folders = new() { "/" };

    public DocumentLibraryCapabilities Capabilities => DocumentLibraryCapabilities.All;

    public Guid Add(string name, string folderPath, DiagramDocument? document = null)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var doc = document ?? Build(id, name);
        doc.Id = id.ToString();
        doc.Title = name;
        doc.ModifiedAt = now;
        _docs[id] = new Entry
        {
            Document = doc,
            Name = name,
            FolderPath = folderPath,
            CreatedAt = now,
            ModifiedAt = now
        };
        _folders.Add(folderPath);
        return id;
    }

    public Task<(Guid Id, DiagramDocument Document)> CreateDiagramDocumentAsync(string title)
    {
        var id = Add(string.IsNullOrWhiteSpace(title) ? "Untitled diagram" : title, "/");
        return Task.FromResult((id, _docs[id].Document));
    }

    public Task<DiagramDocument?> GetDiagramDocumentAsync(Guid documentId)
        => Task.FromResult(_docs.TryGetValue(documentId, out var e) ? e.Document : null);

    public Task<DiagramDocument> SaveDiagramDocumentAsync(Guid documentId, DiagramDocument document)
    {
        if (_docs.TryGetValue(documentId, out var e))
        {
            e.Document = document;
            e.Name = document.Title;
            e.ModifiedAt = DateTime.UtcNow;
            document.Id = documentId.ToString();
            document.ModifiedAt = e.ModifiedAt;
        }

        return Task.FromResult(document);
    }

    public Task<DocumentLibraryFolder> GetFolderTreeAsync(TempoDocumentKind kind, CancellationToken ct = default)
        => Task.FromResult(new DocumentLibraryFolder { Path = "/", Name = "/" });

    public Task<DocumentLibraryPage> BrowseAsync(DocumentLibraryQuery query, CancellationToken ct = default)
    {
        if (query.Kind != TempoDocumentKind.Diagram)
        {
            return Task.FromResult(new DocumentLibraryPage { Items = [], TotalCount = 0 });
        }

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
    {
        if (kind != TempoDocumentKind.Diagram)
        {
            return Task.FromResult<DocumentLibraryEntry?>(null);
        }

        return Task.FromResult(_docs.TryGetValue(documentId, out var e) ? ToEntry(documentId, e) : null);
    }

    public Task<DocumentLibraryFolder> CreateFolderAsync(TempoDocumentKind kind, string parentPath, string name, CancellationToken ct = default)
    {
        var path = parentPath == "/" ? "/" + name : parentPath + "/" + name;
        _folders.Add(path);
        return Task.FromResult(new DocumentLibraryFolder { Path = path, Name = name });
    }

    public Task RenameDocumentAsync(TempoDocumentKind kind, Guid documentId, string newName, CancellationToken ct = default)
    {
        if (kind == TempoDocumentKind.Diagram && _docs.TryGetValue(documentId, out var e))
        {
            e.Name = newName;
            e.Document.Title = newName;
            e.ModifiedAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task RenameFolderAsync(TempoDocumentKind kind, string folderPath, string newName, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteDocumentsAsync(TempoDocumentKind kind, IReadOnlyList<Guid> documentIds, CancellationToken ct = default)
    {
        if (kind == TempoDocumentKind.Diagram)
        {
            foreach (var id in documentIds)
            {
                _docs.Remove(id);
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(TempoDocumentKind kind, string folderPath, CancellationToken ct = default)
        => Task.CompletedTask;

    private static DiagramDocument Build(Guid id, string title)
    {
        var document = new DiagramDocument
        {
            Id = id.ToString(),
            Title = title,
            Pages =
            [
                new DiagramPage { Name = "Page 1" }
            ],
            ActivePageIndex = 0
        };
        document.EnsurePages();
        return document;
    }

    private static DocumentLibraryEntry ToEntry(Guid id, Entry e) => new()
    {
        Id = id,
        Name = e.Name,
        Kind = TempoDocumentKind.Diagram,
        FolderPath = e.FolderPath,
        CreatedAt = e.CreatedAt,
        ModifiedAt = e.ModifiedAt
    };
}
