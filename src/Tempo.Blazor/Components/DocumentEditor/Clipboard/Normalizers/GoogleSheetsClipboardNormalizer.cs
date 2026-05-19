using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;

public sealed class GoogleSheetsClipboardNormalizer : IDocumentClipboardNormalizer
{
    /// <inheritdoc/>
    public int Priority => 80;

    /// <inheritdoc/>
    public bool CanHandle(DocumentClipboardInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.Html))
        {
            return input.Html.Contains("google-sheets-html-origin", StringComparison.Ordinal)
                || input.Html.Contains("data-sheets-value", StringComparison.Ordinal);
        }

        return !string.IsNullOrWhiteSpace(input.PlainText)
            && input.PlainText.Contains('\t');
    }

    /// <inheritdoc/>
    public DocumentClipboardOutput Normalize(DocumentClipboardInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.Html))
        {
            var cleaned = new DocumentClipboardInput
            {
                Html = input.Html,
                PlainText = input.PlainText,
                Source = DocumentClipboardSource.GoogleSheets,
                Files = input.Files
            };
            var output = new RawHtmlClipboardNormalizer().Normalize(cleaned);
            return new DocumentClipboardOutput
            {
                Blocks = output.Blocks,
                Source = DocumentClipboardSource.GoogleSheets,
                Warnings = output.Warnings
            };
        }

        return NormalizeTsv(input.PlainText!);
    }

    private static DocumentClipboardOutput NormalizeTsv(string tsv)
    {
        var lines = tsv.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var rows = lines.Select(line =>
        {
            var cells = line.Split('\t').Select(cellText => new TableCellContent
            {
                Blocks =
                [
                    new DocumentBlock
                    {
                        Content = new ParagraphBlockContent
                        {
                            Inlines = [new TextRun { Text = cellText }]
                        }
                    }
                ]
            }).ToList();
            return new TableRowContent { Cells = cells };
        }).ToList();

        var tableBlock = new DocumentBlock
        {
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent { Rows = rows }
        };

        return new DocumentClipboardOutput { Blocks = [tableBlock], Source = DocumentClipboardSource.GoogleSheets };
    }
}
