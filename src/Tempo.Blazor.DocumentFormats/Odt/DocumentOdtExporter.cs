using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Internal;

namespace Tempo.Blazor.DocumentFormats.Odt;

/// <summary>Exports document editor JSON models as ODT packages.</summary>
public sealed class DocumentOdtExporter : IDocumentFormatExporter
{
    /// <inheritdoc />
    public async Task<DocumentFormatExportResult> ExportAsync(DocumentEditorDocument document, DocumentFormatExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DocumentFormatExportOptions();
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var writer = new OdtPackageWriter(archive, document, options);
            await writer.WriteAsync(cancellationToken);
        }

        return new DocumentFormatExportResult
        {
            Content = memory.ToArray(),
            ContentType = "application/vnd.oasis.opendocument.text",
            FileName = $"{SanitizeFileName(options.FileName ?? document.Metadata.Title ?? document.DocumentId)}.odt",
            Format = DocumentFormatKind.Odt
        };
    }

    private static string SanitizeFileName(string value)
    {
        var sanitized = string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
    }
}

/// <summary>Writes OpenDocument package XML from the editor model.</summary>
public sealed class OdtPackageWriter
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace Tm = "urn:tempo-blazor:document-editor:1.0";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    private readonly ZipArchive _archive;
    private readonly DocumentEditorDocument _document;
    private readonly DocumentFormatExportOptions _options;
    private readonly List<(string Path, ImageBlockContent Image)> _images = [];
    private int _imageIndex;

    /// <summary>Creates an ODT package writer.</summary>
    public OdtPackageWriter(ZipArchive archive, DocumentEditorDocument document, DocumentFormatExportOptions options)
    {
        _archive = archive;
        _document = document;
        _options = options;
    }

    /// <summary>Writes the package.</summary>
    public async Task WriteAsync(CancellationToken cancellationToken = default)
    {
        await WriteTextEntryAsync("mimetype", "application/vnd.oasis.opendocument.text", CompressionLevel.NoCompression, cancellationToken);
        await WriteTextEntryAsync("content.xml", BuildContentXml().ToString(SaveOptions.DisableFormatting), CompressionLevel.Optimal, cancellationToken);
        foreach (var image in _images)
        {
            await WriteImageEntryAsync(image.Path, image.Image, cancellationToken);
        }

        await WriteTextEntryAsync("styles.xml", BuildStylesXml().ToString(SaveOptions.DisableFormatting), CompressionLevel.Optimal, cancellationToken);
        await WriteTextEntryAsync("meta.xml", BuildMetaXml().ToString(SaveOptions.DisableFormatting), CompressionLevel.Optimal, cancellationToken);
        await WriteTextEntryAsync("META-INF/manifest.xml", BuildManifestXml().ToString(SaveOptions.DisableFormatting), CompressionLevel.Optimal, cancellationToken);
    }

    private XDocument BuildContentXml()
    {
        var body = new XElement(Office + "body", new XElement(Office + "text"));
        var textRoot = body.Element(Office + "text")!;
        foreach (var headerFooter in _document.HeadersFooters)
        {
            textRoot.Add(WriteHeaderFooter(headerFooter));
        }

        foreach (var comment in _document.Comments)
        {
            textRoot.Add(WriteComment(comment));
        }

        foreach (var block in _document.Blocks.OrderBy(block => block.Order))
        {
            foreach (var element in WriteBlock(block))
            {
                textRoot.Add(element);
            }
        }

        return new XDocument(new XElement(Office + "document-content",
            new XAttribute(XNamespace.Xmlns + "office", Office),
            new XAttribute(XNamespace.Xmlns + "text", Text),
            new XAttribute(XNamespace.Xmlns + "table", Table),
            new XAttribute(XNamespace.Xmlns + "draw", Draw),
            new XAttribute(XNamespace.Xmlns + "style", Style),
            new XAttribute(XNamespace.Xmlns + "svg", Svg),
            new XAttribute(XNamespace.Xmlns + "tm", Tm),
            new XAttribute(XNamespace.Xmlns + "xlink", XLink),
            new XAttribute(Office + "version", "1.3"),
            body));
    }

    private IEnumerable<XElement> WriteBlock(DocumentBlock block)
    {
        return block.Content switch
        {
            ParagraphBlockContent paragraph => [new XElement(Text + "p", WriteInlines(paragraph.Inlines))],
            HeadingBlockContent heading => [new XElement(Text + "h", new XAttribute(Text + "outline-level", Math.Clamp(heading.Level, 1, 6)), WriteInlines(heading.Inlines))],
            ListBlockContent list => [new XElement(Text + "list", new XAttribute(Text + "style-name", list.Ordered ? "number-list" : "bullet-list"), new XElement(Text + "list-item", new XElement(Text + "p", WriteInlines(list.Inlines))))],
            QuoteBlockContent quote => [new XElement(Text + "p", new XAttribute(Text + "style-name", "quote"), WriteInlines(quote.Inlines))],
            TableBlockContent table => [WriteTable(table)],
            ImageBlockContent image => [WriteImageParagraph(image)],
            PageBreakBlockContent => [new XElement(Text + "p", new XAttribute(Text + "style-name", "page-break"))],
            _ => []
        };
    }

    private XElement WriteTable(TableBlockContent table)
    {
        return new XElement(Table + "table",
            table.Rows.Select(row => new XElement(Table + "table-row",
                row.Cells.Select(WriteTableCell))));
    }

    private XElement WriteTableCell(TableCellContent cell)
    {
        if (!cell.Merge.IsOrigin)
        {
            return new XElement(Table + "covered-table-cell",
                string.IsNullOrWhiteSpace(cell.Merge.OriginCellId) ? null : new XAttribute(Tm + "origin-cell-id", cell.Merge.OriginCellId));
        }

        return new XElement(Table + "table-cell",
            cell.ColumnSpan > 1 ? new XAttribute(Table + "number-columns-spanned", cell.ColumnSpan) : null,
            cell.RowSpan > 1 ? new XAttribute(Table + "number-rows-spanned", cell.RowSpan) : null,
            cell.Blocks.Select(block => new XElement(Text + "p", DocumentModelText.GetBlockText(block))));
    }

    private XElement WriteHeaderFooter(DocumentHeaderFooter headerFooter)
    {
        return new XElement(Tm + "header-footer",
            new XAttribute(Tm + "id", headerFooter.Id),
            new XAttribute(Tm + "type", headerFooter.Type.ToString()),
            new XAttribute(Tm + "scope", headerFooter.Scope.ToString()),
            headerFooter.Blocks.Select(block => new XElement(Text + "p", DocumentModelText.GetBlockText(block))));
    }

    private XElement WriteComment(DocumentComment comment)
    {
        return new XElement(Tm + "comment",
            new XAttribute(Tm + "id", comment.Id),
            string.IsNullOrWhiteSpace(comment.SourceFormat) ? null : new XAttribute(Tm + "source-format", comment.SourceFormat),
            string.IsNullOrWhiteSpace(comment.ExternalId) ? null : new XAttribute(Tm + "external-id", comment.ExternalId),
            comment.Entries.Select(entry => new XElement(Tm + "entry",
                new XAttribute(Tm + "author", entry.Author.DisplayName),
                new XAttribute(Tm + "created-at", entry.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
                entry.Text)));
    }

    private XElement WriteImageParagraph(ImageBlockContent image)
    {
        var path = $"Pictures/image{++_imageIndex}.png";
        _images.Add((path, image));
        var layout = image.FloatingLayout;
        return new XElement(Text + "p",
            new XElement(Draw + "frame",
                new XAttribute(Draw + "name", image.AltText ?? $"Image {_imageIndex}"),
                new XAttribute(Text + "anchor-type", layout?.Inline == false ? "page" : "as-char"),
                layout?.Inline == false ? new XAttribute(Svg + "x", FormatLength(layout.X)) : null,
                layout?.Inline == false ? new XAttribute(Svg + "y", FormatLength(layout.Y)) : null,
                image.Size.Width is > 0 ? new XAttribute(Svg + "width", FormatLength(image.Size.Width.Value)) : null,
                image.Size.Height is > 0 ? new XAttribute(Svg + "height", FormatLength(image.Size.Height.Value)) : null,
                layout?.Inline == false ? new XAttribute(Style + "wrap", ToOdtWrap(layout.WrapMode)) : null,
                layout?.Inline == false ? new XAttribute(Draw + "z-index", layout.ZIndex) : null,
                layout?.Inline == false ? new XAttribute(Tm + "wrap-mode", layout.WrapMode.ToString()) : null,
                layout?.Inline == false ? new XAttribute(Tm + "horizontal-relative-to", layout.HorizontalRelativeTo.ToString()) : null,
                layout?.Inline == false ? new XAttribute(Tm + "vertical-relative-to", layout.VerticalRelativeTo.ToString()) : null,
                layout?.Inline == false ? new XAttribute(Tm + "lock-anchor", layout.LockAnchor ? "true" : "false") : null,
                new XElement(Draw + "image",
                    new XAttribute(XLink + "href", path),
                    new XAttribute(XLink + "type", "simple"),
                    new XAttribute(XLink + "show", "embed"),
                    new XAttribute(XLink + "actuate", "onLoad"))),
            string.IsNullOrWhiteSpace(image.Caption) ? null : new XElement(Text + "span", image.Caption));
    }

    private static string FormatLength(double value)
    {
        return FormattableString.Invariant($"{value:0.##}pt");
    }

    private static string ToOdtWrap(DocumentWrapMode wrapMode)
    {
        return wrapMode switch
        {
            DocumentWrapMode.TopBottom => "none",
            DocumentWrapMode.BehindText => "run-through",
            DocumentWrapMode.InFrontOfText => "run-through",
            _ => "parallel"
        };
    }

    private IEnumerable<object> WriteInlines(IEnumerable<InlineContent> inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is TextRun text)
            {
                var content = (object)text.Text;
                if (text.Marks.Any(mark => mark.Type == InlineMarkType.Link && mark.Link is not null))
                {
                    var link = text.Marks.First(mark => mark.Type == InlineMarkType.Link && mark.Link is not null).Link!;
                    yield return new XElement(Text + "a", new XAttribute(XLink + "href", link.Href), text.Text);
                    continue;
                }

                var style = GetStyleName(text.Marks);
                var commentId = text.Marks
                    .FirstOrDefault(mark => mark.Type == InlineMarkType.CommentAnchor && mark.CommentAnchor is not null)
                    ?.CommentAnchor?.CommentId;
                if (!string.IsNullOrWhiteSpace(style) || !string.IsNullOrWhiteSpace(commentId))
                {
                    yield return new XElement(Text + "span",
                        string.IsNullOrWhiteSpace(style) ? null : new XAttribute(Text + "style-name", style),
                        string.IsNullOrWhiteSpace(commentId) ? null : new XAttribute(Tm + "comment-id", commentId),
                        content);
                    continue;
                }

                yield return content;
            }
            else if (inline is TokenRun token)
            {
                yield return $"{{{{{token.Key}}}}}";
            }
            else if (inline is DocumentNoteReferenceRun note)
            {
                yield return new XElement(Text + (note.NoteType == DocumentNoteType.Footnote ? "note-ref" : "note-ref"), note.DisplayMarker ?? note.NoteId);
            }
        }
    }

    private static string? GetStyleName(IEnumerable<InlineMark> marks)
    {
        var names = marks.Select(mark => mark.Type switch
        {
            InlineMarkType.Bold => "bold",
            InlineMarkType.Italic => "italic",
            InlineMarkType.Underline => "underline",
            InlineMarkType.Strikethrough => "strike",
            _ => null
        }).Where(name => name is not null);
        var style = string.Join("-", names);
        return string.IsNullOrWhiteSpace(style) ? null : style;
    }

    private XDocument BuildStylesXml()
    {
        return new XDocument(new XElement(Office + "document-styles",
            new XAttribute(XNamespace.Xmlns + "office", Office),
            new XAttribute(XNamespace.Xmlns + "text", Text),
            new XAttribute(Office + "version", "1.3")));
    }

    private XDocument BuildMetaXml()
    {
        return new XDocument(new XElement(Office + "document-meta",
            new XAttribute(XNamespace.Xmlns + "office", Office),
            new XAttribute(Office + "version", "1.3"),
            new XElement(Office + "meta", new XElement("title", _document.Metadata.Title))));
    }

    private XDocument BuildManifestXml()
    {
        XNamespace manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
        return new XDocument(new XElement(manifest + "manifest",
            new XAttribute(XNamespace.Xmlns + "manifest", manifest),
            new XAttribute(manifest + "version", "1.3"),
            new XElement(manifest + "file-entry", new XAttribute(manifest + "full-path", "/"), new XAttribute(manifest + "media-type", "application/vnd.oasis.opendocument.text")),
            new XElement(manifest + "file-entry", new XAttribute(manifest + "full-path", "content.xml"), new XAttribute(manifest + "media-type", "text/xml"))));
    }

    private async Task WriteTextEntryAsync(string path, string content, CompressionLevel compressionLevel, CancellationToken cancellationToken)
    {
        var entry = _archive.CreateEntry(path, compressionLevel);
        await using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private async Task WriteImageEntryAsync(string path, ImageBlockContent image, CancellationToken cancellationToken)
    {
        var entry = _archive.CreateEntry(path, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        var bytes = await ResolveImageBytesAsync(image, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private async Task<byte[]> ResolveImageBytesAsync(ImageBlockContent image, CancellationToken cancellationToken)
    {
        if (image.Source == DocumentImageSource.Asset && !string.IsNullOrWhiteSpace(image.AssetId) && _options.ImageResolver is not null)
        {
            var resolved = await _options.ImageResolver(new DocumentFormatImageExportRequest { AssetId = image.AssetId }, cancellationToken);
            if (resolved?.Content.Length > 0)
            {
                return resolved.Content;
            }
        }

        if (!string.IsNullOrWhiteSpace(image.Url) && image.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = image.Url.IndexOf(',');
            if (comma >= 0)
            {
                return Convert.FromBase64String(image.Url[(comma + 1)..]);
            }
        }

        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x04, 0x00, 0x00, 0x00, 0xB5, 0x1C, 0x0C,
            0x02, 0x00, 0x00, 0x00, 0x0B, 0x49, 0x44, 0x41,
            0x54, 0x78, 0xDA, 0x63, 0xFC, 0xFF, 0x1F, 0x00,
            0x03, 0x03, 0x02, 0x00, 0xEF, 0xBF, 0xA7, 0xDB,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
            0xAE, 0x42, 0x60, 0x82
        ];
    }
}
