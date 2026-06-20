using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Models;

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
    /// <param name="name">When provided, a single entity with this name is created and all files are stored as its attachments.</param>
    Task<IReadOnlyList<DocumentManagerItem<TMetadata>>> UploadAsync(
        string folderPath,
        IReadOnlyList<FileUploadInfo> files,
        TMetadata? metadata = null,
        string? name = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads the primary (first) attachment stream for a file by its Id.</summary>
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

    /// <summary>
    /// Uploads a single 256 KB chunk. Returns an upload session ID (null on first chunk
    /// response if the provider uses an implicit session). <see cref="FileChunkData.IsLast"/>
    /// signals the final chunk.
    /// </summary>
    Task<string?> UploadChunkAsync(FileChunkData chunk, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all attachments for a specific item.</summary>
    Task<IReadOnlyList<FileAttachment>> GetAttachmentsAsync(
        string itemId, CancellationToken cancellationToken = default);

    /// <summary>Adds new file attachments to an existing item.</summary>
    Task<IReadOnlyList<FileAttachment>> AddAttachmentsAsync(
        string itemId, IReadOnlyList<FileUploadInfo> files, CancellationToken cancellationToken = default);

    /// <summary>Removes a single attachment from an item.</summary>
    Task RemoveAttachmentAsync(
        string itemId, string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>Downloads a specific attachment as a stream.</summary>
    Task<Stream> DownloadAttachmentAsync(
        string itemId, string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>Downloads all attachments for an item as a single ZIP archive stream.</summary>
    Task<Stream> DownloadAllAttachmentsAsync(
        string itemId, CancellationToken cancellationToken = default);
}
