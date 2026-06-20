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
            ContentControlBlockContent control => string.Join("\n", control.Blocks.Select(GetBlockText)),
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
            DocumentFieldRun field => GetFieldText(field),
            DocumentMathRun math => math.AltText ?? DocumentMathText.FlattenMathContent(math.Content),
            DocumentContentControlRun control => GetContentControlText(control),
            DocumentNoteReferenceRun note => note.DisplayMarker ?? note.NoteId,
            DocumentDrawingRun drawing => GetDrawingText(drawing),
            _ => string.Empty
        }));
    }

    private static string GetFieldText(DocumentFieldRun field)
        => field.CachedResult
            ?? field.DisplayText
            ?? field.FallbackText
            ?? field.SequenceLabel
            ?? field.TargetId
            ?? field.CitationId
            ?? field.InstrText
            ?? field.FieldType.ToString();

    private static string GetContentControlText(DocumentContentControlRun control)
    {
        var inlineText = GetInlineText(control.Inlines);
        if (!string.IsNullOrWhiteSpace(inlineText))
        {
            return inlineText;
        }

        return GetContentControlValueText(control.Control);
    }

    private static string GetContentControlValueText(DocumentContentControl control)
    {
        if (!string.IsNullOrWhiteSpace(control.Value.Text))
        {
            return control.Value.Text;
        }

        if (!string.IsNullOrWhiteSpace(control.Value.SelectedValue))
        {
            return control.Items.FirstOrDefault(item => item.Value == control.Value.SelectedValue)?.DisplayText
                ?? control.Value.SelectedValue;
        }

        if (control.Value.Checked.HasValue)
        {
            return control.Value.Checked.Value ? "Yes" : "No";
        }

        return control.Value.DateIso
            ?? control.Value.AssetId
            ?? control.PlaceholderText
            ?? control.Alias
            ?? control.Tag
            ?? control.ControlId
            ?? string.Empty;
    }

    private static string GetDrawingText(DocumentDrawingRun drawing)
        => string.IsNullOrWhiteSpace(drawing.Caption)
            ? string.IsNullOrWhiteSpace(drawing.AltText) ? "[Image]" : $"[Image] {drawing.AltText}"
            : $"[Image] {drawing.Caption}";

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
