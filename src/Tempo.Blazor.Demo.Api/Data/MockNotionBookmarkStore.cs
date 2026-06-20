using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class MockNotionBookmarkStore
{
    private readonly Dictionary<string, BookmarkBlockContent> _metadata = new(StringComparer.OrdinalIgnoreCase)
    {
        ["https://docs.tempo.local/notion/special-blocks"] = new BookmarkBlockContent
        {
            Url = "https://docs.tempo.local/notion/special-blocks",
            Title = "Tempo Notion special blocks",
            Description = "Production verification notes for equation, bookmark, embed, synced, navigation, diagram, wireframe, and spreadsheet blocks.",
            Domain = "docs.tempo.local",
            FaviconUrl = "https://docs.tempo.local/favicon.ico",
            CoverImageUrl = "https://docs.tempo.local/assets/notion-special-blocks.png"
        },
        ["https://www.tempo-blazor.local/releases/eb15"] = new BookmarkBlockContent
        {
            Url = "https://www.tempo-blazor.local/releases/eb15",
            Title = "EB15 release readiness",
            Description = "Backfill coverage for special Notion blocks, fallback rendering, and UX screenshot baselines.",
            Domain = "tempo-blazor.local",
            FaviconUrl = "https://www.tempo-blazor.local/favicon.ico"
        },
        ["https://docs.tempo.local/notion/very-long-smart-link-title"] = new BookmarkBlockContent
        {
            Url = "https://docs.tempo.local/notion/very-long-smart-link-title",
            Title = "Tempo Notion Smart Link Preview With An Exceptionally Long Title That Must Truncate Cleanly In Inline Mode",
            Description = "Long title preview used for inline truncation coverage.",
            Domain = "docs.tempo.local",
            FaviconUrl = "https://docs.tempo.local/favicon.ico"
        }
    };

    public BookmarkBlockContent Resolve(string url)
    {
        if (!Uri.TryCreate(NormalizeUrl(url), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new UriFormatException("URL must be an absolute HTTP or HTTPS address.");
        }

        var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        if (_metadata.TryGetValue(normalized, out var known))
        {
            return Clone(known);
        }

        return new BookmarkBlockContent
        {
            Url = uri.ToString(),
            Domain = uri.Host
        };
    }

    public SmartLinkDto? ResolveSmartLink(string url)
    {
        if (Uri.TryCreate(NormalizeUrl(url), UriKind.Absolute, out var smartLinkUri) &&
            string.Equals(smartLinkUri.Host, "resolver-fail.tempo.local", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Smart link metadata resolver is unavailable for this host.");
        }

        var bookmark = Resolve(url);
        var title = string.IsNullOrWhiteSpace(bookmark.Title)
            ? bookmark.Domain ?? bookmark.Url
            : bookmark.Title;

        return new SmartLinkDto(
            bookmark.Url,
            title,
            bookmark.FaviconUrl,
            bookmark.Description,
            bookmark.CoverImageUrl,
            bookmark.Domain);
    }

    private static string NormalizeUrl(string url)
    {
        var trimmed = url.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"https://{trimmed}";
    }

    private static BookmarkBlockContent Clone(BookmarkBlockContent source) => new()
    {
        Url = source.Url,
        Title = source.Title,
        Description = source.Description,
        CoverImageUrl = source.CoverImageUrl,
        FaviconUrl = source.FaviconUrl,
        Domain = source.Domain,
        Caption = source.Caption
    };
}
