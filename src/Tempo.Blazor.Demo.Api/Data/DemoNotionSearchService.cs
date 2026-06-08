using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed partial class DemoNotionSearchService(
    MockNotionDataStore pageStore,
    MockNotionBlockStore blockStore)
{
    public Task<NotionSearchResponse> SearchAsync(NotionSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maxResults = Math.Clamp(request.MaxResults <= 0 ? 20 : request.MaxResults, 1, 100);
        var filter = request.Filter;
        var query = request.Query?.Trim() ?? string.Empty;
        var allPages = pageStore.GetAllPages()
            .Select(ClonePage)
            .ToArray();

        var matchingPageContainers = allPages
            .Where(page => MatchesContainerPageFilters(page, filter))
            .ToArray();

        var matchingPageResults = allPages
            .Where(page => MatchesPageFilters(page, filter))
            .ToArray();

        var pageMap = matchingPageContainers.ToDictionary(page => page.Id);
        var matchingPages = matchingPageResults
            .Where(page => MatchesQuery(query, PageSearchText(page)))
            .OrderByDescending(page => page.LastEditedAt)
            .ThenBy(page => page.Title, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToArray();

        var remaining = Math.Max(0, maxResults - matchingPages.Length);
        var matchingBlocks = remaining == 0
            ? []
            : blockStore.GetAllBlocksSnapshot()
                .Where(block => pageMap.ContainsKey(block.PageId))
                .Where(block => MatchesBlockFilters(block, filter))
                .Select(block => BuildBlockResult(block, pageMap[block.PageId], query))
                .Where(result => result is not null)
                .Select(result => result!)
                .OrderBy(result => pageMap[result.PageId].Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result.BlockType?.ToString(), StringComparer.OrdinalIgnoreCase)
                .Take(remaining)
                .ToArray();

        return Task.FromResult(new NotionSearchResponse
        {
            Pages = matchingPages,
            Blocks = matchingBlocks
        });
    }

    private bool MatchesPageFilters(NotionPage page, NotionSearchFilter? filter)
    {
        if (filter is null)
            return true;

        if (filter.InPageId is { } inPageId && page.Id != inPageId)
            return false;

        if (!MatchesAuthor(page, filter))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.LabelFilter) &&
            !page.Labels.Any(label => NormalizedContains(label, filter.LabelFilter)))
            return false;

        if (!IsWithinRange(page.CreatedAt, filter.CreatedAfter, filter.CreatedBefore))
            return false;

        if (!IsWithinRange(page.LastEditedAt, filter.LastEditedAfter, filter.LastEditedBefore))
            return false;

        if (!MatchesSpace(page.Id, filter.SpaceId))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.ContentType) &&
            !IsPageContentType(filter.ContentType))
            return false;

        if (filter.BlockType is not null)
            return false;

        return true;
    }

    private bool MatchesContainerPageFilters(NotionPage page, NotionSearchFilter? filter)
    {
        if (filter is null)
            return true;

        if (filter.InPageId is { } inPageId && page.Id != inPageId)
            return false;

        if (!MatchesAuthor(page, filter))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.LabelFilter) &&
            !page.Labels.Any(label => NormalizedContains(label, filter.LabelFilter)))
            return false;

        if (!MatchesSpace(page.Id, filter.SpaceId))
            return false;

        return true;
    }

    private bool MatchesBlockFilters(PageBlock block, NotionSearchFilter? filter)
    {
        if (filter is null)
            return true;

        if (filter.InPageId is { } inPageId && block.PageId != inPageId)
            return false;

        if (!IsWithinRange(block.CreatedAt, filter.CreatedAfter, filter.CreatedBefore))
            return false;

        if (!IsWithinRange(block.LastEditedAt, filter.LastEditedAfter, filter.LastEditedBefore))
            return false;

        if (!MatchesSpace(block.PageId, filter.SpaceId))
            return false;

        if (filter.BlockType is { } blockType && block.Type != blockType)
            return false;

        if (!string.IsNullOrWhiteSpace(filter.ContentType) &&
            !MatchesContentType(block.Type, filter.ContentType))
            return false;

        return true;
    }

    private bool MatchesAuthor(NotionPage page, NotionSearchFilter filter)
    {
        var author = string.IsNullOrWhiteSpace(filter.Author) ? filter.CreatedByUserId : filter.Author;
        if (string.IsNullOrWhiteSpace(author))
            return true;

        return NormalizedContains(page.CreatedByUserId, author) ||
               NormalizedContains(page.LastEditedByUserId, author);
    }

    private bool MatchesSpace(Guid pageId, string? spaceId)
    {
        if (string.IsNullOrWhiteSpace(spaceId))
            return true;

        var root = ResolveRootPage(pageId);
        if (root is null)
            return false;

        return string.Equals(root.Id.ToString("D"), spaceId.Trim(), StringComparison.OrdinalIgnoreCase) ||
               NormalizedContains(root.Title, spaceId);
    }

    private NotionSearchResult? BuildBlockResult(PageBlock block, NotionPage page, string query)
    {
        var text = ExtractSearchText(block.Content);
        if (!MatchesQuery(query, text))
            return null;

        var (snippet, ranges) = BuildSnippet(text, query);
        return new NotionSearchResult
        {
            PageId = page.Id,
            PageTitle = page.Title,
            PageIconEmoji = page.IconEmoji,
            BlockId = block.Id,
            BlockType = block.Type,
            MatchSnippet = snippet,
            HighlightRanges = ranges
        };
    }

    private static string PageSearchText(NotionPage page)
        => string.Join(' ', page.Title, page.Description, string.Join(' ', page.Labels));

    private static bool MatchesQuery(string query, string? text)
        => string.IsNullOrWhiteSpace(query) || NormalizedContains(text, query);

    private static bool NormalizedContains(string? value, string query)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(query))
            return false;

        return NormalizeForSearch(value).Contains(NormalizeForSearch(query), StringComparison.Ordinal);
    }

    private static string NormalizeForSearch(string value)
    {
        var normalized = WebUtility.HtmlDecode(value)
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsWithinRange(DateTime value, DateTime? after, DateTime? before)
    {
        if (after is { } min && value < min)
            return false;

        if (before is { } max)
        {
            var inclusiveMax = max.TimeOfDay == TimeSpan.Zero
                ? max.Date.AddDays(1).AddTicks(-1)
                : max;
            if (value > inclusiveMax)
                return false;
        }

        return true;
    }

    private static bool IsPageContentType(string contentType)
        => IsType(contentType, "page", "pages");

    private static bool MatchesContentType(BlockType blockType, string contentType)
    {
        if (IsType(contentType, "block", "blocks", "content"))
            return true;

        if (Enum.TryParse<BlockType>(contentType, ignoreCase: true, out var parsed))
            return blockType == parsed;

        return NormalizeForSearch(contentType) switch
        {
            "paragraph" or "text" => blockType is BlockType.Paragraph or BlockType.Quote or BlockType.Callout,
            "heading" or "headings" => blockType is BlockType.Heading1 or BlockType.Heading2 or BlockType.Heading3,
            "todo" or "task" or "tasks" => blockType is BlockType.TodoItem,
            "list" or "lists" => blockType is BlockType.BulletList or BlockType.NumberedList or BlockType.Toggle,
            "media" => blockType is BlockType.Image or BlockType.Video or BlockType.Audio or BlockType.File or BlockType.Pdf,
            "table" or "tables" => blockType is BlockType.Table or BlockType.TableRow,
            _ => false
        };
    }

    private static bool IsType(string contentType, params string[] aliases)
    {
        var normalized = NormalizeForSearch(contentType);
        return aliases.Any(alias => string.Equals(normalized, alias, StringComparison.Ordinal));
    }

    private NotionPage? ResolveRootPage(Guid pageId)
    {
        try
        {
            var current = ClonePage(pageStore.GetPageAsync(pageId.ToString("D")).GetAwaiter().GetResult());
            while (current.ParentId is { } parentId)
                current = ClonePage(pageStore.GetPageAsync(parentId.ToString("D")).GetAwaiter().GetResult());
            return current;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static NotionPage ClonePage(INotionPage page)
        => new()
        {
            Id = page.Id,
            ParentId = page.ParentId,
            Title = page.Title,
            Description = page.Description,
            SpaceId = page.SpaceId,
            IconEmoji = page.IconEmoji,
            IconImageUrl = page.IconImageUrl,
            CoverImageUrl = page.CoverImageUrl,
            CoverImagePositionY = page.CoverImagePositionY,
            IsFullWidth = page.IsFullWidth,
            IsSmallText = page.IsSmallText,
            IsLocked = page.IsLocked,
            CreatedAt = page.CreatedAt,
            CreatedByUserId = page.CreatedByUserId,
            LastEditedAt = page.LastEditedAt,
            LastEditedByUserId = page.LastEditedByUserId,
            IsDeleted = page.IsDeleted,
            DeletedAt = page.DeletedAt,
            IsFavorite = page.IsFavorite,
            Labels = page.Labels.ToArray()
        };

    private static (string Snippet, IReadOnlyList<NotionSearchHighlightRange> Ranges) BuildSnippet(string text, string query)
    {
        var plain = HtmlTagRegex().Replace(WebUtility.HtmlDecode(text), " ").Trim();
        plain = WhitespaceRegex().Replace(plain, " ");
        if (string.IsNullOrWhiteSpace(plain))
            return (string.Empty, []);

        if (string.IsNullOrWhiteSpace(query))
            return (plain.Length <= 180 ? plain : plain[..180], []);

        var match = FindNormalizedMatch(plain, query);
        if (match is null)
            return (plain.Length <= 180 ? plain : plain[..180], []);

        var (start, end) = match.Value;
        var snippetStart = Math.Max(0, start - 50);
        var snippetEnd = Math.Min(plain.Length, end + 90);
        var snippet = plain[snippetStart..snippetEnd];
        var ranges = new[]
        {
            new NotionSearchHighlightRange(start - snippetStart, end - snippetStart)
        };

        return (snippet, ranges);
    }

    private static (int Start, int End)? FindNormalizedMatch(string text, string query)
    {
        var normalizedQuery = NormalizeForSearch(query);
        if (normalizedQuery.Length == 0)
            return null;

        var normalized = new StringBuilder(text.Length);
        var originalIndexes = new List<int>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            foreach (var ch in text[i].ToString().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                    continue;

                normalized.Append(char.ToLowerInvariant(ch));
                originalIndexes.Add(i);
            }
        }

        var index = normalized.ToString().IndexOf(normalizedQuery, StringComparison.Ordinal);
        if (index < 0)
            return null;

        var start = originalIndexes[index];
        var end = originalIndexes[Math.Min(index + normalizedQuery.Length - 1, originalIndexes.Count - 1)] + 1;
        return (start, end);
    }

    private static string ExtractSearchText(IBlockContent content)
        => content switch
        {
            ITextBlockContent text => text.Html,
            IBookmarkBlockContent bookmark => string.Join(' ', bookmark.Title, bookmark.Description, bookmark.Caption, bookmark.Url),
            IFileBlockContent file => string.Join(' ', file.FileName, file.ContentType, file.Caption, file.Url),
            IMediaBlockContent media => string.Join(' ', media.Caption, media.Url),
            ITableRowBlockContent row => string.Join(' ', row.Cells.Concat(row.RichCells.Select(cell => cell.Html))),
            IChildPageBlockContent child => child.Title ?? string.Empty,
            ILinkedPageBlockContent linked => linked.Title ?? string.Empty,
            IInlineDatabaseBlockContent database => database.Title,
            ICodeBlockContent code => string.Join(' ', code.Caption, code.Code),
            IEquationBlockContent equation => equation.Expression,
            IEmbedBlockContent embed => string.Join(' ', embed.Caption, embed.Url),
            IExcerptBlockContent excerpt => excerpt.Html ?? string.Empty,
            IDiagramBlockContent diagram => diagram.Caption ?? string.Empty,
            IWireframeBlockContent wireframe => wireframe.Caption ?? string.Empty,
            ISpreadsheetBlockContent spreadsheet => spreadsheet.Caption ?? string.Empty,
            IPagePropertiesBlockContent pageProperties => string.Join(' ', pageProperties.Rows.Select(row => string.Join(' ', row.Key, row.ValueHtml))),
            _ => string.Empty
        };

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
