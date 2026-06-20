using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// In-memory demo data provider for <see cref="TmFileManager"/>.
/// </summary>
public class DemoFileManagerProvider : IFileManagerDataProvider
{
    private readonly List<FileManagerItem> _items;

    public DemoFileManagerProvider()
    {
        _items =
        [
            new() { Id = "1", Name = "Documents", Path = "/Documents", IsDirectory = true },
            new() { Id = "2", Name = "Pictures", Path = "/Pictures", IsDirectory = true },
            new() { Id = "3", Name = "Music", Path = "/Music", IsDirectory = true },
            new() { Id = "4", Name = "Report.pdf", Path = "/Documents/Report.pdf", IsDirectory = false, Size = 1_024_000, Extension = ".pdf" },
            new() { Id = "5", Name = "Budget.xlsx", Path = "/Documents/Budget.xlsx", IsDirectory = false, Size = 512_000, Extension = ".xlsx" },
            new() { Id = "6", Name = "Notes.txt", Path = "/Documents/Notes.txt", IsDirectory = false, Size = 4_096, Extension = ".txt" },
            new() { Id = "7", Name = "Vacation.jpg", Path = "/Pictures/Vacation.jpg", IsDirectory = false, Size = 3_500_000, Extension = ".jpg" },
            new() { Id = "8", Name = "Logo.png", Path = "/Pictures/Logo.png", IsDirectory = false, Size = 120_000, Extension = ".png" },
            new() { Id = "9", Name = "Song.mp3", Path = "/Music/Song.mp3", IsDirectory = false, Size = 8_000_000, Extension = ".mp3" },
        ];
    }

    public Task<IReadOnlyList<FileManagerItem>> GetFolderContentsAsync(string? folderPath = null, CancellationToken cancellationToken = default)
    {
        var path = folderPath ?? "/";
        var normalizedPath = path.TrimEnd('/');
        if (string.IsNullOrEmpty(normalizedPath)) normalizedPath = "/";

        var children = _items
            .Where(i => i.Path != path && GetParentPath(i.Path) == normalizedPath)
            .ToList();

        return Task.FromResult<IReadOnlyList<FileManagerItem>>(children);
    }

    public Task<IReadOnlyList<FileManagerItem>> GetFolderTreeAsync(CancellationToken cancellationToken = default)
    {
        var folders = _items.Where(i => i.IsDirectory).ToList();
        return Task.FromResult<IReadOnlyList<FileManagerItem>>(folders);
    }

    public Task<FileManagerItem> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellationToken = default)
    {
        var path = $"{parentPath.TrimEnd('/')}/{folderName}";
        var item = new FileManagerItem { Id = Guid.NewGuid().ToString(), Name = folderName, Path = path, IsDirectory = true };
        _items.Add(item);
        return Task.FromResult(item);
    }

    public Task<FileManagerItem> RenameAsync(string itemPath, string newName, CancellationToken cancellationToken = default)
    {
        var item = _items.First(i => i.Path == itemPath);
        item.Name = newName;
        var parent = GetParentPath(itemPath);
        item.Path = $"{parent}/{newName}";
        return Task.FromResult(item);
    }

    public Task DeleteAsync(IReadOnlyList<string> itemPaths, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(i => itemPaths.Contains(i.Path));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FileManagerItem>> UploadAsync(string folderPath, IReadOnlyList<FileUploadInfo> files, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var uploaded = new List<FileManagerItem>();
        foreach (var file in files)
        {
            var name = file.FileName;
            var path = $"{folderPath.TrimEnd('/')}/{name}";
            var extension = System.IO.Path.GetExtension(name);
            uploaded.Add(new FileManagerItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Path = path,
                IsDirectory = false,
                Size = file.Size,
                Extension = extension
            });
            file.Stream.Dispose();
        }
        _items.AddRange(uploaded);
        return Task.FromResult<IReadOnlyList<FileManagerItem>>(uploaded);
    }

    public Task<Stream> DownloadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new MemoryStream([0x00, 0x01, 0x02]));
    }

    private static string GetParentPath(string itemPath)
    {
        itemPath = itemPath.TrimEnd('/');
        var lastSlash = itemPath.LastIndexOf('/');
        if (lastSlash <= 0) return "/";
        return itemPath.Substring(0, lastSlash);
    }
}
