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

        var firstRow = table.Rows[0];
        markdown.Append("| ");
        markdown.Append(string.Join(" | ", firstRow.Cells.Select(cell => EscapeText(string.Join(' ', cell.Blocks.Select(DocumentModelText.GetBlockText))))));
        markdown.AppendLine(" |");
        markdown.Append("| ");
        markdown.Append(string.Join(" | ", firstRow.Cells.Select(_ => "---")));
        markdown.AppendLine(" |");

        foreach (var row in table.Rows.Skip(1))
        {
            markdown.Append("| ");
            markdown.Append(string.Join(" | ", row.Cells.Select(cell => EscapeText(string.Join(' ', cell.Blocks.Select(DocumentModelText.GetBlockText))))));
            markdown.AppendLine(" |");
        }

        markdown.AppendLine();
    }

    private static string RenderInlines(IEnumerable<InlineContent> inlines)
    {
        var rendered = new StringBuilder();
        foreach (var inline in inlines)
        {
            var text = inline switch
            {
                TextRun run => EscapeText(run.Text),
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
