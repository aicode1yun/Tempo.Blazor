using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// In-memory demo data provider for <see cref="Tempo.Blazor.Components.Files.TmDocumentManager{TMetadata}"/>.
/// Includes metadata, tags, categories, and per-item permissions.
/// </summary>
public class DemoDocumentManagerProvider : IDocumentManagerDataProvider<DocumentMetadata>
{
    private readonly List<DocumentManagerItem<DocumentMetadata>> _items;
    private readonly Dictionary<string, List<FileAttachment>> _attachments = new();

    public DemoDocumentManagerProvider(bool readOnly = false)
    {
        _items =
        [
            // Root folders
            new()
            {
                Id = "f1", Name = "Documents", Path = "/Documents", IsDirectory = true,
                Metadata = new DocumentMetadata { Category = "Work", Description = "Document folder", Owner = "Admin", Tags = ["docs"] },
                Permissions = new DocumentManagerPermission { CanDelete = false, CanRename = false }
            },
            new()
            {
                Id = "f2", Name = "Pictures", Path = "/Pictures", IsDirectory = true,
                Metadata = new DocumentMetadata { Category = "Media", Description = "Image folder", Owner = "Admin", Tags = ["images"] },
                Permissions = new DocumentManagerPermission { CanDelete = true, CanRename = true }
            },
            new()
            {
                Id = "f3", Name = "Music", Path = "/Music", IsDirectory = true,
                Metadata = new DocumentMetadata { Category = "Media", Description = "Audio folder", Owner = "Admin", Tags = ["audio"] },
                Permissions = new DocumentManagerPermission { CanUpload = false, CanCreateFolder = false }
            },
            new()
            {
                Id = "f4", Name = "Archive", Path = "/Archive", IsDirectory = true,
                Metadata = new DocumentMetadata { Category = "Archive", Description = "Old files", Owner = "Admin", Tags = ["old"] },
                Permissions = new DocumentManagerPermission { CanRead = false }
            },

            // Documents folder files
            new()
            {
                Id = "d1", Name = "Annual Report.pdf", Path = "/Documents/Annual Report.pdf",
                IsDirectory = false, Size = 1_024_000, Extension = ".pdf",
                ModifiedDate = new DateTime(2025, 3, 15),
                Metadata = new DocumentMetadata
                {
                    Category = "Finance",
                    Description = "Annual financial report for FY2025",
                    Owner = "John Doe",
                    Tags = ["finance", "annual", "pdf"],
                    ReviewDate = new DateTime(2025, 6, 30)
                }
            },
            new()
            {
                Id = "d2", Name = "Budget.xlsx", Path = "/Documents/Budget.xlsx",
                IsDirectory = false, Size = 512_000, Extension = ".xlsx",
                ModifiedDate = new DateTime(2025, 4, 1),
                Metadata = new DocumentMetadata
                {
                    Category = "Finance",
                    Description = "Q2 budget planning",
                    Owner = "Jane Smith",
                    Tags = ["budget", "q2", "planning"],
                    ReviewDate = new DateTime(2025, 5, 15)
                },
                Permissions = new DocumentManagerPermission { CanDelete = false }
            },
            new()
            {
                Id = "d3", Name = "Meeting Notes.txt", Path = "/Documents/Meeting Notes.txt",
                IsDirectory = false, Size = 4_096, Extension = ".txt",
                ModifiedDate = new DateTime(2025, 4, 20),
                Metadata = new DocumentMetadata
                {
                    Category = "General",
                    Description = "Notes from weekly standup",
                    Owner = "Team",
                    Tags = ["notes", "meeting"]
                }
            },

            // Pictures folder files
            new()
            {
                Id = "p1", Name = "Vacation.jpg", Path = "/Pictures/Vacation.jpg",
                IsDirectory = false, Size = 3_500_000, Extension = ".jpg",
                ModifiedDate = new DateTime(2024, 8, 10),
                Metadata = new DocumentMetadata
                {
                    Category = "Personal",
                    Description = "Summer vacation photos",
                    Owner = "John Doe",
                    Tags = ["vacation", "summer", "photos"]
                }
            },
            new()
            {
                Id = "p2", Name = "Logo.png", Path = "/Pictures/Logo.png",
                IsDirectory = false, Size = 120_000, Extension = ".png",
                ModifiedDate = new DateTime(2025, 1, 5),
                Metadata = new DocumentMetadata
                {
                    Category = "Branding",
                    Description = "Company logo in PNG format",
                    Owner = "Marketing",
                    Tags = ["logo", "branding", "png"]
                }
            },

            // Music folder files
            new()
            {
                Id = "m1", Name = "Podcast Intro.mp3", Path = "/Music/Podcast Intro.mp3",
                IsDirectory = false, Size = 8_000_000, Extension = ".mp3",
                ModifiedDate = new DateTime(2025, 2, 14),
                Metadata = new DocumentMetadata
                {
                    Category = "Audio",
                    Description = "Intro music for company podcast",
                    Owner = "Media Team",
                    Tags = ["audio", "podcast", "intro"]
                },
                Permissions = new DocumentManagerPermission { CanDownload = false }
            },
        ];

        // Seed some attachments for demo
        _attachments["d1"] =
        [
            new FileAttachment { Id = "a1", Name = "Annual Report.pdf", Size = 1_024_000, ContentType = "application/pdf", CreatedDate = new DateTime(2025, 3, 15) },
            new FileAttachment { Id = "a2", Name = "Appendix.xlsx", Size = 256_000, ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", CreatedDate = new DateTime(2025, 3, 15) }
        ];
        _attachments["d2"] =
        [
            new FileAttachment { Id = "a3", Name = "Budget.xlsx", Size = 512_000, ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", CreatedDate = new DateTime(2025, 4, 1) }
        ];
        foreach (var kvp in _attachments)
        {
            var item = _items.FirstOrDefault(i => i.Id == kvp.Key);
            if (item is not null)
                item.Attachments = kvp.Value;
        }

        if (readOnly)
        {
            foreach (var item in _items)
            {
                item.Permissions = new DocumentManagerPermission
                {
                    CanRead = item.Permissions?.CanRead ?? true,
                    CanWrite = false,
                    CanDelete = false,
                    CanRename = false,
                    CanMove = false,
                    CanCopy = false,
                    CanShare = false,
                    CanUpload = false,
                    CanCreateFolder = false,
                    CanDownload = item.Permissions?.CanDownload ?? true
                };
            }
        }
    }

    public Task<IReadOnlyList<DocumentManagerItem<DocumentMetadata>>> GetFolderContentsAsync(
        string? folderPath = null, CancellationToken cancellationToken = default)
    {
        var path = folderPath ?? "/";
        var normalizedPath = path.TrimEnd('/');
        if (string.IsNullOrEmpty(normalizedPath)) normalizedPath = "/";

        var children = _items
            .Where(i => i.Path != path && GetParentPath(i.Path) == normalizedPath)
            .ToList();

        return Task.FromResult<IReadOnlyList<DocumentManagerItem<DocumentMetadata>>>(children);
    }

    public Task<IReadOnlyList<DocumentManagerItem<DocumentMetadata>>> GetFolderTreeAsync(
        CancellationToken cancellationToken = default)
    {
        var folders = _items.Where(i => i.IsDirectory).ToList();
        return Task.FromResult<IReadOnlyList<DocumentManagerItem<DocumentMetadata>>>(folders);
    }

    public Task<DocumentManagerItem<DocumentMetadata>> GetItemDetailAsync(
        string itemId, CancellationToken cancellationToken = default)
    {
        var item = _items.First(i => i.Id == itemId);
        return Task.FromResult(item);
    }

    public Task<DocumentManagerItem<DocumentMetadata>> CreateFolderAsync(
        string parentPath, string folderName, DocumentMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"{parentPath.TrimEnd('/')}/{folderName}";
        var item = new DocumentManagerItem<DocumentMetadata>
        {
            Id = Guid.NewGuid().ToString(),
            Name = folderName,
            Path = path,
            IsDirectory = true,
            Metadata = metadata
        };
        _items.Add(item);
        return Task.FromResult(item);
    }

    public Task<DocumentManagerItem<DocumentMetadata>> RenameAsync(
        string itemId, string newName, CancellationToken cancellationToken = default)
    {
        var item = _items.First(i => i.Id == itemId);
        item.Name = newName;
        var parent = GetParentPath(item.Path);
        item.Path = $"{parent}/{newName}";
        return Task.FromResult(item);
    }

    public Task DeleteAsync(IReadOnlyList<string> itemIds, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(i => itemIds.Contains(i.Id));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentManagerItem<DocumentMetadata>>> UploadAsync(
        string folderPath, IReadOnlyList<FileUploadInfo> files,
        DocumentMetadata? metadata = null,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var uploaded = new List<DocumentManagerItem<DocumentMetadata>>();
        foreach (var file in files)
        {
            var name = file.FileName;
            var path = $"{folderPath.TrimEnd('/')}/{name}";
            var extension = System.IO.Path.GetExtension(name);
            uploaded.Add(new DocumentManagerItem<DocumentMetadata>
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Path = path,
                IsDirectory = false,
                Size = file.Size,
                Extension = extension,
                Metadata = metadata,
                ModifiedDate = DateTime.Now
            });
            file.Stream.Dispose();
        }
        _items.AddRange(uploaded);
        return Task.FromResult<IReadOnlyList<DocumentManagerItem<DocumentMetadata>>>(uploaded);
    }

    public Task<Stream> DownloadAsync(string fileId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new MemoryStream([0x00, 0x01, 0x02]));
    }

    public Task<DocumentManagerItem<DocumentMetadata>> UpdateMetadataAsync(
        string itemId, DocumentMetadata metadata, CancellationToken cancellationToken = default)
    {
        var item = _items.First(i => i.Id == itemId);
        item.Metadata = metadata;
        return Task.FromResult(item);
    }

    public Task<DocumentManagerItem<DocumentMetadata>> MoveAsync(
        string itemId, string targetFolderPath, CancellationToken cancellationToken = default)
    {
        var item = _items.First(i => i.Id == itemId);
        var name = item.Name;
        item.Path = $"{targetFolderPath.TrimEnd('/')}/{name}";
        return Task.FromResult(item);
    }

    public Task<DocumentManagerItem<DocumentMetadata>> CopyAsync(
        string itemId, string targetFolderPath, CancellationToken cancellationToken = default)
    {
        var original = _items.First(i => i.Id == itemId);
        var copy = new DocumentManagerItem<DocumentMetadata>
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Copy of {original.Name}",
            Path = $"{targetFolderPath.TrimEnd('/')}/Copy of {original.Name}",
            IsDirectory = original.IsDirectory,
            Size = original.Size,
            Extension = original.Extension,
            ModifiedDate = original.ModifiedDate,
            Metadata = original.Metadata,
            Permissions = original.Permissions
        };
        _items.Add(copy);
        return Task.FromResult(copy);
    }

