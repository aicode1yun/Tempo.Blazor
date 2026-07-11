using System.Text;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Internal;

namespace Tempo.Blazor.DocumentFormats.Markdown;

/// <summary>Exports a <see cref="DocumentEditorDocument"/> to Markdown.</summary>
public sealed class DocumentMarkdownExporter
{
    /// <summary>Exports a document to Markdown text.</summary>
    public string Export(DocumentEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var markdown = new StringBuilder();
        foreach (var block in document.Blocks.OrderBy(block => block.Order))
        {
            AppendBlock(markdown, block);
        }

        return markdown.ToString().TrimEnd();
    }

    private static void AppendBlock(StringBuilder markdown, DocumentBlock block)
    {
        switch (block.Content)
        {
            case ParagraphBlockContent paragraph:
                markdown.Append(RenderInlines(paragraph.Inlines)).AppendLine().AppendLine();
                break;
            case HeadingBlockContent heading:
                markdown.Append(new string('#', Math.Clamp(heading.Level, 1, 6)))
                    .Append(' ')
                    .Append(RenderInlines(heading.Inlines))
                    .AppendLine()
                    .AppendLine();
                break;
            case ListBlockContent list:
                markdown.Append(list.Ordered ? $"{Math.Max(1, list.StartNumber)}. " : "- ")
                    .Append(list.IsChecked switch { true => "[x] ", false => "[ ] ", null => string.Empty })
                    .Append(RenderInlines(list.Inlines))
                    .AppendLine();
                break;
            case QuoteBlockContent quote:
                markdown.Append("> ")
                    .Append(RenderInlines(quote.Inlines))
                    .AppendLine()
                    .AppendLine();
                break;
            case TableBlockContent table:
                AppendTable(markdown, table);
                break;
            case ImageBlockContent image:
                var alt = EscapeText(image.AltText ?? image.Caption ?? "image");
                var source = image.Url ?? image.AssetId ?? string.Empty;
                markdown.Append("![").Append(alt).Append("](").Append(EscapeUrl(source)).Append(')').AppendLine().AppendLine();
                break;
            case CodeBlockContent code:
                AppendCode(markdown, code);
                break;
            case PageBreakBlockContent:
                markdown.AppendLine("---").AppendLine();
                break;
            default:
                markdown.Append(EscapeText(DocumentModelText.GetBlockText(block))).AppendLine().AppendLine();
                break;
        }
    }

    private static void AppendTable(StringBuilder markdown, TableBlockContent table)
    {
        if (table.Rows.Count == 0)
        {
            return;
        }

        var columnCount = table.Rows.Max(row => row.Cells.Count);
        if (columnCount == 0)
        {
            return;
        }

        AppendTableRow(markdown, table.Rows[0], columnCount);

        markdown.Append("| ");
        markdown.Append(string.Join(" | ", Enumerable.Range(0, columnCount).Select(column => SeparatorFor(table, column))));
        markdown.AppendLine(" |");

        foreach (var row in table.Rows.Skip(1))
        {
            AppendTableRow(markdown, row, columnCount);
        }

        markdown.AppendLine();
    }

    private static void AppendTableRow(StringBuilder markdown, TableRowContent row, int columnCount)
    {
        var cells = Enumerable.Range(0, columnCount)
            .Select(column => column < row.Cells.Count ? RenderTableCell(row.Cells[column]) : string.Empty);

        markdown.Append("| ");
        markdown.Append(string.Join(" | ", cells));
        markdown.AppendLine(" |");
    }

    private static string RenderTableCell(TableCellContent cell)
        => EscapeText(string.Join(' ', cell.Blocks.Select(DocumentModelText.GetBlockText)));

    private static string SeparatorFor(TableBlockContent table, int column)
    {
        var alignment = column < table.ColumnAlignments.Count
            ? table.ColumnAlignments[column]
            : TableColumnAlignment.None;

        return alignment switch
        {
            TableColumnAlignment.Left => ":---",
            TableColumnAlignment.Center => ":---:",
            TableColumnAlignment.Right => "---:",
            _ => "---"
        };
    }

    /// <summary>
    /// Writes a fenced block. The fence grows past the longest backtick run inside the code, so a
    /// snippet that itself contains ``` still round-trips.
    /// </summary>
    private static void AppendCode(StringBuilder markdown, CodeBlockContent code)
    {
        var longestRun = 0;
        var current = 0;
        foreach (var ch in code.Code)
        {
            current = ch == '`' ? current + 1 : 0;
            longestRun = Math.Max(longestRun, current);
        }

        var fence = new string('`', Math.Max(3, longestRun + 1));
        markdown.Append(fence).AppendLine(code.Language ?? string.Empty)
            .AppendLine(code.Code)
            .AppendLine(fence)
            .AppendLine();
    }

    private static string RenderInlines(IEnumerable<InlineContent> inlines)
    {
        var rendered = new StringBuilder();
        foreach (var inline in inlines)
        {
            // Inside inline code every character is literal, so escaping would emit the backslashes.
            var isCode = inline.Marks.Any(mark => mark.Type == InlineMarkType.FontFamily);

            var text = inline switch
            {
                TextRun run => isCode ? run.Text : EscapeText(run.Text),
                TokenRun token => "{{" + EscapeText(token.Key) + "}}",
                DocumentNoteReferenceRun note => "[^" + EscapeText(note.NoteId) + "]",
                DocumentDrawingRun drawing => RenderDrawing(drawing),
                DocumentSigningFieldRun signing => EscapeText(Internal.SigningFieldPlaceholder.Text(signing)),
                _ => string.Empty
            };

            rendered.Append(ApplyMarks(text, inline.Marks));
        }

        return rendered.ToString();
    }

    private static string RenderDrawing(DocumentDrawingRun drawing)
    {
        var alt = EscapeText(drawing.AltText ?? drawing.Caption ?? "image");
        var source = drawing.Url ?? drawing.AssetId ?? string.Empty;
        return "![" + alt + "](" + EscapeUrl(source) + ")";
    }

    private static string ApplyMarks(string text, IReadOnlyList<InlineMark> marks)
    {
        foreach (var mark in marks)
        {
            text = mark.Type switch
            {
                InlineMarkType.Bold => "**" + text + "**",
                InlineMarkType.Italic => "*" + text + "*",
                InlineMarkType.Strikethrough => "~~" + text + "~~",
                InlineMarkType.FontFamily => "`" + text + "`",
                InlineMarkType.Link when mark.Link is not null && IsSafeMarkdownUrl(mark.Link.Href) => "[" + text + "](" + EscapeUrl(mark.Link.Href) + ")",
                _ => text
            };
        }

        return text;
    }

    private static string EscapeText(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '#' or '|' or '~')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string EscapeUrl(string value) => value.Replace(")", "%29", StringComparison.Ordinal);

    private static bool IsSafeMarkdownUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "mailto";
    }
}
