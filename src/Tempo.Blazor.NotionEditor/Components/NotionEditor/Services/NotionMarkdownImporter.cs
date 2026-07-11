using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using TableColumnAlignment = Tempo.Blazor.DocumentEditor.Models.TableColumnAlignment;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Parses a CommonMark / GFM Markdown string into an ordered list of <see cref="PageBlock"/> objects.
/// Supports: headings, paragraphs, bullet lists, numbered lists, task lists, blockquotes,
/// fenced code blocks, horizontal rules, images, block equations, tables, and inline formatting.
/// </summary>
public static class NotionMarkdownImporter
{
    // ── Public entry point ────────────────────────────────────────────────────

    public static List<IPageBlock> Import(string markdown, Guid pageId)
    {
        var blocks  = new List<IPageBlock>();
        var lines   = (markdown ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n');

        int order   = 0;
        int i       = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            // ── Fenced code block ──────────────────────────────────────────────
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var lang = line.Length > 3 ? line[3..].Trim() : string.Empty;
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].StartsWith("```", StringComparison.Ordinal))
                {
                    code.AppendLine(lines[i]);
                    i++;
                }
                i++; // skip closing ```

                // optional caption on next line as italics *caption*
                string? caption = null;
                if (i < lines.Length)
                {
                    var capMatch = Regex.Match(lines[i], @"^\*([^*]+)\*$");
                    if (capMatch.Success)
                    {
                        caption = capMatch.Groups[1].Value;
                        i++;
                    }
                }