    public Task<string?> UploadChunkAsync(FileChunkData chunk, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<IReadOnlyList<FileAttachment>> GetAttachmentsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (_attachments.TryGetValue(itemId, out var list))
            return Task.FromResult<IReadOnlyList<FileAttachment>>(list);
        return Task.FromResult<IReadOnlyList<FileAttachment>>([]);
    }

    public Task<IReadOnlyList<FileAttachment>> AddAttachmentsAsync(
        string itemId, IReadOnlyList<FileUploadInfo> files, CancellationToken cancellationToken = default)
    {
        if (!_attachments.ContainsKey(itemId))
            _attachments[itemId] = [];

        var list = _attachments[itemId];
        foreach (var file in files)
        {
            list.Add(new FileAttachment
            {
                Id = Guid.NewGuid().ToString(),
                Name = file.FileName,
                Size = file.Size,
                ContentType = file.ContentType,
                CreatedDate = DateTime.Now
            });
            file.Stream.Dispose();
        }

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is not null)
            item.Attachments = list;

        return Task.FromResult<IReadOnlyList<FileAttachment>>(list);
    }

    public Task RemoveAttachmentAsync(string itemId, string attachmentId, CancellationToken cancellationToken = default)
    {
        if (_attachments.TryGetValue(itemId, out var list))
        {
            list.RemoveAll(a => a.Id == attachmentId);
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item is not null)
                item.Attachments = list;
        }
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAttachmentAsync(string itemId, string attachmentId, CancellationToken cancellationToken = default)
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
