namespace Tempo.Blazor.DocumentLibrary;

/// <summary>
/// Browses and (optionally) manages stored documents produced by the wireframe, diagram
/// and spreadsheet editors, so they can be opened or inserted as blocks elsewhere
/// (e.g. the NotionEditor) and read by MCP tooling.
/// </summary>
/// <remarks>
/// <para>
/// This contract deals only with <em>metadata</em> and organisation. The document payload
/// itself is loaded and saved through the kind-specific providers
/// (<c>IWireframeDocumentProvider</c>, etc.).
/// </para>
/// <para>
/// Management operations (create folder / rename / delete) are optional. An implementation
/// advertises what it supports through <see cref="Capabilities"/>; callers must not invoke
/// an operation whose flag is absent. Implementations may throw
/// <see cref="NotSupportedException"/> if called anyway.
/// </para>
/// </remarks>
public interface ITempoDocumentLibraryProvider
{
    /// <summary>Management operations this provider supports.</summary>
    DocumentLibraryCapabilities Capabilities { get; }

    /// <summary>
    /// Returns the folder tree for the given kind as a single root node. Flat stores
    /// return a root with no children.
    /// </summary>
    Task<DocumentLibraryFolder> GetFolderTreeAsync(
        TempoDocumentKind kind, CancellationToken cancellationToken = default);

    /// <summary>Returns one page of entries matching <paramref name="query"/>.</summary>
    Task<DocumentLibraryPage> BrowseAsync(
        DocumentLibraryQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the metadata entry (including the latest <see cref="DocumentLibraryEntry.PreviewSvg"/>
    /// and <see cref="DocumentLibraryEntry.ModifiedAt"/>) for a single document, or <c>null</c> if it
    /// no longer exists. Used by embedded blocks to render and refresh their preview, and to detect
    /// deletion.
    /// </summary>
    Task<DocumentLibraryEntry?> GetEntryAsync(
        TempoDocumentKind kind, Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new folder under <paramref name="parentPath"/> and returns it.
    /// Requires <see cref="DocumentLibraryCapabilities.CreateFolder"/>.
    /// </summary>
    Task<DocumentLibraryFolder> CreateFolderAsync(
        TempoDocumentKind kind, string parentPath, string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a document. Requires <see cref="DocumentLibraryCapabilities.Rename"/>.
    /// </summary>
    Task RenameDocumentAsync(
        TempoDocumentKind kind, Guid documentId, string newName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a folder (its path changes; descendant documents and folders move with it).
    /// Requires <see cref="DocumentLibraryCapabilities.Rename"/>.
    /// </summary>
    Task RenameFolderAsync(
        TempoDocumentKind kind, string folderPath, string newName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the given documents. Requires <see cref="DocumentLibraryCapabilities.Delete"/>.
    /// Deleting a document does not remove references to it elsewhere (e.g. NotionEditor
    /// blocks): such references degrade to a "not found" state.
    /// </summary>
    Task DeleteDocumentsAsync(
        TempoDocumentKind kind, IReadOnlyList<Guid> documentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a folder and everything it contains, recursively.
    /// Requires <see cref="DocumentLibraryCapabilities.Delete"/>.
    /// </summary>
    Task DeleteFolderAsync(
        TempoDocumentKind kind, string folderPath,
        CancellationToken cancellationToken = default);
}
