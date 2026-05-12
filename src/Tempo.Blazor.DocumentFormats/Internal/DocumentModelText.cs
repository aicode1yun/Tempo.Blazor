using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Internal;

internal static class DocumentModelText
{
    public static string GetBlockText(DocumentBlock block)
    {
        return block.Content switch
        {
            ParagraphBlockContent paragraph => GetInlineText(paragraph.Inlines),
            HeadingBlockContent heading => GetInlineText(heading.Inlines),
            ListBlockContent list => GetInlineText(list.Inlines),
            QuoteBlockContent quote => GetInlineText(quote.Inlines),
            TableBlockContent table => string.Join("\n", table.Rows.Select(row => string.Join("\t", row.Cells.Select(cell => string.Join("\n", cell.Blocks.Select(GetBlockText)))))),
            ImageBlockContent image => image.Caption ?? image.AltText ?? image.Url ?? image.AssetId ?? string.Empty,
            _ => string.Empty
        };
    }

    public static string GetInlineText(IEnumerable<InlineContent> inlines)
    {
        return string.Concat(inlines.Select(inline => inline switch
        {
            TextRun text => text.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            DocumentNoteReferenceRun note => note.DisplayMarker ?? note.NoteId,
            _ => string.Empty
        }));
    }

    public static List<InlineContent> TextInlines(string? text, params InlineMarkType[] marks)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return
        [
            new TextRun
            {
                Text = text,
                Marks = marks.Select(type => new InlineMark { Type = type }).ToList()
            }
        ];
    }

    public static DocumentBlock Paragraph(string? text, double order = 0)
    {
        return new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = order,
            Content = new ParagraphBlockContent { Inlines = TextInlines(text) }
        };
    }
}
