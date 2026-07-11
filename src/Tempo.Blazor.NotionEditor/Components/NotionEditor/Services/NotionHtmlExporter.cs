using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using TableColumnAlignment = Tempo.Blazor.DocumentEditor.Models.TableColumnAlignment;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Converts an ordered list of <see cref="IPageBlock"/> objects into a self-contained HTML document.
/// Each block type maps to an appropriate HTML element with embedded Notion-style CSS.
/// </summary>
public static class NotionHtmlExporter
{
    // ── Public entry point ────────────────────────────────────────────────────

    public static string Export(IReadOnlyList<IPageBlock> blocks, string pageTitle = "")
    {
        var body = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(pageTitle))
            body.AppendLine($"<h1 class=\"notion-title\">{HtmlEncode(pageTitle)}</h1>");

        var childRows = blocks
            .Where(block => block.Type == BlockType.TableRow && block.ParentBlockId.HasValue)
            .GroupBy(block => block.ParentBlockId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(block => block.Order).ToList());

        bool inUl = false, inOl = false, inTable = false, tableFirstRow = false;

        foreach (var block in blocks)
        {
            // rows owned by a Table block are rendered with their parent
            if (block.Type == BlockType.TableRow && block.ParentBlockId.HasValue)
                continue;

            // close open list containers when changing block type
            if (block.Type != BlockType.BulletList && inUl)   { body.AppendLine("</ul>"); inUl = false; }
            if (block.Type != BlockType.NumberedList && inOl) { body.AppendLine("</ol>"); inOl = false; }

            // close open legacy flat table when leaving TableRow blocks
            if (block.Type != BlockType.TableRow && inTable)
            {
                body.AppendLine("</tbody></table>");
                inTable = false;
            }

            switch (block.Type)
            {
                case BlockType.BulletList:
                    if (!inUl) { body.AppendLine("<ul>"); inUl = true; }
                    body.AppendLine(RenderBullet(block));
                    break;

                case BlockType.NumberedList:
                    if (!inOl) { body.AppendLine("<ol>"); inOl = true; }
                    body.AppendLine(RenderNumbered(block));
                    break;

                case BlockType.Table:
                    body.AppendLine(RenderTable(block, childRows.TryGetValue(block.Id, out var rows) ? rows : []));
                    break;

                case BlockType.TableRow:
                    if (!inTable) { inTable = true; tableFirstRow = true; }
                    body.AppendLine(RenderTableRow(block, tableFirstRow));
                    tableFirstRow = false;
                    break;

                default:
                    body.AppendLine(RenderBlock(block));
                    break;
            }
        }

        if (inUl) body.AppendLine("</ul>");
        if (inOl) body.AppendLine("</ol>");
        if (inTable) body.AppendLine("</tbody></table>");

