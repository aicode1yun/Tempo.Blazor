using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using TableColumnAlignment = Tempo.Blazor.DocumentEditor.Models.TableColumnAlignment;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Converts an ordered list of <see cref="IPageBlock"/> objects into a Markdown string.
/// Inline HTML attributes in block content (bold, italic, code, links…) are mapped to
/// standard CommonMark / GFM syntax.
/// </summary>
public static class NotionMarkdownExporter
{
    // ── Public entry point ────────────────────────────────────────────────────

    public static string Export(IReadOnlyList<IPageBlock> blocks, string pageTitle = "")
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(pageTitle))
        {
            sb.AppendLine($"# {EscapeMarkdown(pageTitle)}");
            sb.AppendLine();
        }

        var childRows = GroupChildRows(blocks);

        // track ordered-list counters per indent level
        var orderedCounters = new Dictionary<int, int>();
        bool tableFirstRow  = false;
        BlockType? prevType = null;

        foreach (var block in blocks)
        {
            // rows owned by a Table block are rendered with their parent
            if (block.Type == BlockType.TableRow && block.ParentBlockId.HasValue)
                continue;

            if (block.Type != BlockType.NumberedList)
                orderedCounters.Clear();

            // reset table-first-row tracker when entering/leaving a legacy flat table section
            if (block.Type == BlockType.TableRow && prevType != BlockType.TableRow)
                tableFirstRow = true;

            string line;
            if (block.Type == BlockType.Table)
            {
                line = RenderTable(block, childRows.TryGetValue(block.Id, out var rows) ? rows : []);
                if (line.Length == 0)
                {
                    continue;
                }
            }
            else if (block.Type == BlockType.TableRow)
            {
                line = RenderTableRow(block, tableFirstRow);
            }
            else
            {
                line = RenderBlock(block, orderedCounters);
            }

            if (block.Type == BlockType.TableRow) tableFirstRow = false;

            // blank line between different block types for readability
            if (prevType is not null
                && prevType != block.Type
                && prevType != BlockType.Divider
                && block.Type != BlockType.Divider)
            {
                sb.AppendLine();
            }

            sb.AppendLine(line);
            prevType = block.Type;
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static Dictionary<Guid, List<IPageBlock>> GroupChildRows(IReadOnlyList<IPageBlock> blocks)
        => blocks
            .Where(block => block.Type == BlockType.TableRow && block.ParentBlockId.HasValue)
            .GroupBy(block => block.ParentBlockId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(block => block.Order).ToList());

    // ── Block rendering ───────────────────────────────────────────────────────

    private static string RenderBlock(IPageBlock block, Dictionary<int, int> counters) =>
        block.Type switch
        {
            BlockType.Heading1  => RenderHeading(block, 1),
            BlockType.Heading2  => RenderHeading(block, 2),
            BlockType.Heading3  => RenderHeading(block, 3),
            BlockType.BulletList   => RenderBullet(block),
            BlockType.NumberedList => RenderNumbered(block, counters),
            BlockType.TodoItem     => RenderTodo(block),
            BlockType.Quote        => RenderQuote(block),
            BlockType.Callout      => RenderCallout(block),
            BlockType.Toggle       => RenderToggle(block),
            BlockType.Code         => RenderCode(block),
            BlockType.Divider      => "---",
            BlockType.Image        => RenderImage(block),
            BlockType.Video        => RenderVideo(block),
            BlockType.Audio        => RenderAudio(block),
            BlockType.File         => RenderFile(block),
            BlockType.Pdf          => RenderPdf(block),
            BlockType.Bookmark     => RenderBookmark(block),
            BlockType.Embed        => RenderEmbed(block),
            BlockType.Equation     => RenderEquation(block),
            BlockType.Table        => string.Empty,   // table handled separately via TableRow blocks
            BlockType.TableRow     => string.Empty,   // handled in Export loop with local state
            BlockType.ChildPage    => RenderChildPage(block),
            BlockType.LinkedPage   => RenderLinkedPage(block),
            _                      => RenderParagraph(block)
        };

    // ── Heading ───────────────────────────────────────────────────────────────

    private static string RenderHeading(IPageBlock block, int level)
    {
        var prefix = new string('#', level);
        var text = block.Content is ITextBlockContent tc
            ? HtmlToMarkdownInline(tc.Html)
            : string.Empty;
        return $"{prefix} {text}";
    }

    // ── Paragraph ─────────────────────────────────────────────────────────────

    private static string RenderParagraph(IPageBlock block)
    {
        if (block.Content is not ITextBlockContent tc) return string.Empty;
        return HtmlToMarkdownInline(tc.Html);
    }

    // ── Lists ─────────────────────────────────────────────────────────────────

    private static string RenderBullet(IPageBlock block)
    {
        var indent = GetListIndent(block);
        var text   = block.Content is ITextBlockContent tc ? HtmlToMarkdownInline(tc.Html) : string.Empty;
        return $"{indent}- {text}";
    }

    private static string RenderNumbered(IPageBlock block, Dictionary<int, int> counters)
    {
        var indent = GetListIndent(block);
        var level  = block.Content is IListBlockContent lc ? lc.IndentLevel : 0;
        counters.TryGetValue(level, out var n);
        counters[level] = n + 1;
        var text   = block.Content is ITextBlockContent tc ? HtmlToMarkdownInline(tc.Html) : string.Empty;
        return $"{indent}{n + 1}. {text}";
    }

    private static string RenderTodo(IPageBlock block)
    {
        var text    = block.Content is ITextBlockContent tc ? HtmlToMarkdownInline(tc.Html) : string.Empty;
        var checked_ = block.Content is ITodoBlockContent td && td.IsChecked;
        return $"- [{(checked_ ? "x" : " ")}] {text}";
    }

    private static string GetListIndent(IPageBlock block)
    {
        var level = block.Content is IListBlockContent lc ? lc.IndentLevel : 0;
        return level > 0 ? new string(' ', level * 2) : string.Empty;
    }

    // ── Quote / Callout / Toggle ──────────────────────────────────────────────

    private static string RenderQuote(IPageBlock block)
    {
        var text = block.Content is ITextBlockContent tc ? HtmlToMarkdownInline(tc.Html) : string.Empty;
        return string.Join(Environment.NewLine,
            text.Split('\n').Select(l => $"> {l}"));
    }

    private static string RenderCallout(IPageBlock block)
    {
        if (block.Content is not ICalloutBlockContent cc) return string.Empty;
        var icon = cc.IconEmoji is { Length: > 0 } e ? $"{e} " : string.Empty;
        var text = HtmlToMarkdownInline(cc.Html);
        return string.Join(Environment.NewLine,
            text.Split('\n').Select((l, i) => i == 0 ? $"> {icon}{l}" : $"> {l}"));
    }

    private static string RenderToggle(IPageBlock block)
    {
        var text = block.Content is ITextBlockContent tc ? HtmlToMarkdownInline(tc.Html) : string.Empty;
        return $"**{text}**";
    }

    // ── Code block ────────────────────────────────────────────────────────────

    private static string RenderCode(IPageBlock block)
    {
        if (block.Content is not ICodeBlockContent cc) return string.Empty;
        var lang    = cc.Language ?? string.Empty;
        var caption = !string.IsNullOrEmpty(cc.Caption) ? $"\n*{EscapeMarkdown(cc.Caption)}*" : string.Empty;
        return $"```{lang}\n{cc.Code}\n```{caption}";
    }

    // ── Media ─────────────────────────────────────────────────────────────────

    private static string RenderImage(IPageBlock block)
    {
        if (block.Content is not IImageBlockContent img) return string.Empty;
        var alt     = EscapeMarkdown(img.AltText ?? img.Caption ?? string.Empty);
        var caption = !string.IsNullOrEmpty(img.Caption) ? $"\n*{EscapeMarkdown(img.Caption)}*" : string.Empty;
        return $"![{alt}]({img.Url}){caption}";
    }

    private static string RenderVideo(IPageBlock block)
    {
        if (block.Content is not IVideoBlockContent vc) return string.Empty;
        var caption = !string.IsNullOrEmpty(vc.Caption) ? $" — {EscapeMarkdown(vc.Caption)}" : string.Empty;
        return $"[Video{caption}]({vc.Url})";
    }

    private static string RenderAudio(IPageBlock block)
    {
        if (block.Content is not IAudioBlockContent ac) return string.Empty;
        var caption = !string.IsNullOrEmpty(ac.Caption) ? $" — {EscapeMarkdown(ac.Caption)}" : string.Empty;
        return $"[Audio{caption}]({ac.Url})";
    }

    private static string RenderFile(IPageBlock block)
    {
        if (block.Content is not IFileBlockContent fc) return string.Empty;
        var name = EscapeMarkdown(fc.Caption ?? fc.Url);
        return $"[{name}]({fc.Url})";
    }

    private static string RenderPdf(IPageBlock block)
    {
        if (block.Content is not IPdfBlockContent pc) return string.Empty;
        var name = EscapeMarkdown(pc.Caption ?? pc.Url);
        return $"[PDF: {name}]({pc.Url})";
    }

    // ── Bookmark / Embed ──────────────────────────────────────────────────────

    private static string RenderBookmark(IPageBlock block)
    {
        if (block.Content is not IBookmarkBlockContent bm) return string.Empty;
        var title = EscapeMarkdown(bm.Title ?? bm.Url);
        return $"[{title}]({bm.Url})";
    }

    private static string RenderEmbed(IPageBlock block)
    {
        if (block.Content is not IEmbedBlockContent em) return string.Empty;
        return $"[Embed: {em.Url}]({em.Url})";
    }

    // ── Equation ─────────────────────────────────────────────────────────────

    private static string RenderEquation(IPageBlock block)
    {
        if (block.Content is not IEquationBlockContent eq) return string.Empty;
        return $"$$\n{eq.Expression}\n$$";
    }

    // ── Table row ─────────────────────────────────────────────────────────────

    private static string RenderTableRow(IPageBlock block, bool isFirst)
    {
        if (block.Content is not ITableRowBlockContent tr) return string.Empty;

        var row = RenderCells(tr.Cells, tr.Cells.Count);

        if (isFirst)
        {
            var sep = $"| {string.Join(" | ", tr.Cells.Select(_ => "---"))} |";
            return row + Environment.NewLine + sep;
        }

        return row;
    }

    /// <summary>Renders a Table block together with its child TableRow blocks.</summary>
    private static string RenderTable(IPageBlock tableBlock, IReadOnlyList<IPageBlock> rowBlocks)
    {
        var rows = rowBlocks
            .Select(row => row.Content as ITableRowBlockContent)
            .Where(row => row is not null)
            .Select(row => row!.Cells)
            .ToList();

        if (rows.Count == 0) return string.Empty;

        var content = tableBlock.Content as ITableBlockContent;
        var columnCount = Math.Max(rows.Max(cells => cells.Count), content?.ColumnCount ?? 0);
        if (columnCount == 0) return string.Empty;

        var alignments = content?.ColumnAlignments ?? [];
        var lines = new List<string> { RenderCells(rows[0], columnCount) };

        lines.Add($"| {string.Join(" | ", Enumerable.Range(0, columnCount).Select(column => SeparatorFor(alignments, column)))} |");
        lines.AddRange(rows.Skip(1).Select(cells => RenderCells(cells, columnCount)));

        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderCells(IReadOnlyList<string> cells, int columnCount)
    {
        var rendered = Enumerable.Range(0, columnCount)
            .Select(column => column < cells.Count
                ? HtmlToMarkdownInline(cells[column]).Replace("|", "\\|")
                : string.Empty);

        return $"| {string.Join(" | ", rendered)} |";
    }

    private static string SeparatorFor(IReadOnlyList<TableColumnAlignment> alignments, int column)
        => (column < alignments.Count ? alignments[column] : TableColumnAlignment.None) switch
        {
            TableColumnAlignment.Left => ":---",
            TableColumnAlignment.Center => ":---:",
            TableColumnAlignment.Right => "---:",
            _ => "---"
        };

    // ── Page links ────────────────────────────────────────────────────────────

    private static string RenderChildPage(IPageBlock block)
    {
        if (block.Content is not IChildPageBlockContent cp) return string.Empty;
        var icon  = cp.IconEmoji is { Length: > 0 } e ? $"{e} " : "📄 ";
        return $"- {icon}{EscapeMarkdown(cp.Title)}";
    }

    private static string RenderLinkedPage(IPageBlock block)
    {
        if (block.Content is not ILinkedPageBlockContent lp) return string.Empty;
        var icon  = lp.IconEmoji is { Length: > 0 } e ? $"{e} " : "📄 ";
        return $"- {icon}{EscapeMarkdown(lp.Title)}";
    }

    // ── Inline HTML → Markdown ────────────────────────────────────────────────

    /// <summary>Converts simple inline HTML (as stored in block content) to CommonMark Markdown.</summary>
    public static string HtmlToMarkdownInline(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        var text = html;

        // links first (before stripping other tags)
        text = Regex.Replace(text,
            @"<a\s[^>]*href=""([^""]*?)""[^>]*>(.*?)</a>",
            m => $"[{HtmlToMarkdownInline(m.Groups[2].Value)}]({m.Groups[1].Value})",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // inline formatting
        text = Regex.Replace(text, @"<strong>(.*?)</strong>", "**$1**",  RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<b>(.*?)</b>",           "**$1**",  RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<em>(.*?)</em>",         "*$1*",    RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<i>(.*?)</i>",           "*$1*",    RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<code>(.*?)</code>",     "`$1`",    RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<s>(.*?)</s>",           "~~$1~~",  RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<del>(.*?)</del>",       "~~$1~~",  RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<u>(.*?)</u>",           "$1",      RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<mark[^>]*>(.*?)</mark>","==$1==",  RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<br\s*/?>",              "\n",      RegexOptions.IgnoreCase);

        // strip remaining tags
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);

        // decode HTML entities
        text = DecodeHtmlEntities(text);

        return text;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(text, @"([\\`*_{}[\]()#+\-.!|])", @"\$1");
    }

    private static string DecodeHtmlEntities(string text) =>
        text.Replace("&amp;",  "&")
            .Replace("&lt;",   "<")
            .Replace("&gt;",   ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;",  "'")
            .Replace("&nbsp;", " ");
}
