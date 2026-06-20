using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Abstractions.Interfaces;

/// <summary>
/// Contract for providing data to the <see cref="TmFileManager"/> component.
/// Implementations handle file system operations, cloud storage APIs, or virtual file systems.
/// </summary>
public interface IFileManagerDataProvider
{
    /// <summary>
    /// Retrieves the contents of a folder.
    /// </summary>
    /// <param name="folderPath">Path of the folder to list. Null or empty for root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of files and folders in the requested directory.</returns>
    Task<IReadOnlyList<FileManagerItem>> GetFolderContentsAsync(string? folderPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the folder tree structure for the sidebar.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hierarchical list of folders.</returns>
    Task<IReadOnlyList<FileManagerItem>> GetFolderTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    /// <param name="parentPath">Path of the parent folder.</param>
    /// <param name="folderName">Name of the new folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created folder item.</returns>
    Task<FileManagerItem> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a file or folder.
    /// </summary>
    /// <param name="itemPath">Path of the item to rename.</param>
    /// <param name="newName">New name (not full path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The renamed item.</returns>
    Task<FileManagerItem> RenameAsync(string itemPath, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes files or folders.
    /// </summary>
    /// <param name="itemPaths">Paths of items to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(IReadOnlyList<string> itemPaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads files to the specified folder.
    /// </summary>
    /// <param name="folderPath">Target folder path.</param>
    /// <param name="files">Files to upload, including metadata and stream.</param>
    /// <param name="progress">Optional progress callback (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of uploaded items.</returns>
    Task<IReadOnlyList<FileManagerItem>> UploadAsync(string folderPath, IReadOnlyList<FileUploadInfo> files, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a download stream for a file.
    /// </summary>
    /// <param name="filePath">Path of the file to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream containing the file data.</returns>
    Task<Stream> DownloadAsync(string filePath, CancellationToken cancellationToken = default);
}
