using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

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

            // ── GFM Table (line contains |) ────────────────────────────────────
            if (line.Contains('|') && i + 1 < lines.Length && IsSeparatorRow(lines[i + 1]))
            {
                var headerCells = ParseTableRow(line);
                i += 2; // skip header + separator

                // header row
                blocks.Add(MakeBlock(pageId, order++, BlockType.TableRow,
                    new TableRowBlockContent { Cells = headerCells }));

                while (i < lines.Length && lines[i].Contains('|'))
                {
                    var cells = ParseTableRow(lines[i]);
                    blocks.Add(MakeBlock(pageId, order++, BlockType.TableRow,
                        new TableRowBlockContent { Cells = cells }));
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
            if (imgMatch.Success)
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
                var quoteHtml = InlineMarkdownToHtml(string.Join("<br>", quoteLines));
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

    /// <summary>Converts CommonMark inline elements to HTML for storage in block content.</summary>
    public static string InlineMarkdownToHtml(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // links and images first
        text = Regex.Replace(text,
            @"!\[([^\]]*)\]\(([^)]+)\)",
            m => $"<img src=\"{HtmlAttrEncode(m.Groups[2].Value)}\" alt=\"{HtmlEncode(m.Groups[1].Value)}\">",
            RegexOptions.Singleline);

        text = Regex.Replace(text,
            @"\[([^\]]+)\]\(([^)]+)\)",
            m => $"<a href=\"{HtmlAttrEncode(m.Groups[2].Value)}\">{m.Groups[1].Value}</a>",
            RegexOptions.Singleline);

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

        // inline code `...`
        text = Regex.Replace(text, @"`([^`]+)`",
            m => $"<code>{HtmlEncode(m.Groups[1].Value)}</code>",
            RegexOptions.Singleline);

        // line break (two spaces or explicit \n)
        text = Regex.Replace(text, @"  $", "<br>", RegexOptions.Multiline);

        return text;
    }

    // ── Table helpers ─────────────────────────────────────────────────────────

    private static bool IsSeparatorRow(string line) =>
        Regex.IsMatch(line.Trim(), @"^\|?(\s*:?-+:?\s*\|)+\s*:?-+:?\s*\|?$");

    private static List<string> ParseTableRow(string line)
    {
        var trimmed = line.Trim().Trim('|');
        return trimmed.Split('|')
            .Select(c => InlineMarkdownToHtml(c.Trim()))
            .ToList();
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
            : url.Replace("\"", "&quot;").Replace("'", "&#39;");
}
