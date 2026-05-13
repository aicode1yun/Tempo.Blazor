using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using System.Globalization;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Internal;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Tempo.Blazor.DocumentFormats.Docx;

/// <summary>Exports document editor JSON models as DOCX packages.</summary>
public sealed class DocumentDocxExporter : IDocumentFormatExporter
{
    /// <inheritdoc />
    public async Task<DocumentFormatExportResult> ExportAsync(DocumentEditorDocument document, DocumentFormatExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DocumentFormatExportOptions();
        using var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var writer = new DocxPackageWriter(word, document, options);
            await writer.WriteAsync(cancellationToken);
        }

        return new DocumentFormatExportResult
        {
            Content = memory.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName = $"{SanitizeFileName(options.FileName ?? document.Metadata.Title ?? document.DocumentId)}.docx",
            Format = DocumentFormatKind.Docx
        };
    }

    private static string SanitizeFileName(string value)
    {
        var sanitized = string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
    }
}

/// <summary>Writes an editor document to a WordprocessingML package.</summary>
public sealed class DocxPackageWriter
{
    private static readonly byte[] TransparentPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x04, 0x00, 0x00, 0x00, 0xB5, 0x1C, 0x0C,
        0x02, 0x00, 0x00, 0x00, 0x0B, 0x49, 0x44, 0x41, 0x54, 0x78, 0xDA, 0x63, 0xFC, 0xFF, 0x1F, 0x00,
        0x03, 0x03, 0x02, 0x00, 0xEF, 0xBF, 0xA7, 0xDB, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
        0xAE, 0x42, 0x60, 0x82
    ];

    private readonly WordprocessingDocument _package;
    private readonly DocumentEditorDocument _document;
    private readonly DocumentFormatExportOptions _options;
    private MainDocumentPart _mainPart = null!;
    private long _drawingId = 1;
    private readonly Dictionary<string, string> _commentIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _revisionIds = new(StringComparer.Ordinal);

    /// <summary>Creates a DOCX package writer.</summary>
    public DocxPackageWriter(WordprocessingDocument package, DocumentEditorDocument document, DocumentFormatExportOptions options)
    {
        _package = package;
        _document = document;
        _options = options;
    }

    /// <summary>Writes the package.</summary>
    public async Task WriteAsync(CancellationToken cancellationToken = default)
    {
        _package.PackageProperties.Title = _document.Metadata.Title;
        _package.PackageProperties.Creator = _document.Metadata.Author?.DisplayName;
        _package.PackageProperties.Created = _document.Metadata.CreatedAt.UtcDateTime;
        _package.PackageProperties.Modified = (_document.Metadata.ModifiedAt ?? DateTimeOffset.UtcNow).UtcDateTime;

        _mainPart = _package.AddMainDocumentPart();
        _mainPart.Document = new W.Document(new W.Body());
        AddStylesPart();
        AddNumberingPart();
        AddNotesParts();
        AddRevisionIds();
        AddCommentsPart();

        var body = _mainPart.Document.Body!;
        foreach (var block in _document.Blocks.OrderBy(block => block.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var element in await WriteBlockAsync(block, cancellationToken))
            {
                body.Append(element);
            }
        }

        body.Append(CreateSectionProperties());
        AddHeadersFooters(body);
        _mainPart.Document.Save();
    }

    private async Task<List<OpenXmlElement>> WriteBlockAsync(DocumentBlock block, CancellationToken cancellationToken)
    {
        return block.Content switch
        {
            ParagraphBlockContent paragraph => [WriteParagraph(paragraph.Inlines)],
            HeadingBlockContent heading => [WriteParagraph(heading.Inlines, heading.Level)],
            ListBlockContent list => [WriteParagraph(list.Inlines, ordered: list.Ordered, level: list.IndentLevel)],
            QuoteBlockContent quote => [WriteParagraph(quote.Inlines, styleId: "Quote")],
            TableBlockContent table => [WriteTable(table)],
            ImageBlockContent image => [await WriteImageParagraphAsync(image, cancellationToken)],
            PageBreakBlockContent => [new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page }))],
            _ => []
        };
    }

    private W.Paragraph WriteParagraph(IEnumerable<InlineContent> inlines, int? headingLevel = null, bool ordered = false, int level = 0, string? styleId = null)
    {
        var paragraph = new W.Paragraph();
        var properties = new W.ParagraphProperties();
        if (headingLevel is not null)
        {
            properties.Append(new W.ParagraphStyleId { Val = $"Heading{Math.Clamp(headingLevel.Value, 1, 6)}" });
        }
        else if (!string.IsNullOrWhiteSpace(styleId))
        {
            properties.Append(new W.ParagraphStyleId { Val = styleId });
        }

        if (ordered || level > 0)
        {
            properties.Append(new W.NumberingProperties(
                new W.NumberingLevelReference { Val = level },
                new W.NumberingId { Val = ordered ? 2 : 1 }));
        }

        if (properties.HasChildren)
        {
            paragraph.Append(properties);
        }

        foreach (var inline in inlines)
        {
            if (inline is TextRun text)
            {
                foreach (var element in WriteTextInline(text))
                {
                    paragraph.Append(element);
                }
            }
            else if (inline is TokenRun token)
            {
                paragraph.Append(WriteRun($"{{{{{token.Key}}}}}", inline.Marks));
            }
            else if (inline is DocumentNoteReferenceRun note)
            {
                if (note.NoteType == DocumentNoteType.Footnote)
                {
                    paragraph.Append(new W.Run(new W.FootnoteReference { Id = int.TryParse(note.NoteId, out var footnoteId) ? footnoteId : 1 }));
                }
                else
                {
                    paragraph.Append(new W.Run(new W.EndnoteReference { Id = int.TryParse(note.NoteId, out var endnoteId) ? endnoteId : 1 }));
                }
            }
        }

        if (!paragraph.ChildElements.Any(element =>
            element is W.Run or W.Hyperlink or W.InsertedRun or W.DeletedRun or W.CommentRangeStart or W.CommentRangeEnd))
        {
            paragraph.Append(new W.Run(new W.Text(string.Empty)));
        }

        return paragraph;
    }

    private IEnumerable<OpenXmlElement> WriteTextInline(TextRun text)
    {
        var semanticMarks = text.Marks
            .Where(mark => mark.Type is InlineMarkType.Link or InlineMarkType.CommentAnchor or InlineMarkType.Revision)
            .ToList();
        OpenXmlElement content = WriteRun(text.Text, text.Marks.Except(semanticMarks));

        var link = semanticMarks.FirstOrDefault(mark => mark.Type == InlineMarkType.Link && mark.Link is not null)?.Link;
        if (link is not null && Uri.TryCreate(link.Href, UriKind.Absolute, out var uri))
        {
            var rel = _mainPart.AddHyperlinkRelationship(uri, true);
            content = new W.Hyperlink(content.CloneNode(true)) { Id = rel.Id };
        }

        var revisionMark = semanticMarks.FirstOrDefault(mark => mark.Type == InlineMarkType.Revision && !string.IsNullOrWhiteSpace(mark.RevisionId));
        if (revisionMark?.RevisionId is not null && _revisionIds.TryGetValue(revisionMark.RevisionId, out var revisionId))
        {
            var revision = _document.Revisions.FirstOrDefault(item => item.Id == revisionMark.RevisionId);
            var author = revision?.Author.DisplayName ?? string.Empty;
            var date = revision?.CreatedAt.UtcDateTime ?? DateTime.UtcNow;
            content = revision?.Type == DocumentRevisionType.Deletion
                ? new W.DeletedRun(content.CloneNode(true)) { Id = revisionId, Author = author, Date = date }
                : new W.InsertedRun(content.CloneNode(true)) { Id = revisionId, Author = author, Date = date };
        }

        var commentId = semanticMarks
            .FirstOrDefault(mark => mark.Type == InlineMarkType.CommentAnchor && mark.CommentAnchor is not null)
            ?.CommentAnchor?.CommentId;
        if (!string.IsNullOrWhiteSpace(commentId) && _commentIds.TryGetValue(commentId, out var docxCommentId))
        {
            yield return new W.CommentRangeStart { Id = docxCommentId };
            yield return content;
            yield return new W.CommentRangeEnd { Id = docxCommentId };
            yield return new W.Run(new W.CommentReference { Id = docxCommentId });
            yield break;
        }

        yield return content;
    }

    private static W.Run WriteRun(string text, IEnumerable<InlineMark> marks)
    {
        var run = new W.Run();
        var properties = new W.RunProperties();
        foreach (var mark in marks)
        {
            switch (mark.Type)
            {
                case InlineMarkType.Bold:
                    properties.Append(new W.Bold());
                    break;
                case InlineMarkType.Italic:
                    properties.Append(new W.Italic());
                    break;
                case InlineMarkType.Underline:
                    properties.Append(new W.Underline { Val = W.UnderlineValues.Single });
                    break;
                case InlineMarkType.Strikethrough:
                    properties.Append(new W.Strike());
                    break;
                case InlineMarkType.Superscript:
                    properties.Append(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript });
                    break;
                case InlineMarkType.Subscript:
                    properties.Append(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Subscript });
                    break;
                case InlineMarkType.TextColor when !string.IsNullOrWhiteSpace(mark.Value):
                    properties.Append(new W.Color { Val = mark.Value.TrimStart('#') });
                    break;
                case InlineMarkType.Highlight when !string.IsNullOrWhiteSpace(mark.Value):
                    properties.Append(new W.Highlight { Val = W.HighlightColorValues.Yellow });
                    break;
            }
        }

        if (properties.HasChildren)
        {
            run.Append(properties);
        }

        run.Append(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private W.Table WriteTable(TableBlockContent table)
    {
        var docxTable = new W.Table(new W.TableProperties(new W.TableBorders(
            new W.TopBorder { Val = W.BorderValues.Single, Size = 4 },
            new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 },
            new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 },
            new W.RightBorder { Val = W.BorderValues.Single, Size = 4 },
            new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 },
            new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 })));

        foreach (var row in table.Rows)
        {
            var docxRow = new W.TableRow();
            foreach (var cell in row.Cells)
            {
                var docxCell = new W.TableCell();
                var properties = new W.TableCellProperties();
                if (cell.ColumnSpan > 1)
                {
                    properties.Append(new W.GridSpan { Val = cell.ColumnSpan });
                }

                if (cell.RowSpan > 1 || !cell.Merge.IsOrigin)
                {
                    properties.Append(new W.VerticalMerge { Val = cell.Merge.IsOrigin ? W.MergedCellValues.Restart : W.MergedCellValues.Continue });
                }

                if (properties.HasChildren)
                {
                    docxCell.Append(properties);
                }

                foreach (var block in cell.Blocks)
                {
                    if (block.Content is ParagraphBlockContent paragraph)
                    {
                        docxCell.Append(WriteParagraph(paragraph.Inlines));
                    }
                    else
                    {
                        docxCell.Append(WriteParagraph(DocumentModelText.TextInlines(DocumentModelText.GetBlockText(block))));
                    }
                }

                if (!docxCell.Elements<W.Paragraph>().Any())
                {
                    docxCell.Append(new W.Paragraph());
                }

                docxRow.Append(docxCell);
            }

            docxTable.Append(docxRow);
        }

        return docxTable;
    }

    private async Task<W.Paragraph> WriteImageParagraphAsync(ImageBlockContent image, CancellationToken cancellationToken)
    {
        var imageBytes = await ResolveImageBytesAsync(image, cancellationToken);
        var part = _mainPart.AddImagePart(ImagePartType.Png);
        await using (var stream = new MemoryStream(imageBytes))
        {
            part.FeedData(stream);
        }

        var relId = _mainPart.GetIdOfPart(part);
        var width = image.Size.Width ?? 120;
        var height = image.Size.Height ?? 90;
        var cx = (long)(width * 12700);
        var cy = (long)(height * 12700);
        var graphic = CreatePictureGraphic(image, relId, cx, cy);
        var drawingBody = image.FloatingLayout?.Inline == false
            ? CreateAnchoredDrawing(image, cx, cy, graphic)
            : CreateInlineDrawing(image, cx, cy, graphic);
        var drawing = new W.Drawing(drawingBody);

        var paragraph = new W.Paragraph(new W.Run(drawing));
        if (!string.IsNullOrWhiteSpace(image.Caption))
        {
            paragraph.Append(new W.Run(new W.Break()), WriteRun(image.Caption, []));
        }

        return paragraph;
    }

    private OpenXmlElement CreateInlineDrawing(ImageBlockContent image, long cx, long cy, A.Graphic graphic)
    {
        return new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            new DW.DocProperties { Id = (UInt32Value)_drawingId++, Name = image.AltText ?? "Picture" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            graphic)
        {
            DistanceFromTop = 0,
            DistanceFromBottom = 0,
            DistanceFromLeft = 0,
            DistanceFromRight = 0
        };
    }

    private OpenXmlElement CreateAnchoredDrawing(ImageBlockContent image, long cx, long cy, A.Graphic graphic)
    {
        var layout = image.FloatingLayout!;
        var anchor = new DW.Anchor(
            new DW.SimplePosition { X = 0, Y = 0 },
            new DW.HorizontalPosition(
                new DW.PositionOffset(PointToEmu(layout.X).ToString(CultureInfo.InvariantCulture)))
            { RelativeFrom = ToDocxHorizontalRelative(layout.HorizontalRelativeTo) },
            new DW.VerticalPosition(
                new DW.PositionOffset(PointToEmu(layout.Y).ToString(CultureInfo.InvariantCulture)))
            { RelativeFrom = ToDocxVerticalRelative(layout.VerticalRelativeTo) },
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            CreateDocxWrap(layout.WrapMode),
            new DW.DocProperties { Id = (UInt32Value)_drawingId++, Name = image.AltText ?? "Picture" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            graphic)
        {
            DistanceFromTop = 0,
            DistanceFromBottom = 0,
            DistanceFromLeft = 0,
            DistanceFromRight = 0,
            SimplePos = false,
            RelativeHeight = (UInt32Value)(uint)Math.Max(0, layout.ZIndex),
            BehindDoc = layout.WrapMode == DocumentWrapMode.BehindText,
            Locked = layout.LockAnchor,
            LayoutInCell = true,
            AllowOverlap = true
        };

        return anchor;
    }

    private A.Graphic CreatePictureGraphic(ImageBlockContent image, string relId, long cx, long cy)
    {
        return new A.Graphic(new A.GraphicData(
            new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    new PIC.NonVisualDrawingProperties { Id = (UInt32Value)_drawingId++, Name = image.AltText ?? "Picture", Description = image.AltText },
                    new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(new A.Blip { Embed = relId }, new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = cx, Cy = cy }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });
    }

    private static OpenXmlElement CreateDocxWrap(DocumentWrapMode wrapMode)
    {
        return wrapMode switch
        {
            DocumentWrapMode.TopBottom => new DW.WrapTopBottom(),
            DocumentWrapMode.BehindText or DocumentWrapMode.InFrontOfText => new DW.WrapNone(),
            _ => new DW.WrapSquare { WrapText = DW.WrapTextValues.BothSides }
        };
    }

    private static long PointToEmu(double value)
    {
        return (long)Math.Round(value * 12700);
    }

    private static DW.HorizontalRelativePositionValues ToDocxHorizontalRelative(DocumentRelativePosition value)
    {
        return value switch
        {
            DocumentRelativePosition.Margin => DW.HorizontalRelativePositionValues.Margin,
            DocumentRelativePosition.Column => DW.HorizontalRelativePositionValues.Column,
            DocumentRelativePosition.Character => DW.HorizontalRelativePositionValues.Character,
            _ => DW.HorizontalRelativePositionValues.Page
        };
    }

    private static DW.VerticalRelativePositionValues ToDocxVerticalRelative(DocumentRelativePosition value)
    {
        return value switch
        {
            DocumentRelativePosition.Margin => DW.VerticalRelativePositionValues.Margin,
            DocumentRelativePosition.Line => DW.VerticalRelativePositionValues.Line,
            DocumentRelativePosition.Page => DW.VerticalRelativePositionValues.Page,
            _ => DW.VerticalRelativePositionValues.Paragraph
        };
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
            if (comma >= 0 && Convert.TryFromBase64String(image.Url[(comma + 1)..], new Span<byte>(new byte[image.Url.Length]), out _))
            {
                return Convert.FromBase64String(image.Url[(comma + 1)..]);
            }
        }

        return TransparentPng;
    }

    private W.SectionProperties CreateSectionProperties()
    {
        var settings = _document.Sections.FirstOrDefault()?.Properties.PageSettings ?? _document.PageSettings;
        return new W.SectionProperties(
            new W.PageSize
            {
                Width = (UInt32Value)(uint)Math.Round(settings.Size.Width * 20),
                Height = (UInt32Value)(uint)Math.Round(settings.Size.Height * 20),
                Orient = settings.Landscape ? W.PageOrientationValues.Landscape : W.PageOrientationValues.Portrait
            },
            new W.PageMargin
            {
                Top = (Int32Value)(int)Math.Round(settings.Margins.Top * 20),
                Right = (UInt32Value)(uint)Math.Round(settings.Margins.Right * 20),
                Bottom = (Int32Value)(int)Math.Round(settings.Margins.Bottom * 20),
                Left = (UInt32Value)(uint)Math.Round(settings.Margins.Left * 20)
            });
    }

    private void AddStylesPart()
    {
        var stylesPart = _mainPart.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new W.Styles(
            new W.Style(new W.Name { Val = "Heading 1" }, new W.BasedOn { Val = "Normal" }, new W.NextParagraphStyle { Val = "Normal" }) { Type = W.StyleValues.Paragraph, StyleId = "Heading1" },
            new W.Style(new W.Name { Val = "Heading 2" }, new W.BasedOn { Val = "Normal" }, new W.NextParagraphStyle { Val = "Normal" }) { Type = W.StyleValues.Paragraph, StyleId = "Heading2" },
            new W.Style(new W.Name { Val = "Quote" }) { Type = W.StyleValues.Paragraph, StyleId = "Quote" });
        stylesPart.Styles.Save();
    }

    private void AddNumberingPart()
    {
        var numberingPart = _mainPart.AddNewPart<NumberingDefinitionsPart>();
        numberingPart.Numbering = new W.Numbering(
            new W.AbstractNum(new W.Level(new W.NumberingFormat { Val = W.NumberFormatValues.Bullet }, new W.LevelText { Val = "•" }) { LevelIndex = 0 }) { AbstractNumberId = 1 },
            new W.AbstractNum(new W.Level(new W.NumberingFormat { Val = W.NumberFormatValues.Decimal }, new W.LevelText { Val = "%1." }) { LevelIndex = 0 }) { AbstractNumberId = 2 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 2 }) { NumberID = 2 });
        numberingPart.Numbering.Save();
    }

    private void AddHeadersFooters(W.Body body)
    {
        var section = body.Elements<W.SectionProperties>().LastOrDefault();
        if (section is null)
        {
            return;
        }

        foreach (var header in _document.HeadersFooters.Where(h => h.Type == DocumentHeaderFooterType.Header))
        {
            var part = _mainPart.AddNewPart<HeaderPart>();
            part.Header = new W.Header(header.Blocks.Select(block => WriteParagraph(DocumentModelText.TextInlines(DocumentModelText.GetBlockText(block)))));
            part.Header.Save();
            section.PrependChild(new W.HeaderReference { Type = ToHeaderFooterValues(header.Scope), Id = _mainPart.GetIdOfPart(part) });
        }

        foreach (var footer in _document.HeadersFooters.Where(h => h.Type == DocumentHeaderFooterType.Footer))
        {
            var part = _mainPart.AddNewPart<FooterPart>();
            part.Footer = new W.Footer(footer.Blocks.Select(block => WriteParagraph(DocumentModelText.TextInlines(DocumentModelText.GetBlockText(block)))));
            part.Footer.Save();
            section.PrependChild(new W.FooterReference { Type = ToHeaderFooterValues(footer.Scope), Id = _mainPart.GetIdOfPart(part) });
        }
    }

    private static W.HeaderFooterValues ToHeaderFooterValues(DocumentHeaderFooterScope scope)
    {
        return scope switch
        {
            DocumentHeaderFooterScope.FirstPage => W.HeaderFooterValues.First,
            DocumentHeaderFooterScope.EvenPages => W.HeaderFooterValues.Even,
            _ => W.HeaderFooterValues.Default
        };
    }

    private void AddNotesParts()
    {
        var footnotes = _document.Notes.Where(note => note.Type == DocumentNoteType.Footnote).ToList();
        if (footnotes.Count > 0)
        {
            var part = _mainPart.AddNewPart<FootnotesPart>();
            part.Footnotes = new W.Footnotes(footnotes.Select(note => new W.Footnote(note.Blocks.Select(block => WriteParagraph(DocumentModelText.TextInlines(DocumentModelText.GetBlockText(block))))) { Id = int.TryParse(note.Id, out var id) ? id : footnotes.IndexOf(note) + 1 }));
            part.Footnotes.Save();
        }

        var endnotes = _document.Notes.Where(note => note.Type == DocumentNoteType.Endnote).ToList();
        if (endnotes.Count > 0)
        {
            var part = _mainPart.AddNewPart<EndnotesPart>();
            part.Endnotes = new W.Endnotes(endnotes.Select(note => new W.Endnote(note.Blocks.Select(block => WriteParagraph(DocumentModelText.TextInlines(DocumentModelText.GetBlockText(block))))) { Id = int.TryParse(note.Id, out var id) ? id : endnotes.IndexOf(note) + 1 }));
            part.Endnotes.Save();
        }
    }

    private void AddCommentsPart()
    {
        if (_document.Comments.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _document.Comments.Count; i++)
        {
            _commentIds[_document.Comments[i].Id] = i.ToString(CultureInfo.InvariantCulture);
        }

        var part = _mainPart.AddNewPart<WordprocessingCommentsPart>();
        part.Comments = new W.Comments(_document.Comments.Select((comment, index) => new W.Comment(
            new W.Paragraph(new W.Run(new W.Text(comment.Entries.FirstOrDefault()?.Text ?? string.Empty))))
        {
            Id = _commentIds[comment.Id],
            Author = comment.Entries.FirstOrDefault()?.Author.DisplayName ?? string.Empty,
            Date = comment.Entries.FirstOrDefault()?.CreatedAt.UtcDateTime ?? DateTime.UtcNow
        }));
        part.Comments.Save();
    }

    private void AddRevisionIds()
    {
        for (var i = 0; i < _document.Revisions.Count; i++)
        {
            _revisionIds[_document.Revisions[i].Id] = i.ToString(CultureInfo.InvariantCulture);
        }
    }
}
