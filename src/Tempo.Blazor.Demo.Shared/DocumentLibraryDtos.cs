using Tempo.Blazor.DocumentLibrary;

namespace Tempo.Blazor.Demo.Shared;

/// <summary>Metadata returned for a stored document.</summary>
public sealed class DocumentLibraryMetadataDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TempoDocumentKind Kind { get; set; }
    public string FolderPath { get; set; } = "/";
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string? Author { get; set; }
    public string? PreviewSvg { get; set; }
}

/// <summary>Request to create a new document in the library.</summary>
public sealed class DocumentLibraryCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string FolderPath { get; set; } = "/";
    public string PayloadJson { get; set; } = "{}";
    public string? PreviewSvg { get; set; }
    public string? Author { get; set; }
}

/// <summary>Request to save an existing document's payload, with optimistic concurrency.</summary>
public sealed class DocumentLibrarySaveRequest
{
    public string PayloadJson { get; set; } = "{}";
    public string? PreviewSvg { get; set; }
    public DateTime? ExpectedModifiedAt { get; set; }
    public string? Name { get; set; }
}

/// <summary>Request to create a folder.</summary>
public sealed class DocumentLibraryCreateFolderRequest
{
    public string ParentPath { get; set; } = "/";
    public string Name { get; set; } = string.Empty;
}

/// <summary>Request to rename a folder.</summary>
public sealed class DocumentLibraryRenameFolderRequest
{
    public string FolderPath { get; set; } = "/";
    public string NewName { get; set; } = string.Empty;
}

/// <summary>Request to rename a document.</summary>
public sealed class DocumentLibraryRenameDocumentRequest
{
    public string NewName { get; set; } = string.Empty;
}

/// <summary>Request to delete documents by id.</summary>
public sealed class DocumentLibraryDeleteDocumentsRequest
{
    public List<Guid> Ids { get; set; } = [];
}
