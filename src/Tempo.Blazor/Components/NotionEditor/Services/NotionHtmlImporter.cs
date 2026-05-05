using System.Text.RegularExpressions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Parses an HTML string (document or fragment) into an ordered list of <see cref="PageBlock"/> objects.
/// Uses a regex-based approach supporting: headings, paragraphs, ul/ol lists, blockquotes,
/// pre/code blocks, hr dividers, images, tables, and details/summary toggles.
/// </summary>
public static class NotionHtmlImporter
{
    // ── Public entry point ────────────────────────────────────────────────────

    public static List<IPageBlock> Import(string html, Guid pageId)
    {
        var blocks  = new List<IPageBlock>();
        var content = ExtractBodyContent(html ?? string.Empty);

        int order = 0;
        ParseFragment(content, pageId, blocks, ref order);

        return blocks;
    }

    // ── Body extraction ───────────────────────────────────────────────────────

    private static string ExtractBodyContent(string html)
    {
        var bodyMatch = Regex.Match(html,
            @"<body[^>]*>([\s\S]*?)</body>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return bodyMatch.Success ? bodyMatch.Groups[1].Value : html;
    }

    // ── Fragment parser (recursive for nested lists) ──────────────────────────

    private static void ParseFragment(string html, Guid pageId, List<IPageBlock> blocks, ref int order)
    {
        // We tokenise by matching top-level block elements in document order.
        // The regex matches one complete block element at a time.
        var pattern = new Regex(
            @"<(h[1-6]|p|ul|ol|li|blockquote|pre|hr|img|table|figure|div|details|article|section|header|footer|main|aside|nav)\b([^>]*)>([\s\S]*?)</\1>"
            + @"|<hr\s*/?>|<img\b([^>]*)/?>"
            , RegexOptions.IgnoreCase | RegexOptions.Singleline);

        int pos = 0;
        foreach (Match m in pattern.Matches(html))
        {
            var tag  = m.Groups[1].Value.ToLowerInvariant();
            var attr = (m.Groups[2].Value + m.Groups[4].Value).Trim();
            var inner = m.Groups[3].Value;

            switch (tag)
            {
                case "h1":
                    blocks.Add(MakeBlock(pageId, order++, BlockType.Heading1,
                        new HeadingBlockContent { Html = SanitizeInline(inner), Level = 1 }));
                    break;

                case "h2":
                    blocks.Add(MakeBlock(pageId, order++, BlockType.Heading2,
                        new HeadingBlockContent { Html = SanitizeInline(inner), Level = 2 }));
                    break;

                case "h3": case "h4": case "h5": case "h6":
                    blocks.Add(MakeBlock(pageId, order++, BlockType.Heading3,
                        new HeadingBlockContent { Html = SanitizeInline(inner), Level = 3 }));
                    break;

                case "p":
                    var pHtml = SanitizeInline(inner);
                    if (!string.IsNullOrWhiteSpace(StripTags(pHtml)))
                        blocks.Add(MakeBlock(pageId, order++, BlockType.Paragraph,
                            new TextBlockContent { Html = pHtml }));
                    break;

                case "ul":
                    ParseList(inner, pageId, blocks, ref order, false, 0);
                    break;

                case "ol":
                    ParseList(inner, pageId, blocks, ref order, true, 0);
                    break;

                case "blockquote":
                    var bqHtml = SanitizeInline(StripBlockTags(inner));
                    blocks.Add(MakeBlock(pageId, order++, BlockType.Quote,
                        new TextBlockContent { Html = bqHtml }));
                    break;

                case "pre":
                    ParsePre(inner, pageId, blocks, ref order);
                    break;

                case "hr":
                    blocks.Add(MakeBlock(pageId, order++, BlockType.Divider, new DividerBlockContent()));
                    break;

                case "img":
                    var imgUrl  = ExtractAttr(attr, "src");
                    var imgAlt  = ExtractAttr(attr, "alt");
                    if (!string.IsNullOrWhiteSpace(imgUrl))
                        blocks.Add(MakeBlock(pageId, order++, BlockType.Image,
                            new ImageBlockContent { Url = imgUrl, AltText = imgAlt, Caption = imgAlt }));
                    break;

                case "figure":
                    ParseFigure(inner, pageId, blocks, ref order);
                    break;

                case "table":
                    ParseTable(inner, pageId, blocks, ref order);
                    break;

                case "details":
                    ParseToggle(inner, attr, pageId, blocks, ref order);
                    break;

                case "div": case "article": case "section": case "main":
                case "header": case "footer": case "aside": case "nav":
                    // recurse into container elements
                    ParseFragment(inner, pageId, blocks, ref order);
                    break;
            }

            pos = m.Index + m.Length;
        }

        // text nodes between matched elements → paragraphs
        var remaining = pos < html.Length ? html[pos..] : string.Empty;
        if (!string.IsNullOrWhiteSpace(StripTags(remaining)))
        {
            var txt = SanitizeInline(remaining.Trim());
            if (!string.IsNullOrWhiteSpace(StripTags(txt)))
                blocks.Add(MakeBlock(pageId, order++, BlockType.Paragraph,
                    new TextBlockContent { Html = txt }));
        }
    }

    // ── List parser ───────────────────────────────────────────────────────────

    private static void ParseList(string html, Guid pageId, List<IPageBlock> blocks,
                                   ref int order, bool numbered, int indentLevel)
    {
        var itemPattern = new Regex(
            @"<li\b[^>]*>([\s\S]*?)</li>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match item in itemPattern.Matches(html))
        {
            var inner = item.Groups[1].Value;

            // check for task-list checkbox
            var checkMatch = Regex.Match(inner,
                @"<input\b[^>]*type=""checkbox""([^>]*)>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (checkMatch.Success)
            {
                var isChecked = checkMatch.Value.Contains("checked", StringComparison.OrdinalIgnoreCase);
                var liText    = SanitizeInline(inner.Replace(checkMatch.Value, string.Empty));
                blocks.Add(MakeBlock(pageId, order++, BlockType.TodoItem,
                    new TodoBlockContent { IsChecked = isChecked, Html = liText }));
                continue;
            }

            // check for nested list
            var nestedUl = Regex.Match(inner, @"<ul\b[^>]*>([\s\S]*?)</ul>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var nestedOl = Regex.Match(inner, @"<ol\b[^>]*>([\s\S]*?)</ol>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var liHtml   = inner;
            if (nestedUl.Success) liHtml = liHtml.Replace(nestedUl.Value, string.Empty);
            if (nestedOl.Success) liHtml = liHtml.Replace(nestedOl.Value, string.Empty);

            var liText2 = SanitizeInline(liHtml);
            if (!string.IsNullOrWhiteSpace(StripTags(liText2)))
            {
                var type = numbered ? BlockType.NumberedList : BlockType.BulletList;
                blocks.Add(MakeBlock(pageId, order++, type,
                    new ListBlockContent { Html = liText2, IndentLevel = indentLevel }));
            }

            // recurse into nested lists
            if (nestedUl.Success) ParseList(nestedUl.Groups[1].Value, pageId, blocks, ref order, false, indentLevel + 1);
            if (nestedOl.Success) ParseList(nestedOl.Groups[1].Value, pageId, blocks, ref order, true,  indentLevel + 1);
        }
    }

    // ── Pre / Code block ──────────────────────────────────────────────────────

    private static void ParsePre(string inner, Guid pageId, List<IPageBlock> blocks, ref int order)
    {
        var codeMatch = Regex.Match(inner,
            @"<code\b([^>]*)>([\s\S]*?)</code>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        string lang = string.Empty;
        string code;

        if (codeMatch.Success)
        {
            var classAttr = ExtractAttr(codeMatch.Groups[1].Value, "class");
            var langMatch = Regex.Match(classAttr ?? string.Empty, @"language-(\S+)");
            lang = langMatch.Success ? langMatch.Groups[1].Value : string.Empty;
            code = DecodeHtmlEntities(codeMatch.Groups[2].Value);
        }
        else
        {
            code = DecodeHtmlEntities(StripTags(inner));
        }

        blocks.Add(MakeBlock(pageId, order++, BlockType.Code,
            new CodeBlockContent
            {
                Code     = code.TrimEnd('\n', '\r'),
                Language = string.IsNullOrWhiteSpace(lang) ? null : lang
            }));
    }

    // ── Figure (image + figcaption) ───────────────────────────────────────────

    private static void ParseFigure(string inner, Guid pageId, List<IPageBlock> blocks, ref int order)
    {
        var imgMatch = Regex.Match(inner, @"<img\b([^>]*)/?>" , RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var capMatch = Regex.Match(inner, @"<figcaption\b[^>]*>([\s\S]*?)</figcaption>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (imgMatch.Success)
        {
            var url     = ExtractAttr(imgMatch.Groups[1].Value, "src") ?? string.Empty;
            var alt     = ExtractAttr(imgMatch.Groups[1].Value, "alt") ?? string.Empty;
            var caption = capMatch.Success ? StripTags(capMatch.Groups[1].Value) : alt;

            blocks.Add(MakeBlock(pageId, order++, BlockType.Image,
                new ImageBlockContent { Url = url, AltText = alt, Caption = caption }));
            return;
        }

        // pre inside figure → code block
        var preMatch = Regex.Match(inner, @"<pre\b[^>]*>([\s\S]*?)</pre>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (preMatch.Success)
            ParsePre(preMatch.Groups[1].Value, pageId, blocks, ref order);
    }

    // ── Table ─────────────────────────────────────────────────────────────────

    private static void ParseTable(string inner, Guid pageId, List<IPageBlock> blocks, ref int order)
    {
        // extract all rows from thead + tbody
        var rowPattern = new Regex(
            @"<tr\b[^>]*>([\s\S]*?)</tr>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var cellPattern = new Regex(
            @"<t[dh]\b[^>]*>([\s\S]*?)</t[dh]>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match row in rowPattern.Matches(inner))
        {
            var cells = cellPattern.Matches(row.Groups[1].Value)
                .Cast<Match>()
                .Select(c => SanitizeInline(c.Groups[1].Value))
                .ToList();

            if (cells.Count > 0)
                blocks.Add(MakeBlock(pageId, order++, BlockType.TableRow,
                    new TableRowBlockContent { Cells = cells }));
        }
    }

    // ── Details/Summary → Toggle ──────────────────────────────────────────────

    private static void ParseToggle(string inner, string attr, Guid pageId, List<IPageBlock> blocks, ref int order)
    {
        var summaryMatch = Regex.Match(inner,
            @"<summary\b[^>]*>([\s\S]*?)</summary>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var summaryHtml = summaryMatch.Success
            ? SanitizeInline(summaryMatch.Groups[1].Value)
            : string.Empty;

        var isOpen = attr.Contains("open", StringComparison.OrdinalIgnoreCase);

        blocks.Add(MakeBlock(pageId, order++, BlockType.Toggle,
            new ToggleBlockContent { Html = summaryHtml, IsOpen = isOpen }));
    }

    // ── HTML sanitisation helpers ─────────────────────────────────────────────

    private static readonly HashSet<string> InlineTags = new(StringComparer.OrdinalIgnoreCase)
        { "strong", "b", "em", "i", "u", "s", "del", "code", "a", "br", "span", "mark", "sub", "sup" };

    private static string SanitizeInline(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        return Regex.Replace(html, @"<(/?)(\w+)([^>]*)>", m =>
        {
            var close = m.Groups[1].Value;
            var tag   = m.Groups[2].Value;
            var attrs = m.Groups[3].Value;

            if (!InlineTags.Contains(tag)) return string.Empty;

            if (tag.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                var href = ExtractAttr(attrs, "href");
                return href != null ? $"<{close}a href=\"{HtmlAttrEncode(href)}\">" : string.Empty;
            }

            if (tag.Equals("span", StringComparison.OrdinalIgnoreCase) ||
                tag.Equals("mark", StringComparison.OrdinalIgnoreCase))
            {
                var style = ExtractAttr(attrs, "style");
                if (style != null)
                {
                    var safeStyle = FilterStyle(style);
                    return !string.IsNullOrEmpty(safeStyle)
                        ? $"<{close}{tag} style=\"{safeStyle}\">"
                        : close == "/" ? $"</{tag}>" : string.Empty;
                }
                return $"<{close}{tag}>";
            }

            return $"<{close}{tag}>";
        });
    }

    private static string FilterStyle(string style) =>
        string.Join(";", style.Split(';')
            .Select(p => p.Trim())
            .Where(p => p.StartsWith("color:", StringComparison.OrdinalIgnoreCase)
                     || p.StartsWith("background-color:", StringComparison.OrdinalIgnoreCase)
                     || p.StartsWith("background:", StringComparison.OrdinalIgnoreCase)));

    private static string StripTags(string? html) =>
        string.IsNullOrEmpty(html) ? string.Empty
            : Regex.Replace(html, @"<[^>]+>", string.Empty);

    private static string StripBlockTags(string html)
    {
        // strip block-level tags but preserve inline content
        return Regex.Replace(html, @"</?(?:div|p|br|ul|ol|li|blockquote|pre|code|h[1-6])\b[^>]*>",
            m => m.Value.Contains("br") ? " " : " ",
            RegexOptions.IgnoreCase);
    }

    private static string? ExtractAttr(string attrs, string name)
    {
        var m = Regex.Match(attrs,
            $@"{Regex.Escape(name)}\s*=\s*""([^""]*?)""",
            RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;

        m = Regex.Match(attrs,
            $@"{Regex.Escape(name)}\s*=\s*'([^']*?)'",
            RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;

        m = Regex.Match(attrs,
            $@"{Regex.Escape(name)}\s*=\s*(\S+)",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string DecodeHtmlEntities(string text) =>
        text.Replace("&amp;",  "&")
            .Replace("&lt;",   "<")
            .Replace("&gt;",   ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;",  "'")
            .Replace("&nbsp;", " ");

    private static string HtmlAttrEncode(string? url) =>
        string.IsNullOrEmpty(url) ? string.Empty
            : url.Replace("\"", "&quot;").Replace("'", "&#39;");

    // ── Factory ───────────────────────────────────────────────────────────────

    private static IPageBlock MakeBlock(Guid pageId, int order, BlockType type, IBlockContent content)
        => new PageBlock
        {
            Id           = Guid.NewGuid(),
            PageId       = pageId,
            Type         = type,
            Order        = order,
            Content      = content,
            CreatedAt    = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        };
}
