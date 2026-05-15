using System.Text;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Server-side demo implementation of the document PDF export provider boundary.</summary>
public sealed class DemoDocumentPdfExportProvider : IDocumentPdfExportProvider
{
    /// <inheritdoc />
    public Task<DocumentPdfExportResult> ExportPdfAsync(
        DocumentPdfExportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileName = EnsurePdfFileName(request.FileName, request.DocumentId);
        var content = RenderPdf(request);
        return Task.FromResult(new DocumentPdfExportResult
        {
            Content = content,
            ContentType = "application/pdf",
            FileName = fileName
        });
    }

    private static byte[] RenderPdf(DocumentPdfExportRequest request)
    {
        var options = request.Options ?? new DocumentPdfExportOptions();
        var setup = options.PageSetup ?? new DocumentPdfPageSetupOptions();
        var pageSize = setup.PageSize ?? DocumentPageSize.A4;
        var width = pageSize.Width <= 0 ? DocumentPageSize.A4.Width : pageSize.Width;
        var height = pageSize.Height <= 0 ? DocumentPageSize.A4.Height : pageSize.Height;
        if (setup.Orientation == DocumentPdfPageOrientation.Landscape && height > width)
        {
            (width, height) = (height, width);
        }

        var margins = setup.Margins ?? DocumentPageMargins.Default;
        var lines = BuildTextLines(request, options).Take(42).ToList();
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 12 Tf");
        content.AppendLine("14 TL");
        content.Append(CultureInvariant("1 0 0 1 {0} {1} Tm", margins.Left, height - margins.Top));
        content.AppendLine();
        foreach (var line in lines)
        {
            content.Append('(').Append(EscapePdfText(line)).AppendLine(") Tj");
            content.AppendLine("T*");
        }

        content.AppendLine("ET");
        var contentBytes = Encoding.ASCII.GetBytes(content.ToString());

        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            CultureInvariant("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0} {1}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>", width, height),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {contentBytes.Length} >>\nstream\n{content}endstream"
        };

        var pdf = new StringBuilder();
        var offsets = new List<int>();
        pdf.AppendLine("%PDF-1.4");
        pdf.AppendLine("% Tempo.Blazor demo PDF export");
        foreach (var (body, index) in objects.Select((body, index) => (body, index)))
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(index + 1).AppendLine(" 0 obj");
            pdf.AppendLine(body);
            pdf.AppendLine("endobj");
        }

        var startXref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 ").Append(objects.Length + 1).AppendLine();
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets)
        {
            pdf.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(" 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.Append("<< /Size ").Append(objects.Length + 1).AppendLine(" /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(startXref.ToString(System.Globalization.CultureInfo.InvariantCulture));
        pdf.AppendLine("%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static IEnumerable<string> BuildTextLines(DocumentPdfExportRequest request, DocumentPdfExportOptions options)
    {
        var document = request.Document;
        if (!string.IsNullOrWhiteSpace(document.Metadata.Title))
        {
            yield return document.Metadata.Title;
            yield return string.Empty;
        }

        foreach (var block in document.Blocks.OrderBy(block => block.Order))
        {
            foreach (var line in GetBlockLines(block))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return line;
                }
            }
        }

        if (options.IncludeComments && document.Comments.Count > 0)
        {
            yield return string.Empty;
            yield return $"Comments: {document.Comments.Count}";
        }

        if (options.IncludeSuggestions && document.Revisions.Count > 0)
        {
            yield return $"Revisions: {document.Revisions.Count}";
        }
    }

    private static IEnumerable<string> GetBlockLines(DocumentBlock block)
    {
        switch (block.Content)
        {
            case ParagraphBlockContent paragraph:
                yield return InlineText(paragraph.Inlines);
                break;
            case HeadingBlockContent heading:
                yield return InlineText(heading.Inlines);
                break;
            case ListBlockContent list:
                yield return $"{(list.Ordered ? $"{list.StartNumber}." : "-")} {InlineText(list.Inlines)}";
                break;
            case QuoteBlockContent quote:
                yield return $"> {InlineText(quote.Inlines)}";
                break;
            case TableBlockContent table:
                foreach (var row in table.Rows)
                {
                    yield return string.Join(" | ", row.Cells.Select(cell =>
                        string.Join(" ", cell.Blocks.SelectMany(GetBlockLines))));
                }

                break;
            case ImageBlockContent image:
                yield return string.IsNullOrWhiteSpace(image.AltText) ? "[Image]" : $"[Image] {image.AltText}";
                break;
            case PageBreakBlockContent:
                yield return "[Page break]";
                break;
        }
    }

    private static string InlineText(IEnumerable<InlineContent> inlines)
    {
        return string.Concat(inlines.Select(inline => inline switch
        {
            TextRun text => text.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.FallbackText) ? token.DisplayName : token.FallbackText,
            DocumentNoteReferenceRun note => note.DisplayMarker ?? note.NoteId,
            _ => string.Empty
        }));
    }

    private static string EnsurePdfFileName(string? requestedName, string documentId)
    {
        var name = string.IsNullOrWhiteSpace(requestedName) ? documentId : requestedName;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '-');
        }

        return name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{name}.pdf";
    }

    private static string EscapePdfText(string value)
    {
        var normalized = value.ReplaceLineEndings(" ");
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            builder.Append(ch switch
            {
                '(' => "\\(",
                ')' => "\\)",
                '\\' => "\\\\",
                >= ' ' and <= '~' => ch.ToString(),
                _ => '?'
            });
        }

        return builder.ToString();
    }

    private static string CultureInvariant(string format, params object[] args)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
}