        var title = HtmlEncode(pageTitle);
        return $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="UTF-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1.0">
                  <title>{title}</title>
                  <style>
                {GetCss()}
                  </style>
                </head>
                <body>
                  <article class="notion-page">
                {body.ToString().TrimEnd()}
                  </article>
                </body>
                </html>
                """;
    }

    // ── Block rendering ───────────────────────────────────────────────────────

    private static string RenderBlock(IPageBlock block) => block.Type switch
    {
        BlockType.Heading1  => RenderHeading(block, 1),
        BlockType.Heading2  => RenderHeading(block, 2),
        BlockType.Heading3  => RenderHeading(block, 3),
        BlockType.Quote     => RenderQuote(block),
        BlockType.Callout   => RenderCallout(block),
        BlockType.Toggle    => RenderToggle(block),
        BlockType.TodoItem  => RenderTodo(block),
        BlockType.Code      => RenderCode(block),
        BlockType.Divider   => "<hr>",
        BlockType.Image     => RenderImage(block),
        BlockType.Video     => RenderVideo(block),
        BlockType.Audio     => RenderAudio(block),
        BlockType.File      => RenderFile(block),
        BlockType.Pdf       => RenderPdf(block),
        BlockType.Bookmark  => RenderBookmark(block),
        BlockType.Embed     => RenderEmbed(block),
        BlockType.Equation  => RenderEquation(block),
        BlockType.Table     => string.Empty,
        BlockType.TableRow  => string.Empty,   // handled in Export loop with local state
        BlockType.ChildPage => RenderChildPage(block),
        BlockType.LinkedPage => RenderLinkedPage(block),
        _                   => RenderParagraph(block)
    };

    // ── Text blocks ───────────────────────────────────────────────────────────

    private static string RenderParagraph(IPageBlock block)
    {
        if (block.Content is not ITextBlockContent tc) return "<p></p>";
        var style = BuildTextStyle(tc);
        return $"<p class=\"notion-p\"{style}>{SanitizeInlineHtml(tc.Html)}</p>";
    }

    private static string RenderHeading(IPageBlock block, int level)
    {
        if (block.Content is not ITextBlockContent tc) return string.Empty;
        var style = BuildTextStyle(tc);
        return $"<h{level} class=\"notion-h notion-h{level}\"{style}>{SanitizeInlineHtml(tc.Html)}</h{level}>";
    }

    // ── Lists ─────────────────────────────────────────────────────────────────

    private static string RenderBullet(IPageBlock block)
    {
        if (block.Content is not ITextBlockContent tc) return "<li></li>";
        var indent = block.Content is IListBlockContent lc ? lc.IndentLevel * 20 : 0;
        var style  = indent > 0 ? $" style=\"margin-left:{indent}px\"" : string.Empty;
        return $"<li class=\"notion-li\"{style}>{SanitizeInlineHtml(tc.Html)}</li>";
    }

    private static string RenderNumbered(IPageBlock block)
    {
        if (block.Content is not ITextBlockContent tc) return "<li></li>";
        var indent = block.Content is IListBlockContent lc ? lc.IndentLevel * 20 : 0;
        var style  = indent > 0 ? $" style=\"margin-left:{indent}px\"" : string.Empty;
        return $"<li class=\"notion-li\"{style}>{SanitizeInlineHtml(tc.Html)}</li>";
    }

    private static string RenderTodo(IPageBlock block)
    {
        if (block.Content is not ITodoBlockContent td) return string.Empty;
        var check    = td.IsChecked
            ? "<input type=\"checkbox\" checked disabled>"
            : "<input type=\"checkbox\" disabled>";
        var textStyle = td.IsChecked
            ? " style=\"text-decoration:line-through;opacity:.6\""
            : string.Empty;
        return $"<div class=\"notion-todo\">{check}<span{textStyle}>{SanitizeInlineHtml(td.Html)}</span></div>";
    }

    // ── Quote / Callout / Toggle ──────────────────────────────────────────────

    private static string RenderQuote(IPageBlock block)
    {
        if (block.Content is not ITextBlockContent tc) return string.Empty;
        return $"<blockquote class=\"notion-quote\">{SanitizeInlineHtml(tc.Html)}</blockquote>";
    }

    private static string RenderCallout(IPageBlock block)
    {
        if (block.Content is not ICalloutBlockContent cc) return string.Empty;
        var icon = cc.IconEmoji is { Length: > 0 } e
            ? $"<span class=\"notion-callout__icon\" aria-hidden=\"true\">{e}</span>"
            : string.Empty;
        var bgStyle = cc.BackgroundColor is { Length: > 0 } bg
            ? $" style=\"background:{HtmlEncode(bg)}\""
            : string.Empty;
        return $"<div class=\"notion-callout\"{bgStyle}>{icon}<div class=\"notion-callout__text\">{SanitizeInlineHtml(cc.Html)}</div></div>";
    }

    private static string RenderToggle(IPageBlock block)
    {
        if (block.Content is not IToggleBlockContent tc) return string.Empty;
        var open = tc.IsOpen ? " open" : string.Empty;
        return $"<details class=\"notion-toggle\"{open}><summary>{SanitizeInlineHtml(tc.Html)}</summary></details>";
    }

    // ── Code ─────────────────────────────────────────────────────────────────

    private static string RenderCode(IPageBlock block)
    {
        if (block.Content is not ICodeBlockContent cc) return string.Empty;
        var lang    = cc.Language is { Length: > 0 } l ? $" class=\"language-{HtmlEncode(l)}\"" : string.Empty;
        var caption = cc.Caption is { Length: > 0 } c
            ? $"<figcaption class=\"notion-code-caption\">{HtmlEncode(c)}</figcaption>"
            : string.Empty;
        return $"<figure class=\"notion-code-block\">{caption}<pre><code{lang}>{HtmlEncode(cc.Code)}</code></pre></figure>";
    }

    // ── Equation ─────────────────────────────────────────────────────────────

    private static string RenderEquation(IPageBlock block)
    {
        if (block.Content is not IEquationBlockContent eq) return string.Empty;
        return $"<div class=\"notion-equation\"><code>$$ {HtmlEncode(eq.Expression)} $$</code></div>";
    }

    // ── Media ─────────────────────────────────────────────────────────────────

    private static string RenderImage(IPageBlock block)
    {
        if (block.Content is not IImageBlockContent img) return string.Empty;
        var alt     = HtmlEncode(img.AltText ?? img.Caption ?? string.Empty);
        var caption = img.Caption is { Length: > 0 } c
            ? $"<figcaption>{HtmlEncode(c)}</figcaption>"
            : string.Empty;
        return $"<figure class=\"notion-image\"><img src=\"{HtmlAttrEncode(img.Url)}\" alt=\"{alt}\"></figure>{caption}";
    }

    private static string RenderVideo(IPageBlock block)
    {
        if (block.Content is not IVideoBlockContent vc) return string.Empty;
        var caption = vc.Caption is { Length: > 0 } c
            ? $"<figcaption>{HtmlEncode(c)}</figcaption>"
            : string.Empty;
        return $"<figure class=\"notion-video\">"
             + $"<video controls src=\"{HtmlAttrEncode(vc.Url)}\"><a href=\"{HtmlAttrEncode(vc.Url)}\">{HtmlEncode(vc.Caption ?? vc.Url)}</a></video>"
             + $"</figure>{caption}";
    }

    private static string RenderAudio(IPageBlock block)
    {
        if (block.Content is not IAudioBlockContent ac) return string.Empty;
        return $"<figure class=\"notion-audio\">"
             + $"<audio controls src=\"{HtmlAttrEncode(ac.Url)}\"></audio>"
             + $"</figure>";
    }

    private static string RenderFile(IPageBlock block)
    {
        if (block.Content is not IFileBlockContent fc) return string.Empty;
        var name = HtmlEncode(fc.Caption ?? fc.Url);
        return $"<p class=\"notion-file\">📎 <a href=\"{HtmlAttrEncode(fc.Url)}\">{name}</a></p>";
    }

    private static string RenderPdf(IPageBlock block)
    {
        if (block.Content is not IPdfBlockContent pc) return string.Empty;
        var name = HtmlEncode(pc.Caption ?? pc.Url);
        return $"<p class=\"notion-pdf\">📄 <a href=\"{HtmlAttrEncode(pc.Url)}\">{name}</a></p>";
    }

    private static string RenderBookmark(IPageBlock block)
    {
        if (block.Content is not IBookmarkBlockContent bm) return string.Empty;
        var title = HtmlEncode(bm.Title ?? bm.Url);
        var desc  = bm.Description is { Length: > 0 } d
            ? $"<p class=\"notion-bookmark__desc\">{HtmlEncode(d)}</p>"
            : string.Empty;
        return $"""
                <div class="notion-bookmark">
                  <a href="{HtmlAttrEncode(bm.Url)}" class="notion-bookmark__link" target="_blank" rel="noopener">
                    <div class="notion-bookmark__body">
                      <span class="notion-bookmark__title">{title}</span>
                      {desc}
                      <span class="notion-bookmark__url">{HtmlEncode(bm.Url)}</span>
                    </div>
                  </a>
                </div>
                """;
    }

    private static string RenderEmbed(IPageBlock block)
    {
        if (block.Content is not IEmbedBlockContent em) return string.Empty;
        return $"<div class=\"notion-embed\"><a href=\"{HtmlAttrEncode(em.Url)}\" target=\"_blank\" rel=\"noopener\">{HtmlEncode(em.Url)}</a></div>";
    }

    // ── Table row ─────────────────────────────────────────────────────────────

    private static string RenderTableRow(IPageBlock block, bool isHeader)
    {
        if (block.Content is not ITableRowBlockContent tr) return string.Empty;
        var row = RenderRowCells(tr.Cells, isHeader, []);
        return isHeader ? $"<table class=\"notion-table\"><thead>{row}</thead><tbody>" : row;
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
        var hasHeaderRow = content?.HasHeaderRow ?? true;
        var alignments = content?.ColumnAlignments ?? [];
        var builder = new StringBuilder("<table class=\"notion-table\">");

        var bodyRows = rows;
        if (hasHeaderRow)
        {
            builder.Append("<thead>").Append(RenderRowCells(rows[0], isHeader: true, alignments)).Append("</thead>");
            bodyRows = [.. rows.Skip(1)];
        }

        builder.Append("<tbody>");
        foreach (var cells in bodyRows)
        {
            builder.Append(RenderRowCells(cells, isHeader: false, alignments));
        }

        return builder.Append("</tbody></table>").ToString();
    }

    private static string RenderRowCells(
        IReadOnlyList<string> cells,
        bool isHeader,
        IReadOnlyList<TableColumnAlignment> alignments)
    {
        var tag = isHeader ? "th" : "td";
        var rendered = cells.Select((cell, column) =>
            $"<{tag}{AlignmentAttribute(alignments, column)}>{SanitizeInlineHtml(cell)}</{tag}>");
        return $"<tr>{string.Join(string.Empty, rendered)}</tr>";
    }

    /// <summary>Emits the GFM column alignment inline so the markup stays self-contained.</summary>
    private static string AlignmentAttribute(IReadOnlyList<TableColumnAlignment> alignments, int column)
        => (column < alignments.Count ? alignments[column] : TableColumnAlignment.None) switch
        {
            TableColumnAlignment.Left => " style=\"text-align:left\"",
            TableColumnAlignment.Center => " style=\"text-align:center\"",
            TableColumnAlignment.Right => " style=\"text-align:right\"",
            _ => string.Empty
        };

    // ── Page links ────────────────────────────────────────────────────────────

    private static string RenderChildPage(IPageBlock block)
    {
        if (block.Content is not IChildPageBlockContent cp) return string.Empty;
        var icon = cp.IconEmoji is { Length: > 0 } e ? $"{e} " : "📄 ";
        return $"<p class=\"notion-child-page\">{icon}{HtmlEncode(cp.Title)}</p>";
    }

    private static string RenderLinkedPage(IPageBlock block)
    {
        if (block.Content is not ILinkedPageBlockContent lp) return string.Empty;
        var icon = lp.IconEmoji is { Length: > 0 } e ? $"{e} " : "📄 ";
        return $"<p class=\"notion-linked-page\">{icon}{HtmlEncode(lp.Title)}</p>";
    }

    // ── Style helpers ─────────────────────────────────────────────────────────

    private static string BuildTextStyle(ITextBlockContent tc)
    {
        var parts = new List<string>();
        if (tc.TextColor is { Length: > 0 } fg)      parts.Add($"color:{HtmlEncode(fg)}");
        if (tc.BackgroundColor is { Length: > 0 } bg) parts.Add($"background:{HtmlEncode(bg)}");
        if (tc.Alignment != TextAlignment.Left)
        {
            var align = tc.Alignment.ToString().ToLowerInvariant();
            parts.Add($"text-align:{align}");
        }
        return parts.Count > 0 ? $" style=\"{string.Join(";", parts)}\"" : string.Empty;
    }

    // ── Sanitize inline HTML stored in block content ──────────────────────────

    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
        { "strong", "b", "em", "i", "u", "s", "del", "code", "a", "br", "span", "mark" };

    private static string SanitizeInlineHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // allow only safe inline tags, strip anything else (keeps text content)
        return Regex.Replace(html, @"<(/?)(\w+)([^>]*)>", m =>
        {
            var tag = m.Groups[2].Value;
            if (!AllowedTags.Contains(tag)) return string.Empty;

            // for <a> allow only href attribute
            if (tag.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                var href = Regex.Match(m.Groups[3].Value, @"href=""([^""]*?)""");
                return href.Success
                    ? $"<{m.Groups[1].Value}a href=\"{HtmlAttrEncode(href.Groups[1].Value)}\">"
                    : string.Empty;
            }

            // for <span> allow only style attribute (color/background)
            if (tag.Equals("span", StringComparison.OrdinalIgnoreCase))
            {
                var styleAttr = Regex.Match(m.Groups[3].Value, @"style=""([^""]*?)""");
                if (styleAttr.Success)
                {
                    var safeStyle = SanitizeStyle(styleAttr.Groups[1].Value);
                    return !string.IsNullOrEmpty(safeStyle)
                        ? $"<span style=\"{safeStyle}\">"
                        : string.Empty;
                }
                return string.Empty;
            }

            return $"<{m.Groups[1].Value}{tag}>";
        });
    }

    private static string SanitizeStyle(string style)
    {
        // allow only color and background-color
        var parts = style.Split(';')
            .Select(p => p.Trim())
            .Where(p => p.StartsWith("color:", StringComparison.OrdinalIgnoreCase)
                     || p.StartsWith("background-color:", StringComparison.OrdinalIgnoreCase)
                     || p.StartsWith("background:", StringComparison.OrdinalIgnoreCase));
        return string.Join(";", parts);
    }

    // ── HTML encoding helpers ─────────────────────────────────────────────────

    private static string HtmlEncode(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");
    }

    private static string HtmlAttrEncode(string? url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        return url.Replace("\"", "&quot;").Replace("'", "&#39;");
    }

    // ── Embedded CSS ──────────────────────────────────────────────────────────

    private static string GetCss() => """
          *, *::before, *::after { box-sizing: border-box; }
          body { margin: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background: #fff; color: #37352f; line-height: 1.6; }
          .notion-page { max-width: 720px; margin: 2rem auto; padding: 0 2rem 6rem; }
          .notion-title { font-size: 2.5rem; font-weight: 700; margin: 0 0 2rem; }
          .notion-p { margin: 0.3em 0; font-size: 1rem; }
          .notion-h { font-weight: 700; margin: 1.4em 0 0.3em; }
          .notion-h1 { font-size: 1.875rem; }
          .notion-h2 { font-size: 1.5rem; }
          .notion-h3 { font-size: 1.25rem; }
          ul, ol { padding-left: 1.5rem; margin: 0.3em 0; }
          .notion-li { margin: 0.15em 0; }
          .notion-todo { display: flex; align-items: flex-start; gap: 0.5rem; margin: 0.25em 0; }
          .notion-todo input[type=checkbox] { margin-top: 0.25rem; flex-shrink: 0; }
          .notion-quote { border-left: 3px solid #37352f; margin: 0.5em 0; padding: 0.25em 0 0.25em 1rem; font-style: italic; color: #555; }
          .notion-callout { display: flex; gap: 0.75rem; background: rgba(55,53,47,0.05); border-radius: 6px; padding: 1rem; margin: 0.5em 0; }
          .notion-callout__icon { font-size: 1.25rem; flex-shrink: 0; }
          .notion-toggle summary { cursor: pointer; font-weight: 500; list-style: none; }
          .notion-toggle summary::before { content: '▶'; margin-right: 0.4rem; font-size: 0.75rem; transition: transform 0.15s; }
          .notion-toggle[open] summary::before { transform: rotate(90deg); }
          .notion-code-block { margin: 0.75em 0; }
          .notion-code-block pre { background: #f7f6f3; border-radius: 6px; padding: 1rem; overflow-x: auto; margin: 0; }
          .notion-code-block code { font-family: 'SFMono-Regular', Consolas, monospace; font-size: 0.875rem; }
          .notion-code-caption { font-size: 0.8rem; color: #888; margin-bottom: 0.25rem; }
          .notion-equation { background: #f7f6f3; border-radius: 6px; padding: 1rem; text-align: center; margin: 0.5em 0; font-family: monospace; }
          hr { border: none; border-top: 1px solid rgba(55,53,47,0.15); margin: 1.5em 0; }
          .notion-image, .notion-video { margin: 0.75em 0; text-align: center; }
          .notion-image img { max-width: 100%; border-radius: 4px; }
          .notion-video video { max-width: 100%; border-radius: 4px; }
          figcaption { font-size: 0.8rem; color: #888; text-align: center; margin-top: 0.3rem; }
          .notion-table { border-collapse: collapse; width: 100%; margin: 0.75em 0; }
          .notion-table th, .notion-table td { border: 1px solid rgba(55,53,47,0.15); padding: 0.5rem 0.75rem; text-align: left; }
          .notion-table th { background: rgba(55,53,47,0.04); font-weight: 600; }
          .notion-bookmark { border: 1px solid rgba(55,53,47,0.15); border-radius: 6px; overflow: hidden; margin: 0.75em 0; }
          .notion-bookmark__link { display: block; text-decoration: none; color: inherit; padding: 1rem; }
          .notion-bookmark__link:hover { background: rgba(55,53,47,0.03); }
          .notion-bookmark__title { font-weight: 600; display: block; }
          .notion-bookmark__desc { font-size: 0.85rem; color: #888; margin: 0.25rem 0 0; }
          .notion-bookmark__url { font-size: 0.75rem; color: #888; }
          .notion-embed { border: 1px solid rgba(55,53,47,0.15); border-radius: 6px; padding: 0.75rem 1rem; margin: 0.75em 0; }
          .notion-child-page, .notion-linked-page { font-weight: 500; }
          a { color: #0066cc; }
          code { font-family: 'SFMono-Regular', Consolas, monospace; background: rgba(135,131,120,0.15); border-radius: 3px; padding: 0.1em 0.3em; font-size: 0.875em; }
        """;
}
