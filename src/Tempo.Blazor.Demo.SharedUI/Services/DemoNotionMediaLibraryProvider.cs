using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// In-memory demo implementation of <see cref="INotionMediaLibraryProvider"/>.
/// Uses Picsum Photos for stable public image URLs.
/// </summary>
public sealed class DemoNotionMediaLibraryProvider : INotionMediaLibraryProvider
{
    private static readonly IReadOnlyList<NotionMediaLibraryItem> All =
    [
        MakeImage("lib-01", "Mountain landscape",  1,  "image/jpeg"),
        MakeImage("lib-02", "City at night",       2,  "image/jpeg"),
        MakeImage("lib-03", "Abstract pattern",    3,  "image/jpeg"),
        MakeImage("lib-04", "Forest path",         4,  "image/jpeg"),
        MakeImage("lib-05", "Ocean waves",         5,  "image/jpeg"),
        MakeImage("lib-06", "Desert dunes",        6,  "image/jpeg"),
        MakeImage("lib-07", "Autumn leaves",       7,  "image/jpeg"),
        MakeImage("lib-08", "Waterfall",           8,  "image/jpeg"),
        MakeImage("lib-09", "Snowy mountains",     9,  "image/jpeg"),
        MakeImage("lib-10", "Tropical beach",      10, "image/jpeg"),
        MakeImage("lib-11", "Wooden cabin",        11, "image/jpeg"),
        MakeImage("lib-12", "Flower field",        12, "image/jpeg"),
        MakeFile ("lib-13", "Project report.pdf",  "application/pdf"),
        MakeFile ("lib-14", "Budget 2024.xlsx",    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        MakeFile ("lib-15", "Meeting notes.docx",  "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
    ];

    public Task<IEnumerable<INotionMediaLibraryItem>> SearchAsync(
        string  query,
        string? mediaType = null,
        int     skip      = 0,
        int     take      = 24,
        CancellationToken ct = default)
    {
        var results = All.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            results = mediaType switch
            {
                "image" => results.Where(i => i.ContentType.StartsWith("image/")),
                "pdf"   => results.Where(i => i.ContentType == "application/pdf"),
                "file"  => results.Where(i => !i.ContentType.StartsWith("image/")),
                _       => results
            };
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            results = results.Where(i =>
                i.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var page = results.Skip(skip).Take(take);
        return Task.FromResult<IEnumerable<INotionMediaLibraryItem>>(page);
    }

    private static NotionMediaLibraryItem MakeImage(string id, string name, int seed, string contentType) =>
        new()
        {
            Id           = id,
            Name         = name,
            ContentType  = contentType,
            Url          = $"https://picsum.photos/seed/{seed}/1200/800",
            ThumbnailUrl = $"https://picsum.photos/seed/{seed}/200/150",
            FileSizeBytes = seed * 120_000L,
            CreatedAt    = DateTime.UtcNow.AddDays(-seed * 3),
        };

    private static NotionMediaLibraryItem MakeFile(string id, string name, string contentType) =>
        new()
        {
            Id           = id,
            Name         = name,
            ContentType  = contentType,
            Url          = $"https://example.com/files/{id}/{Uri.EscapeDataString(name)}",
            ThumbnailUrl = null,
            FileSizeBytes = 512_000,
            CreatedAt    = DateTime.UtcNow.AddDays(-7),
        };
}
