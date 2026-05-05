using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Abstractions.Interfaces;

/// <summary>
/// Contract for providing data to <see cref="Components.Files.TmDocumentManager{TMetadata}"/>.
/// All operations use <paramref name="itemId"/> as the stable primary key instead of path.
/// </summary>
public interface IDocumentManagerDataProvider<TMetadata> where TMetadata : class
{
    /// <summary>Retrieves the contents of a folder.</summary>
    Task<IReadOnlyList<DocumentManagerItem<TMetadata>>> GetFolderContentsAsync(
        string? folderPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves the folder tree structure for the sidebar.</summary>
    Task<IReadOnlyList<DocumentManagerItem<TMetadata>>> GetFolderTreeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves full details (including metadata) for a single item.</summary>
    Task<DocumentManagerItem<TMetadata>> GetItemDetailAsync(
        string itemId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a new folder with optional metadata.</summary>
    Task<DocumentManagerItem<TMetadata>> CreateFolderAsync(
        string parentPath,
        string folderName,
        TMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Renames an item by its stable Id.</summary>
    Task<DocumentManagerItem<TMetadata>> RenameAsync(
        string itemId,
        string newName,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes items by their stable Ids.</summary>
    Task DeleteAsync(
        IReadOnlyList<string> itemIds,
        CancellationToken cancellationToken = default);

    /// <summary>Uploads files to the specified folder.</summary>
    Task<IReadOnlyList<DocumentManagerItem<TMetadata>>> UploadAsync(
        string folderPath,
        IReadOnlyList<FileUploadInfo> files,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a download stream for a file by its Id.</summary>
    Task<Stream> DownloadAsync(
        string fileId,
        CancellationToken cancellationToken = default);

    /// <summary>Updates metadata for an item.</summary>
    Task<DocumentManagerItem<TMetadata>> UpdateMetadataAsync(
        string itemId,
        TMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>Moves an item to a different folder.</summary>
    Task<DocumentManagerItem<TMetadata>> MoveAsync(
        string itemId,
        string targetFolderPath,
        CancellationToken cancellationToken = default);

    /// <summary>Copies an item to a different folder.</summary>
    Task<DocumentManagerItem<TMetadata>> CopyAsync(
        string itemId,
        string targetFolderPath,
        CancellationToken cancellationToken = default);
}