                blocks.Add(MakeBlock(pageId, order++, BlockType.Code,
                    new CodeBlockContent
                    {
                        Code     = code.ToString().TrimEnd('\n', '\r'),
                        Language = string.IsNullOrWhiteSpace(lang) ? null : lang,
                        Caption  = caption
                    }));
                continue;
            }

            // ── Block equation $$...$$  ────────────────────────────────────────
            if (line.TrimStart().StartsWith("$$", StringComparison.Ordinal) && line.Trim() == "$$")
            {
                var expr = new StringBuilder();
                i++;
                while (i < lines.Length && lines[i].Trim() != "$$")
                {
                    expr.AppendLine(lines[i]);
                    i++;
                }
                i++; // skip closing $$

                blocks.Add(MakeBlock(pageId, order++, BlockType.Equation,
                    new EquationBlockContent { Expression = expr.ToString().Trim() }));
                continue;
            }

            // ── GFM Table (outer pipes optional) ───────────────────────────────
            if (ContainsUnescapedPipe(line) && i + 1 < lines.Length && IsSeparatorRow(lines[i + 1]))
            {
                var headerCells = ParseTableRow(line);
                var columnCount = headerCells.Count;
                var alignments = ParseColumnAlignments(lines[i + 1], columnCount);
                i += 2; // skip header + separator

                // The rows are children of this Table block, matching TmNotionTableBlock's live model.
                var tableBlock = MakeBlock(pageId, order++, BlockType.Table, new TableBlockContent
                {
                    ColumnCount = columnCount,
                    HasHeaderRow = true,
                    ColumnAlignments = alignments
                });
                blocks.Add(tableBlock);

                var rowOrder = 0;
                blocks.Add(MakeRowBlock(pageId, tableBlock.Id, rowOrder++, headerCells));

                while (i < lines.Length && ContainsUnescapedPipe(lines[i]))
                {
                    var cells = NormalizeCells(ParseTableRow(lines[i]), columnCount);
                    blocks.Add(MakeRowBlock(pageId, tableBlock.Id, rowOrder++, cells));
                    i++;
                }
                continue;
            }

            // ── Horizontal rule ────────────────────────────────────────────────
            if (Regex.IsMatch(line, @"^(-{3,}|\*{3,}|_{3,})\s*$"))
            {
                blocks.Add(MakeBlock(pageId, order++, BlockType.Divider, new DividerBlockContent()));
                i++;
                continue;
            }

            // ── Headings ──────────────────────────────────────────────────────
            var headingMatch = Regex.Match(line, @"^(#{1,3})\s+(.+)$");
            if (headingMatch.Success)
            {
                var level = headingMatch.Groups[1].Length;
                var type  = level == 1 ? BlockType.Heading1
                          : level == 2 ? BlockType.Heading2
                          :              BlockType.Heading3;
                var html  = InlineMarkdownToHtml(headingMatch.Groups[2].Value.Trim());
                blocks.Add(MakeBlock(pageId, order++, type,
                    new HeadingBlockContent { Html = html, Level = level }));
                i++;
                continue;
            }

            // ── Block image  ![alt](url) ──────────────────────────────────────
            var imgMatch = Regex.Match(line, @"^!\[([^\]]*)\]\(([^)]+)\)\s*$");
            if (imgMatch.Success && NotionUrlSanitizer.IsSafe(imgMatch.Groups[2].Value))
            {
                blocks.Add(MakeBlock(pageId, order++, BlockType.Image,
                    new ImageBlockContent
                    {
                        AltText = imgMatch.Groups[1].Value,
                        Url     = imgMatch.Groups[2].Value,
                        Caption = imgMatch.Groups[1].Value
                    }));
                i++;
                continue;
            }

            // ── Blockquote > ──────────────────────────────────────────────────
            if (line.StartsWith("> ", StringComparison.Ordinal) || line == ">")
            {
                var quoteLines = new List<string>();
                while (i < lines.Length && (lines[i].StartsWith("> ", StringComparison.Ordinal) || lines[i] == ">"))
                {
                    quoteLines.Add(lines[i].Length > 2 ? lines[i][2..] : string.Empty);
                    i++;
                }
                // Convert each line separately; joining first would encode the <br> separator.
                var quoteHtml = string.Join("<br>", quoteLines.Select(InlineMarkdownToHtml));
                blocks.Add(MakeBlock(pageId, order++, BlockType.Quote,
                    new TextBlockContent { Html = quoteHtml }));
                continue;
            }

            // ── Task list item - [ ] / - [x] ──────────────────────────────────
            var taskMatch = Regex.Match(line, @"^(\s*)- \[([ xX])\] (.*)$");
            if (taskMatch.Success)
            {
                var checked_ = taskMatch.Groups[2].Value.ToUpperInvariant() == "X";
                var html     = InlineMarkdownToHtml(taskMatch.Groups[3].Value);
                blocks.Add(MakeBlock(pageId, order++, BlockType.TodoItem,
                    new TodoBlockContent { IsChecked = checked_, Html = html }));
                i++;
                continue;
            }

            // ── Bullet list - / * / + ──────────────────────────────────────────
            var bulletMatch = Regex.Match(line, @"^(\s*)[-*+] (.+)$");
            if (bulletMatch.Success)
            {
                var indent  = bulletMatch.Groups[1].Length / 2;
                var html    = InlineMarkdownToHtml(bulletMatch.Groups[2].Value);
                blocks.Add(MakeBlock(pageId, order++, BlockType.BulletList,
                    new ListBlockContent { Html = html, IndentLevel = indent }));
                i++;
                continue;
            }

            // ── Numbered list 1. 2. … ──────────────────────────────────────────
            var numberedMatch = Regex.Match(line, @"^(\s*)\d+\. (.+)$");
            if (numberedMatch.Success)
            {
                var indent  = numberedMatch.Groups[1].Length / 2;
                var html    = InlineMarkdownToHtml(numberedMatch.Groups[2].Value);
                blocks.Add(MakeBlock(pageId, order++, BlockType.NumberedList,
                    new ListBlockContent { Html = html, IndentLevel = indent }));
                i++;
                continue;
            }

            // ── Empty line → skip ──────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // ── Paragraph (collect consecutive non-empty lines) ───────────────
            {
                var para = new StringBuilder();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i])
                    && !lines[i].StartsWith("#", StringComparison.Ordinal)
                    && !lines[i].StartsWith(">", StringComparison.Ordinal)
                    && !lines[i].StartsWith("- ", StringComparison.Ordinal)
                    && !lines[i].StartsWith("* ", StringComparison.Ordinal)
                    && !lines[i].StartsWith("+ ", StringComparison.Ordinal)
                    && !lines[i].StartsWith("```", StringComparison.Ordinal)
                    && !lines[i].StartsWith("$$", StringComparison.Ordinal)
                    && !Regex.IsMatch(lines[i], @"^\d+\. "))
                {
                    if (para.Length > 0) para.Append("<br>");
                    para.Append(InlineMarkdownToHtml(lines[i]));
                    i++;
                }

                if (para.Length > 0)
                    blocks.Add(MakeBlock(pageId, order++, BlockType.Paragraph,
                        new TextBlockContent { Html = para.ToString() }));
            }
        }

        return blocks;
    }

    // ── Inline Markdown → HTML ────────────────────────────────────────────────

    /// <summary>
    /// Converts CommonMark inline elements to HTML for storage in block content.
    /// Everything that is not generated markup is HTML-encoded, and link/image URLs are
    /// restricted to http, https, mailto and relative targets.
    /// </summary>
    public static string InlineMarkdownToHtml(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // The placeholder marker must never originate from user input.
        text = text.Replace(PlaceholderMarker, string.Empty, StringComparison.Ordinal);

        var generated = new List<string>();

        // Code spans first: their content must not be interpreted as markdown.
        text = Regex.Replace(text, @"`([^`]+)`",
            m => Stash(generated, $"<code>{HtmlEncode(m.Groups[1].Value)}</code>"),
            RegexOptions.Singleline);

        text = Regex.Replace(text, @"!\[([^\]]*)\]\(([^)]+)\)",
            m => Stash(generated, RenderImage(m.Groups[1].Value, m.Groups[2].Value)),
            RegexOptions.Singleline);

        text = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)",
            m => Stash(generated, RenderLink(m.Groups[1].Value, m.Groups[2].Value)),
            RegexOptions.Singleline);

        // Any remaining raw text is untrusted and must not survive as markup.
        text = HtmlEncode(text);
        text = ApplyEmphasis(text);

        // line break (two trailing spaces)
        text = Regex.Replace(text, @"  $", "<br>", RegexOptions.Multiline);

        return Unstash(text, generated);
    }

    private static string ApplyEmphasis(string text)
    {
        // bold + italic ***...***
        text = Regex.Replace(text, @"\*\*\*(.+?)\*\*\*", "<strong><em>$1</em></strong>", RegexOptions.Singleline);
        text = Regex.Replace(text, @"___(.+?)___",        "<strong><em>$1</em></strong>", RegexOptions.Singleline);

        // bold **...**
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>", RegexOptions.Singleline);
        text = Regex.Replace(text, @"__(.+?)__",     "<strong>$1</strong>", RegexOptions.Singleline);

        // italic *...* / _..._  (not preceded/followed by another * / _)
        text = Regex.Replace(text, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "<em>$1</em>", RegexOptions.Singleline);
        text = Regex.Replace(text, @"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)",       "<em>$1</em>", RegexOptions.Singleline);

        // strikethrough ~~...~~
        text = Regex.Replace(text, @"~~(.+?)~~", "<s>$1</s>", RegexOptions.Singleline);

        // highlight ==...==
        text = Regex.Replace(text, @"==(.+?)==", "<mark>$1</mark>", RegexOptions.Singleline);

        return text;
    }

    private static string RenderLink(string label, string url)
    {
        var safeLabel = ApplyEmphasis(HtmlEncode(label));
        return NotionUrlSanitizer.IsSafe(url)
            ? $"<a href=\"{HtmlAttrEncode(url)}\">{safeLabel}</a>"
            : safeLabel;
    }

    private static string RenderImage(string alt, string url)
        => NotionUrlSanitizer.IsSafe(url)
            ? $"<img src=\"{HtmlAttrEncode(url)}\" alt=\"{HtmlEncode(alt)}\">"
            : HtmlEncode(alt);

    // ── Placeholder plumbing (keeps generated markup out of the encoder) ──────

    private const string PlaceholderMarker = "\u0001";

    private static string Stash(List<string> generated, string markup)
    {
        generated.Add(markup);
        return $"{PlaceholderMarker}{generated.Count - 1}{PlaceholderMarker}";
    }

    private static string Unstash(string text, List<string> generated)
    {
        for (var index = 0; index < generated.Count; index++)
        {
            text = text.Replace(
                $"{PlaceholderMarker}{index}{PlaceholderMarker}",
                generated[index],
                StringComparison.Ordinal);
        }

        return text;
    }

    // ── Table helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// A delimiter row is a pipe-delimited line whose every cell is a run of dashes with optional
    /// colon anchors. Requiring a pipe is what separates a single-column delimiter (<c>| --- |</c>)
    /// from a thematic break (<c>---</c>).
    /// </summary>
    private static bool IsSeparatorRow(string line)
    {
        var trimmed = line.Trim();
        if (!ContainsUnescapedPipe(trimmed))
        {
            return false;
        }

        var cells = SplitCells(trimmed);
        return cells.Count > 0 && cells.All(cell => Regex.IsMatch(cell.Trim(), @"^:?-+:?$"));
    }

    /// <summary>Outer pipes are optional in GFM; only an unescaped pipe delimits cells.</summary>
    private static bool ContainsUnescapedPipe(string line)
    {
        var escaped = false;
        foreach (var ch in line)
        {
            if (escaped) { escaped = false; continue; }
            if (ch == '\\') { escaped = true; continue; }
            if (ch == '|') return true;
        }

        return false;
    }

    private static List<string> SplitCells(string line)
    {
        var trimmed = line.Trim();
        var cells = new List<string>();
        var current = new StringBuilder();
        var escaped = false;
        var endedOnDelimiter = false;

        foreach (var ch in trimmed)
        {
            if (escaped)
            {
                current.Append(ch);
                escaped = false;
                endedOnDelimiter = false;
                continue;
            }

            if (ch == '\\') { escaped = true; continue; }

            if (ch == '|')
            {
                cells.Add(current.ToString());
                current.Clear();
                endedOnDelimiter = true;
                continue;
            }

            current.Append(ch);
            endedOnDelimiter = false;
        }

        cells.Add(current.ToString());

        if (endedOnDelimiter && cells.Count > 1) cells.RemoveAt(cells.Count - 1);
        if (cells.Count > 1 && trimmed.StartsWith('|')) cells.RemoveAt(0);

        return cells;
    }

    private static List<string> ParseTableRow(string line)
        => SplitCells(line).Select(cell => InlineMarkdownToHtml(cell.Trim())).ToList();

    private static List<TableColumnAlignment> ParseColumnAlignments(string separatorLine, int columnCount)
    {
        var alignments = SplitCells(separatorLine).Select(ParseColumnAlignment).ToList();

        if (alignments.Count > columnCount) return [.. alignments.Take(columnCount)];
        while (alignments.Count < columnCount) alignments.Add(TableColumnAlignment.None);
        return alignments;
    }

    private static TableColumnAlignment ParseColumnAlignment(string separatorCell)
    {
        var cell = separatorCell.Trim();
        var left = cell.StartsWith(':');
        var right = cell.EndsWith(':') && cell.Length > 1;

        return (left, right) switch
        {
            (true, true) => TableColumnAlignment.Center,
            (true, false) => TableColumnAlignment.Left,
            (false, true) => TableColumnAlignment.Right,
            _ => TableColumnAlignment.None
        };
    }

    private static List<string> NormalizeCells(List<string> cells, int columnCount)
    {
        if (cells.Count > columnCount) return [.. cells.Take(columnCount)];
        while (cells.Count < columnCount) cells.Add(string.Empty);
        return cells;
    }

    private static IPageBlock MakeRowBlock(Guid pageId, Guid tableId, int order, List<string> cells)
    {
        var row = (PageBlock)MakeBlock(pageId, order, BlockType.TableRow, new TableRowBlockContent { Cells = cells });
        row.ParentBlockId = tableId;
        return row;
    }

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

    // ── HTML encode helpers ───────────────────────────────────────────────────

    private static string HtmlEncode(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty
            : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string HtmlAttrEncode(string? url) =>
        string.IsNullOrEmpty(url) ? string.Empty
            : HtmlEncode(url).Replace("'", "&#39;");
}
