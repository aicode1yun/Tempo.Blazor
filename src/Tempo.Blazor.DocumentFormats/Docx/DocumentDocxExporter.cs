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
        DocumentImagePersistence.Sanitize(document);
        using var memory = new MemoryStream();
        var warnings = new List<DocumentFormatCompatibilityWarning>();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var writer = new DocxPackageWriter(word, document, options);
            await writer.WriteAsync(cancellationToken);
            warnings = writer.Warnings.ToList();
        }

        return new DocumentFormatExportResult
        {
            Content = memory.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName = $"{SanitizeFileName(options.FileName ?? document.Metadata.Title ?? document.DocumentId)}.docx",
            Format = DocumentFormatKind.Docx,
            Warnings = warnings
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
    private const string TempoNamespace = "urn:tempo-blazor:document-editor:1.0";
    private const string TempoPrefix = "tm";
    private const string MarkupCompatibilityNamespace = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private const string WordprocessingDrawing2010Namespace = "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing";

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
    private readonly Dictionary<string, DocumentFormatImageExportResult?> _assetImageCache = new(StringComparer.Ordinal);
    private readonly List<DocumentFormatCompatibilityWarning> _warnings = [];

    /// <summary>Creates a DOCX package writer.</summary>
    public DocxPackageWriter(WordprocessingDocument package, DocumentEditorDocument document, DocumentFormatExportOptions options)
    {
        _package = package;
        _document = document;
        _options = options;
    }

    /// <summary>Compatibility warnings emitted while writing the package.</summary>
    public IReadOnlyList<DocumentFormatCompatibilityWarning> Warnings => _warnings;

    /// <summary>Writes the package.</summary>
    public async Task WriteAsync(CancellationToken cancellationToken = default)
    {
        _package.PackageProperties.Title = _document.Metadata.Title;
        _package.PackageProperties.Creator = _document.Metadata.Author?.DisplayName;
        _package.PackageProperties.Created = _document.Metadata.CreatedAt.UtcDateTime;
        _package.PackageProperties.Modified = (_document.Metadata.ModifiedAt ?? DateTimeOffset.UtcNow).UtcDateTime;

        _mainPart = _package.AddMainDocumentPart();
        _mainPart.Document = CreateDocumentRoot();
        AddStylesPart();
        AddNumberingPart();
        await AddNotesPartsAsync(cancellationToken);
        AddRevisionIds();
        await AddCommentsPartAsync(cancellationToken);
        AddSettingsPart();

        var body = _mainPart.Document.Body!;
        var mainContext = new DocxPartWriterContext(_mainPart, DocumentRenditionAnchorScope.Body);
        foreach (var block in _document.Blocks.OrderBy(block => block.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var restrictedMarker = FindRestrictedMarkerForBlock(block.Id);
            foreach (var element in await WriteBlockAsync(block, mainContext, cancellationToken))
            {
                body.Append(restrictedMarker is null ? element : WrapEditableRegion(element, restrictedMarker));
            }
        }

        body.Append(CreateSectionProperties());
        await AddHeadersFootersAsync(body, cancellationToken);
        _mainPart.Document.Save();
    }

    private static W.Document CreateDocumentRoot()
    {
        var document = new W.Document(new W.Body())
        {
            MCAttributes = new MarkupCompatibilityAttributes { Ignorable = "wp14 tm" }
        };
        document.AddNamespaceDeclaration("mc", MarkupCompatibilityNamespace);
        document.AddNamespaceDeclaration("wp14", WordprocessingDrawing2010Namespace);
        document.AddNamespaceDeclaration(TempoPrefix, TempoNamespace);
        return document;
    }

    private DocumentRestrictedMarker? FindRestrictedMarkerForBlock(string blockId)
        => _document.RestrictedMarkers.FirstOrDefault(marker =>
            string.Equals(marker.StartBlockId, blockId, StringComparison.Ordinal)
            && string.Equals(marker.EndBlockId, blockId, StringComparison.Ordinal));

    private static W.SdtBlock WrapEditableRegion(OpenXmlElement element, DocumentRestrictedMarker marker)
        => new(
            new W.SdtProperties(
                new W.SdtAlias { Val = marker.Label ?? "Editable region" },
                new W.Tag { Val = $"tm-editable:{marker.Id}:{marker.StartOffset}:{marker.EndOffset}" },
                new W.Lock { Val = W.LockingValues.SdtLocked }),
            new W.SdtContentBlock(element.CloneNode(true)));

    private async Task<List<OpenXmlElement>> WriteBlockAsync(
        DocumentBlock block,
        DocxPartWriterContext context,
        CancellationToken cancellationToken)
    {
        return block.Content switch
        {
            ParagraphBlockContent paragraph => [await WriteParagraphAsync(paragraph.Inlines, context, blockId: block.Id, cancellationToken: cancellationToken)],
            HeadingBlockContent heading => [await WriteParagraphAsync(heading.Inlines, context, heading.Level, blockId: block.Id, cancellationToken: cancellationToken)],
            ListBlockContent list => [await WriteParagraphAsync(list.Inlines, context, ordered: list.Ordered, level: list.IndentLevel, blockId: block.Id, cancellationToken: cancellationToken)],
            QuoteBlockContent quote => [await WriteParagraphAsync(quote.Inlines, context, styleId: "Quote", blockId: block.Id, cancellationToken: cancellationToken)],
            TableBlockContent table => [await WriteTableAsync(table, context, block.Id, cancellationToken)],
            ImageBlockContent image => [await WriteImageParagraphAsync(image, context, block.Id, cancellationToken)],
            PageBreakBlockContent => [new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page }))],
            _ => []
        };
    }

    private async Task<W.Paragraph> WriteParagraphAsync(
        IEnumerable<InlineContent> inlines,
        DocxPartWriterContext context,
        int? headingLevel = null,
        bool ordered = false,
        int level = 0,
        string? styleId = null,
        string? blockId = null,
        CancellationToken cancellationToken = default)
    {
        var paragraph = new W.Paragraph();
        SetTempoAttribute(paragraph, "block-id", blockId);
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
                foreach (var element in WriteTextInline(text, context))
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
            else if (inline is DocumentDrawingRun drawing)
            {
                var imageRun = await WriteDrawingRunAsync(drawing, context, cancellationToken);
                if (imageRun is not null)
                {
                    paragraph.Append(imageRun);
                }

                if (imageRun is not null && !string.IsNullOrWhiteSpace(drawing.Caption))
                {
                    paragraph.Append(new W.Run(new W.Break()), WriteRun(drawing.Caption, []));
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

    private W.Paragraph WriteParagraph(
        IEnumerable<InlineContent> inlines,
        DocxPartWriterContext context,
        int? headingLevel = null,
        bool ordered = false,
        int level = 0,
        string? styleId = null,
        string? blockId = null)
        => WriteParagraphAsync(inlines, context, headingLevel, ordered, level, styleId, blockId).GetAwaiter().GetResult();

    private IEnumerable<OpenXmlElement> WriteTextInline(TextRun text, DocxPartWriterContext context)
    {
        var semanticMarks = text.Marks
            .Where(mark => mark.Type is InlineMarkType.Link or InlineMarkType.CommentAnchor or InlineMarkType.Revision)
            .ToList();
        OpenXmlElement content = WriteRun(text.Text, text.Marks.Except(semanticMarks));

        var link = semanticMarks.FirstOrDefault(mark => mark.Type == InlineMarkType.Link && mark.Link is not null)?.Link;
        if (link is not null && Uri.TryCreate(link.Href, UriKind.Absolute, out var uri))
        {
            var rel = context.OwnerPart.AddHyperlinkRelationship(uri, true);
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

    private async Task<W.Table> WriteTableAsync(
        TableBlockContent table,
        DocxPartWriterContext context,
        string tableId,
        CancellationToken cancellationToken)
    {
        var tableProperties = new W.TableProperties(new W.TableBorders(
            new W.TopBorder { Val = W.BorderValues.Single, Size = 4 },
            new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 },
            new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 },
            new W.RightBorder { Val = W.BorderValues.Single, Size = 4 },
            new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 },
            new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 }));

        if (table.Layout.Width is > 0)
        {
            tableProperties.Append(new W.TableWidth
            {
                Type = W.TableWidthUnitValues.Dxa,
                Width = PointsToTwips(table.Layout.Width.Value).ToString(CultureInfo.InvariantCulture)
            });
        }

        if (table.Layout.Alignment != TableHorizontalAlignment.Left)
        {
            tableProperties.Append(new W.TableJustification
            {
                Val = table.Layout.Alignment == TableHorizontalAlignment.Center
                    ? W.TableRowAlignmentValues.Center
                    : W.TableRowAlignmentValues.Right
            });
        }

        var tableFill = NormalizeDocxColor(table.Layout.BackgroundColor);
        if (!string.IsNullOrWhiteSpace(tableFill))
        {
            tableProperties.Append(new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = tableFill });
        }

        var docxTable = new W.Table(tableProperties);
        SetTempoAttribute(docxTable, "block-id", tableId);

        foreach (var row in table.Rows)
        {
            var docxRow = new W.TableRow();
            foreach (var cell in row.Cells)
            {
                var docxCell = new W.TableCell();
                SetTempoAttribute(docxCell, "cell-id", cell.Id);
                var properties = new W.TableCellProperties();
                if (cell.ColumnSpan > 1)
                {
                    properties.Append(new W.GridSpan { Val = cell.ColumnSpan });
                }

                if (cell.RowSpan > 1 || !cell.Merge.IsOrigin)
                {
                    properties.Append(new W.VerticalMerge { Val = cell.Merge.IsOrigin ? W.MergedCellValues.Restart : W.MergedCellValues.Continue });
                }

                if (cell.Width is > 0)
                {
                    properties.Append(new W.TableCellWidth
                    {
                        Type = W.TableWidthUnitValues.Dxa,
                        Width = PointsToTwips(cell.Width.Value).ToString(CultureInfo.InvariantCulture)
                    });
                }

                var cellFill = NormalizeDocxColor(cell.BackgroundColor);
                if (!string.IsNullOrWhiteSpace(cellFill))
                {
                    properties.Append(new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = cellFill });
                }

                if (cell.VerticalAlignment != TableCellVerticalAlignment.Top)
                {
                    properties.Append(new W.TableCellVerticalAlignment
                    {
                        Val = cell.VerticalAlignment == TableCellVerticalAlignment.Middle
                            ? W.TableVerticalAlignmentValues.Center
                            : W.TableVerticalAlignmentValues.Bottom
                    });
                }

                if (properties.HasChildren)
                {
                    docxCell.Append(properties);
                }

                foreach (var block in cell.Blocks)
                {
                    var cellContext = context.ForTableCell(tableId, cell.Id);
                    if (block.Content is ParagraphBlockContent paragraph)
                    {
                        docxCell.Append(await WriteParagraphAsync(paragraph.Inlines, cellContext, blockId: block.Id, cancellationToken: cancellationToken));
                    }
                    else
                    {
                        docxCell.Append(WriteParagraph(DocumentModelText.TextInlines(DocumentModelText.GetBlockText(block)), cellContext, blockId: block.Id));
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

    private static int PointsToTwips(double value) => DocxUnitConverter.PointToTwip(value);

    private static string? NormalizeDocxColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        var value = color.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        return value.Length == 6 && value.All(Uri.IsHexDigit)
            ? value.ToUpperInvariant()
            : null;
    }

    private async Task<W.Paragraph> WriteImageParagraphAsync(
        ImageBlockContent image,
        DocxPartWriterContext context,
        string? blockId,
        CancellationToken cancellationToken)
    {
        var drawing = DocxDrawingRunAdapter.FromImageBlock(image);
        var imageRun = await WriteDrawingRunAsync(drawing, context, cancellationToken);
        var paragraph = imageRun is null ? new W.Paragraph() : new W.Paragraph(imageRun);
        SetTempoAttribute(paragraph, "block-id", blockId);
        if (imageRun is not null && !string.IsNullOrWhiteSpace(image.Caption))
        {
            paragraph.Append(new W.Run(new W.Break()), WriteRun(image.Caption, []));
        }

        return paragraph;
    }

    private async Task<W.Run?> WriteDrawingRunAsync(
        DocumentDrawingRun drawing,
        DocxPartWriterContext context,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveImageAsync(drawing, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        ApplyWriterContext(drawing.Layout.Anchor, context);
        var ownerPart = context.OwnerPart;
        var part = AddImagePart(ownerPart, resolved.PartInfo.ImagePartType);
        await using (var stream = new MemoryStream(resolved.Content))
        {
            part.FeedData(stream);
        }

        var parts = new DocxPictureParts(ownerPart.GetIdOfPart(part), CreateExtent(drawing), resolved.PartInfo);
        var graphic = CreatePictureGraphic(drawing, parts);
        var drawingBody = !drawing.Layout.IsInline
            ? CreateAnchoredDrawing(drawing, parts, graphic)
            : CreateInlineDrawing(drawing, parts, graphic);

        return new W.Run(new W.Drawing(drawingBody));
    }

    private static void ApplyWriterContext(DocumentObjectAnchor anchor, DocxPartWriterContext context)
    {
        anchor.Region = context.Region;
        anchor.HeaderFooterId = context.HeaderFooterId ?? anchor.HeaderFooterId;
        anchor.TableId = context.TableId ?? anchor.TableId;
        anchor.CellId = context.CellId ?? anchor.CellId;
    }

    private static ImagePart AddImagePart(OpenXmlPartContainer ownerPart, PartTypeInfo partType)
        => ownerPart switch
        {
            MainDocumentPart main => main.AddImagePart(partType),
            HeaderPart header => header.AddImagePart(partType),
            FooterPart footer => footer.AddImagePart(partType),
            FootnotesPart footnotes => footnotes.AddImagePart(partType),
            EndnotesPart endnotes => endnotes.AddImagePart(partType),
            WordprocessingCommentsPart comments => comments.AddImagePart(partType),
            _ => throw new InvalidOperationException($"DOCX part '{ownerPart.GetType().Name}' does not support image relationships.")
        };

    private static DocxExtent CreateExtent(DocumentDrawingRun drawing)
    {
        var width = drawing.Layout.Transform.Width ?? drawing.Size.Width ?? 120;
        var height = drawing.Layout.Transform.Height ?? drawing.Size.Height ?? 90;
        return new DocxExtent(DocxUnitConverter.PointToEmu(width), DocxUnitConverter.PointToEmu(height));
    }

    private OpenXmlElement CreateInlineDrawing(DocumentDrawingRun drawing, DocxPictureParts parts, A.Graphic graphic)
    {
        var inline = new DW.Inline(
            new DW.Extent { Cx = parts.Extent.Cx, Cy = parts.Extent.Cy },
            CreateEffectExtent(drawing.Docx?.EffectExtent),
            CreateDocProperties(drawing),
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = drawing.Layout.Transform.LockAspectRatio }),
            graphic)
        {
            DistanceFromTop = 0,
            DistanceFromBottom = 0,
            DistanceFromLeft = 0,
            DistanceFromRight = 0
        };

        WriteTempoDrawingIdentityAttributes(inline, drawing);
        WriteTempoInlineLayoutAttributes(inline, drawing.Layout);
        return inline;
    }

    private OpenXmlElement CreateAnchoredDrawing(DocumentDrawingRun drawing, DocxPictureParts parts, A.Graphic graphic)
    {
        var layout = drawing.Layout;
        var metadata = drawing.Docx;
        var anchor = new DW.Anchor(
            CreateSimplePosition(metadata),
            CreateDocxHorizontalPosition(layout),
            CreateDocxVerticalPosition(layout),
            new DW.Extent { Cx = parts.Extent.Cx, Cy = parts.Extent.Cy },
            CreateEffectExtent(drawing.Docx?.EffectExtent),
            CreateDocxWrap(layout.Wrap, parts.Extent),
            CreateDocProperties(drawing),
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = layout.Transform.LockAspectRatio }),
            graphic)
        {
            DistanceFromTop = (UInt32Value)(uint)Math.Max(0L, DocxUnitConverter.PointToEmu(layout.Wrap.DistanceTop)),
            DistanceFromBottom = (UInt32Value)(uint)Math.Max(0L, DocxUnitConverter.PointToEmu(layout.Wrap.DistanceBottom)),
            DistanceFromLeft = (UInt32Value)(uint)Math.Max(0L, DocxUnitConverter.PointToEmu(layout.Wrap.DistanceLeft)),
            DistanceFromRight = (UInt32Value)(uint)Math.Max(0L, DocxUnitConverter.PointToEmu(layout.Wrap.DistanceRight)),
            SimplePos = metadata?.UsesSimplePosition ?? false,
            RelativeHeight = (UInt32Value)(uint)Math.Max(0, layout.Stacking.ZIndex),
            BehindDoc = layout.Wrap.Mode == DocumentWrapMode.BehindText,
            Locked = layout.Anchor.LockAnchor,
            LayoutInCell = metadata?.LayoutInCell ?? true,
            Hidden = metadata?.Hidden,
            AllowOverlap = layout.Stacking.AllowOverlap
        };

        WriteOffice2010AnchorAttributes(anchor, metadata);
        WriteTempoDrawingIdentityAttributes(anchor, drawing);
        WriteTempoLayoutAttributes(anchor, layout);
        return anchor;
    }

    private static void WriteTempoDrawingIdentityAttributes(OpenXmlElement element, DocumentDrawingRun drawing)
    {
        SetTempoAttribute(element, "object-id", drawing.ObjectId);
        SetTempoAttribute(element, "run-id", drawing.Id);
    }

    private void WriteOffice2010AnchorAttributes(DW.Anchor anchor, DocumentDocxDrawingMetadata? metadata)
    {
        if (metadata is null)
        {
            return;
        }

        if (TryGetOffice2010DrawingId(metadata.AnchorId, out var anchorId))
        {
            anchor.AnchorId = anchorId;
        }
        else if (!string.IsNullOrWhiteSpace(metadata.AnchorId))
        {
            _warnings.Add(Warning(
                "docx.drawingInvalidAnchorIdDropped",
                $"DOCX picture anchor id '{metadata.AnchorId}' is not a valid Office 2010 drawing id and was not exported.",
                DocumentFormatCompatibilitySeverity.Warning,
                metadata.Media?.ImagePartUri));
        }

        if (TryGetOffice2010DrawingId(metadata.EditId, out var editId))
        {
            anchor.EditId = editId;
        }
        else if (!string.IsNullOrWhiteSpace(metadata.EditId))
        {
            _warnings.Add(Warning(
                "docx.drawingInvalidEditIdDropped",
                $"DOCX picture edit id '{metadata.EditId}' is not a valid Office 2010 drawing id and was not exported.",
                DocumentFormatCompatibilitySeverity.Warning,
                metadata.Media?.ImagePartUri));
        }
    }

    private static bool TryGetOffice2010DrawingId(string? value, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length != 8 || !trimmed.All(Uri.IsHexDigit))
        {
            return false;
        }

        id = trimmed.ToUpperInvariant();
        return true;
    }

    private A.Graphic CreatePictureGraphic(DocumentDrawingRun drawing, DocxPictureParts parts)
    {
        var shapeProperties = new PIC.ShapeProperties(
            DocxTransformConverter.ToTransform2D(drawing.Layout.Transform, parts.Extent.Cx, parts.Extent.Cy),
            CreatePresetGeometry(drawing));
        AppendPreservedRawDrawingEffects(drawing, shapeProperties);

        return new A.Graphic(new A.GraphicData(
            new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    CreatePictureNonVisualDrawingProperties(drawing),
                    new PIC.NonVisualPictureDrawingProperties()),
                CreateBlipFill(drawing, parts.RelationshipId),
                shapeProperties))
        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });
    }

    private void AppendPreservedRawDrawingEffects(DocumentDrawingRun drawing, PIC.ShapeProperties shapeProperties)
    {
        if (string.IsNullOrWhiteSpace(drawing.Docx?.RawDrawingXml))
        {
            return;
        }

        if (drawing.Docx.RawDrawingXml.Contains("outerShdw", StringComparison.OrdinalIgnoreCase))
        {
            shapeProperties.Append(new A.EffectList(new A.OuterShadow()));
            _warnings.Add(Warning(
                "docx.drawingUnsupportedEffectExportFallback",
                "DOCX picture raw DrawingML included an unsupported outer shadow effect; a preserve fallback effect was exported.",
                DocumentFormatCompatibilitySeverity.Warning,
                drawing.Docx.Media?.ImagePartUri));
        }
    }

    private DW.DocProperties CreateDocProperties(DocumentDrawingRun drawing)
    {
        var metadata = drawing.Docx;
        return new DW.DocProperties
        {
            Id = NextDrawingId(metadata?.DocPrId),
            Name = FirstNonWhiteSpace(metadata?.DocPrName, drawing.AltText, "Picture"),
            Description = FirstNonWhiteSpace(drawing.AltText, metadata?.DocPrDescription),
            Title = metadata?.DocPrTitle
        };
    }

    private PIC.NonVisualDrawingProperties CreatePictureNonVisualDrawingProperties(DocumentDrawingRun drawing)
    {
        var metadata = drawing.Docx;
        return new PIC.NonVisualDrawingProperties
        {
            Id = NextDrawingId(metadata?.PictureNonVisualId),
            Name = FirstNonWhiteSpace(metadata?.PictureName, metadata?.DocPrName, drawing.AltText, "Picture"),
            Description = FirstNonWhiteSpace(drawing.AltText, metadata?.PictureDescription, metadata?.DocPrDescription),
            Title = metadata?.DocPrTitle
        };
    }

    private static DW.EffectExtent CreateEffectExtent(DocumentObjectEffectExtent? effectExtent)
        => new()
        {
            LeftEdge = effectExtent?.Left ?? 0,
            TopEdge = effectExtent?.Top ?? 0,
            RightEdge = effectExtent?.Right ?? 0,
            BottomEdge = effectExtent?.Bottom ?? 0
        };

    private static DW.SimplePosition CreateSimplePosition(DocumentDocxDrawingMetadata? metadata)
        => new()
        {
            X = metadata?.SimplePosition?.X ?? 0L,
            Y = metadata?.SimplePosition?.Y ?? 0L
        };

    private UInt32Value NextDrawingId(uint? preferred)
    {
        if (preferred.HasValue)
        {
            _drawingId = Math.Max(_drawingId, preferred.Value + 1L);
            return (UInt32Value)preferred.Value;
        }

        return (UInt32Value)(uint)_drawingId++;
    }

    private static string? FirstNonWhiteSpace(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private PIC.BlipFill CreateBlipFill(DocumentDrawingRun drawing, string relId)
    {
        var blipFill = new PIC.BlipFill(new A.Blip { Embed = relId });
        var crop = DocxCropConverter.ToSourceRectangle(drawing.Layout.Transform.Crop);
        if (crop is not null)
        {
            blipFill.Append(crop);
        }

        if (drawing.Docx?.BlipFillMode is DocumentDocxBlipFillMode.Tile or DocumentDocxBlipFillMode.Unknown)
        {
            _warnings.Add(Warning(
                "docx.drawingBlipFillFallback",
                $"DOCX picture fill mode '{drawing.Docx.BlipFillMode}' is exported as stretch/fillRect because the editor model does not support it yet.",
                DocumentFormatCompatibilitySeverity.Warning,
                drawing.Docx.Media?.ImagePartUri));
        }

        blipFill.Append(new A.Stretch(new A.FillRectangle()));
        return blipFill;
    }

    private A.PresetGeometry CreatePresetGeometry(DocumentDrawingRun drawing)
    {
        if (!IsRectPreset(drawing.Docx?.PresetGeometry))
        {
            _warnings.Add(Warning(
                "docx.drawingPresetGeometryFallback",
                $"DOCX picture preset geometry '{drawing.Docx!.PresetGeometry}' is exported as rect because the editor model supports rectangular image geometry only.",
                DocumentFormatCompatibilitySeverity.Warning,
                drawing.Docx.Media?.ImagePartUri));
        }

        return new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle };
    }

    private static bool IsRectPreset(string? preset)
        => string.IsNullOrWhiteSpace(preset)
            || preset.Equals("rect", StringComparison.OrdinalIgnoreCase)
            || preset.Equals(A.ShapeTypeValues.Rectangle.ToString(), StringComparison.OrdinalIgnoreCase);

    private static void WriteTempoLayoutAttributes(OpenXmlElement element, DocumentObjectLayout layout)
    {
        SetTempoAttribute(element, "layout-kind", layout.Kind.ToString());
        SetTempoAttribute(element, "anchor-block-id", layout.Anchor.BlockId);
        SetTempoAttribute(element, "anchor-inline-index", layout.Anchor.InlineIndex?.ToString(CultureInfo.InvariantCulture));
        SetTempoAttribute(element, "anchor-offset", layout.Anchor.Offset?.ToString(CultureInfo.InvariantCulture));
        SetTempoAttribute(element, "anchor-region", layout.Anchor.Region.ToString());
        SetTempoAttribute(element, "table-id", layout.Anchor.TableId);
        SetTempoAttribute(element, "cell-id", layout.Anchor.CellId);
        SetTempoAttribute(element, "header-footer-id", layout.Anchor.HeaderFooterId);
        SetTempoAttribute(element, "move-with-text", FormatBool(layout.Anchor.MoveWithText));
        SetTempoAttribute(element, "fixed-on-page", FormatBool(layout.Anchor.FixedOnPage));
        SetTempoAttribute(element, "lock-anchor", FormatBool(layout.Anchor.LockAnchor));
        SetTempoAttribute(element, "horizontal-relative-to", layout.Position.HorizontalRelativeTo.ToString());
        SetTempoAttribute(element, "vertical-relative-to", layout.Position.VerticalRelativeTo.ToString());
        SetTempoAttribute(element, "x", FormatNumber(layout.Position.X));
        SetTempoAttribute(element, "y", FormatNumber(layout.Position.Y));
        SetTempoAttribute(element, "horizontal-alignment", layout.Position.HorizontalAlignment?.ToString());
        SetTempoAttribute(element, "vertical-alignment", layout.Position.VerticalAlignment.ToString());
        SetTempoAttribute(element, "wrap-mode", layout.Wrap.Mode.ToString());
        SetTempoAttribute(element, "distance-left", FormatNumber(layout.Wrap.DistanceLeft));
        SetTempoAttribute(element, "distance-right", FormatNumber(layout.Wrap.DistanceRight));
        SetTempoAttribute(element, "distance-top", FormatNumber(layout.Wrap.DistanceTop));
        SetTempoAttribute(element, "distance-bottom", FormatNumber(layout.Wrap.DistanceBottom));
        SetTempoAttribute(element, "width", FormatNullableNumber(layout.Transform.Width));
        SetTempoAttribute(element, "height", FormatNullableNumber(layout.Transform.Height));
        SetTempoAttribute(element, "natural-width", FormatNullableNumber(layout.Transform.NaturalWidth));
        SetTempoAttribute(element, "natural-height", FormatNullableNumber(layout.Transform.NaturalHeight));
        SetTempoAttribute(element, "lock-aspect-ratio", FormatBool(layout.Transform.LockAspectRatio));
        SetTempoAttribute(element, "rotation", FormatNumber(layout.Transform.Rotation));
        SetTempoAttribute(element, "z-index", layout.Stacking.ZIndex.ToString(CultureInfo.InvariantCulture));
        SetTempoAttribute(element, "allow-overlap", FormatBool(layout.Stacking.AllowOverlap));
    }

    private static void WriteTempoInlineLayoutAttributes(OpenXmlElement element, DocumentObjectLayout layout)
    {
        SetTempoAttribute(element, "anchor-block-id", layout.Anchor.BlockId);
        SetTempoAttribute(element, "anchor-inline-index", layout.Anchor.InlineIndex?.ToString(CultureInfo.InvariantCulture));
        SetTempoAttribute(element, "anchor-offset", layout.Anchor.Offset?.ToString(CultureInfo.InvariantCulture));
        SetTempoAttribute(element, "anchor-region", layout.Anchor.Region.ToString());
        SetTempoAttribute(element, "table-id", layout.Anchor.TableId);
        SetTempoAttribute(element, "cell-id", layout.Anchor.CellId);
        SetTempoAttribute(element, "header-footer-id", layout.Anchor.HeaderFooterId);
        SetTempoAttribute(element, "natural-width", FormatNullableNumber(layout.Transform.NaturalWidth));
        SetTempoAttribute(element, "natural-height", FormatNullableNumber(layout.Transform.NaturalHeight));
    }

    private static void SetTempoAttribute(OpenXmlElement element, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        element.SetAttribute(new OpenXmlAttribute(TempoPrefix, name, TempoNamespace, value));
    }

    private static string FormatBool(bool value) => value ? "true" : "false";

    private static string FormatNumber(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);

    private static string? FormatNullableNumber(double? value) => value.HasValue ? FormatNumber(value.Value) : null;

    private static DW.HorizontalPosition CreateDocxHorizontalPosition(DocumentObjectLayout layout)
    {
        var hp = new DW.HorizontalPosition { RelativeFrom = ToDocxHorizontalRelative(layout.Position.HorizontalRelativeTo) };
        if (layout.Position.HorizontalAlignment.HasValue)
        {
            hp.Append(new DW.HorizontalAlignment(layout.Position.HorizontalAlignment.Value switch
            {
                DocumentImageHorizontalPosition.Left => "left",
                DocumentImageHorizontalPosition.Center => "center",
                DocumentImageHorizontalPosition.Right => "right",
                _ => "left"
            }));
        }
        else
        {
            hp.Append(new DW.PositionOffset(DocxUnitConverter.PointToEmu(layout.Position.X).ToString(CultureInfo.InvariantCulture)));
        }
        return hp;
    }

    private static DW.VerticalPosition CreateDocxVerticalPosition(DocumentObjectLayout layout)
    {
        var vp = new DW.VerticalPosition { RelativeFrom = ToDocxVerticalRelative(layout.Position.VerticalRelativeTo) };
        if (layout.Position.VerticalAlignment != DocumentObjectVerticalAlignment.None)
        {
            vp.Append(new DW.VerticalAlignment(layout.Position.VerticalAlignment switch
            {
                DocumentObjectVerticalAlignment.Top => "top",
                DocumentObjectVerticalAlignment.Middle => "center",
                DocumentObjectVerticalAlignment.Bottom => "bottom",
                _ => "top"
            }));
        }
        else
        {
            vp.Append(new DW.PositionOffset(DocxUnitConverter.PointToEmu(layout.Position.Y).ToString(CultureInfo.InvariantCulture)));
        }

        return vp;
    }

    private static OpenXmlElement CreateDocxWrap(DocumentObjectWrap wrap, DocxExtent extent)
    {
        var wrapText = ToDocxWrapText(wrap.Side);
        return wrap.Mode switch
        {
            DocumentWrapMode.TopBottom => new DW.WrapTopBottom(),
            DocumentWrapMode.BehindText or DocumentWrapMode.InFrontOfText => new DW.WrapNone(),
            DocumentWrapMode.Tight => new DW.WrapTight(CreateWrapPolygon(wrap, extent))
            {
                WrapText = wrapText,
                DistanceFromLeft = ToPositiveEmu(wrap.DistanceLeft),
                DistanceFromRight = ToPositiveEmu(wrap.DistanceRight)
            },
            DocumentWrapMode.Through => new DW.WrapThrough(CreateWrapPolygon(wrap, extent))
            {
                WrapText = wrapText,
                DistanceFromLeft = ToPositiveEmu(wrap.DistanceLeft),
                DistanceFromRight = ToPositiveEmu(wrap.DistanceRight)
            },
            _ => new DW.WrapSquare
            {
                WrapText = wrapText,
                DistanceFromLeft = ToPositiveEmu(wrap.DistanceLeft),
                DistanceFromRight = ToPositiveEmu(wrap.DistanceRight),
                DistanceFromTop = ToPositiveEmu(wrap.DistanceTop),
                DistanceFromBottom = ToPositiveEmu(wrap.DistanceBottom)
            }
        };
    }

    private static UInt32Value ToPositiveEmu(double points)
        => (UInt32Value)(uint)Math.Max(0L, DocxUnitConverter.PointToEmu(points));

    private static DW.WrapTextValues ToDocxWrapText(DocumentObjectWrapSide side)
        => side switch
        {
            DocumentObjectWrapSide.Left => DW.WrapTextValues.Left,
            DocumentObjectWrapSide.Right => DW.WrapTextValues.Right,
            DocumentObjectWrapSide.Largest => DW.WrapTextValues.Largest,
            _ => DW.WrapTextValues.BothSides
        };

    private static DW.WrapPolygon CreateWrapPolygon(DocumentObjectWrap wrap, DocxExtent extent)
    {
        var points = NormalizeWrapContourPoints(wrap.WrapContourPoints).ToList();
        var start = ToDocxWrapPoint<DW.StartPoint>(points[0], extent);
        var lines = points.Skip(1).Select(point => ToDocxWrapPoint<DW.LineTo>(point, extent)).Cast<OpenXmlElement>().ToArray();
        return new DW.WrapPolygon([start, .. lines])
        {
            Edited = wrap.WrapContourPoints.Count >= 3
        };
    }

    private static IEnumerable<DocumentObjectWrapPoint> NormalizeWrapContourPoints(IEnumerable<DocumentObjectWrapPoint>? points)
    {
        var normalized = (points ?? [])
            .Where(point => point is not null)
            .Select(point => new DocumentObjectWrapPoint
            {
                X = Math.Clamp(point.X, 0, 1),
                Y = Math.Clamp(point.Y, 0, 1)
            })
            .ToList();

        return normalized.Count >= 3
            ? normalized
            :
            [
                new() { X = 0, Y = 0 },
                new() { X = 1, Y = 0 },
                new() { X = 1, Y = 1 },
                new() { X = 0, Y = 1 }
            ];
    }

    private static TPoint ToDocxWrapPoint<TPoint>(DocumentObjectWrapPoint point, DocxExtent extent)
        where TPoint : DW.Point2DType, new()
        => new()
        {
            X = (Int64Value)(long)Math.Round(point.X * extent.Cx),
            Y = (Int64Value)(long)Math.Round(point.Y * extent.Cy)
        };

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

    private async Task<ResolvedDocxImage?> ResolveImageAsync(DocumentDrawingRun image, CancellationToken cancellationToken)
    {
        if (image.Source == DocumentImageSource.Asset && !string.IsNullOrWhiteSpace(image.AssetId) && _options.ImageResolver is not null)
        {
            var resolved = await ResolveAssetImageBytesAsync(image.AssetId, cancellationToken);
            if (resolved?.Content.Length > 0)
            {
                return ResolveImageBytes(
                    resolved.Content,
                    resolved.ContentType,
                    resolved.FileName,
                    image.AssetId,
                    image.ObjectId,
                    $"Resolved image asset '{image.AssetId}'");
            }

            return UsePlaceholderOrDrop(
                "docx.imageResolverEmpty",
                $"Image asset '{image.AssetId}' could not be resolved to exportable bytes.",
                image.AssetId,
                image.ObjectId);
        }

        if (!string.IsNullOrWhiteSpace(image.Url) && image.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            if (DocxImageContentTypeMapper.TryParseDataUrl(image.Url, Math.Max(1L, _options.MaxImagePartBytes), out var data, out var exceededLimit))
            {
                return ResolveImageBytes(
                    data.Content,
                    data.ContentType,
                    null,
                    "data-url",
                    image.ObjectId,
                    "Image data URL");
            }

            if (exceededLimit)
            {
                return UsePlaceholderOrDrop(
                    "docx.imagePartTooLarge",
                    $"Image data URL is larger than the configured export limit of {Math.Max(1L, _options.MaxImagePartBytes)} bytes.",
                    "data-url",
                    image.ObjectId);
            }

            return UsePlaceholderOrDrop(
                "docx.imageUnsupportedContentType",
                "Image data URL has an unsupported, unknown, or invalid image type.",
                "data-url",
                image.ObjectId);
        }

        if (!string.IsNullOrWhiteSpace(image.Url))
        {
            return UsePlaceholderOrDrop(
                "docx.imageExternalUrlUnsupported",
                "External image URLs are not downloaded during DOCX export.",
                image.Url,
                image.ObjectId);
        }

        return UsePlaceholderOrDrop(
            "docx.imageMissingContent",
            "Image does not contain exportable bytes.",
            image.AssetId ?? image.Url,
            image.ObjectId);
    }

    private async Task<DocumentFormatImageExportResult?> ResolveAssetImageBytesAsync(
        string assetId,
        CancellationToken cancellationToken)
    {
        if (_assetImageCache.TryGetValue(assetId, out var cached))
        {
            return cached;
        }

        var resolved = await _options.ImageResolver!(new DocumentFormatImageExportRequest { AssetId = assetId }, cancellationToken);
        _assetImageCache[assetId] = resolved;
        return resolved;
    }

    private ResolvedDocxImage? ResolveImageBytes(
        byte[] content,
        string? contentType,
        string? fileName,
        string sourcePath,
        string? objectId,
        string label)
    {
        var maxBytes = Math.Max(1L, _options.MaxImagePartBytes);
        if (content.LongLength > maxBytes)
        {
            return UsePlaceholderOrDrop(
                "docx.imagePartTooLarge",
                $"{label} is larger than the configured export limit of {maxBytes} bytes.",
                sourcePath,
                objectId);
        }

        if (DocxImageContentTypeMapper.HasContentTypeSignatureMismatch(contentType, content, out var detectedContentType))
        {
            return UsePlaceholderOrDrop(
                "docx.imageContentTypeMismatch",
                $"{label} declares content type '{contentType}' but its byte signature is '{detectedContentType}'.",
                sourcePath,
                objectId);
        }

        if (DocxImageContentTypeMapper.TryResolve(contentType, fileName, content, out var partInfo))
        {
            return new ResolvedDocxImage(content, partInfo);
        }

        return UsePlaceholderOrDrop(
            "docx.imageUnsupportedContentType",
            $"{label} has an unsupported or unknown image type.",
            sourcePath,
            objectId);
    }

    private ResolvedDocxImage? UsePlaceholderOrDrop(string code, string message, string? sourcePath, string? objectId)
    {
        if (_options.AllowImagePlaceholders)
        {
            _warnings.Add(Warning(
                code,
                $"{message} A transparent PNG placeholder was exported because AllowImagePlaceholders is enabled.",
                DocumentFormatCompatibilitySeverity.Warning,
                sourcePath,
                objectId));
            return new ResolvedDocxImage(TransparentPng, DocxImageContentTypeMapper.Png);
        }

        _warnings.Add(Warning(
            code,
            $"{message} The image was dropped because AllowImagePlaceholders is disabled.",
            DocumentFormatCompatibilitySeverity.Dropped,
            sourcePath,
            objectId));
        return null;
    }

    private static DocumentFormatCompatibilityWarning Warning(
        string code,
        string message,
        DocumentFormatCompatibilitySeverity severity,
        string? sourcePath = null,
        string? objectId = null)
        => new()
        {
            Code = code,
            Message = message,
            Severity = severity,
            SourcePath = sourcePath,
            ObjectId = objectId
        };

    private sealed record ResolvedDocxImage(byte[] Content, DocxImagePartInfo PartInfo);

    private sealed record DocxPartWriterContext(
        OpenXmlPartContainer OwnerPart,
        DocumentRenditionAnchorScope Region,
        string? HeaderFooterId = null,
        string? TableId = null,
        string? CellId = null)
    {
        public DocxPartWriterContext ForTableCell(string tableId, string cellId)
            => this with
            {
                Region = DocumentRenditionAnchorScope.TableCell,
                TableId = tableId,
                CellId = cellId
            };
    }

    private sealed record DocxPictureParts(string RelationshipId, DocxExtent Extent, DocxImagePartInfo PartInfo);

    private sealed record DocxExtent(long Cx, long Cy);

    private W.SectionProperties CreateSectionProperties()
    {
        var settings = _document.Sections.FirstOrDefault()?.Properties.PageSettings ?? _document.PageSettings;
        return new W.SectionProperties(
            new W.PageSize
            {
                Width = (UInt32Value)(uint)DocxUnitConverter.PointToTwip(settings.Size.Width),
                Height = (UInt32Value)(uint)DocxUnitConverter.PointToTwip(settings.Size.Height),
                Orient = settings.Landscape ? W.PageOrientationValues.Landscape : W.PageOrientationValues.Portrait
            },
            new W.PageMargin
            {
                Top = (Int32Value)DocxUnitConverter.PointToTwip(settings.Margins.Top),
                Right = (UInt32Value)(uint)DocxUnitConverter.PointToTwip(settings.Margins.Right),
                Bottom = (Int32Value)DocxUnitConverter.PointToTwip(settings.Margins.Bottom),
                Left = (UInt32Value)(uint)DocxUnitConverter.PointToTwip(settings.Margins.Left)
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

    private void AddSettingsPart()
    {
        if (!_document.IsProtected && _document.RestrictedMarkers.Count == 0)
        {
            return;
        }

        var settingsPart = _mainPart.AddNewPart<DocumentSettingsPart>();
        settingsPart.Settings = new W.Settings(
            new W.DocumentProtection
            {
                Edit = W.DocumentProtectionValues.ReadOnly,
                Enforcement = true
            });
        settingsPart.Settings.Save();
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

    private async Task AddHeadersFootersAsync(W.Body body, CancellationToken cancellationToken)
    {
        var section = body.Elements<W.SectionProperties>().LastOrDefault();
        if (section is null)
        {
            return;
        }

        foreach (var header in _document.HeadersFooters.Where(h => h.Type == DocumentHeaderFooterType.Header))
        {
            var part = _mainPart.AddNewPart<HeaderPart>();
            var context = new DocxPartWriterContext(part, DocumentRenditionAnchorScope.Header, HeaderFooterId: header.Id);
            part.Header = new W.Header();
            part.Header.AddNamespaceDeclaration(TempoPrefix, TempoNamespace);
            SetTempoAttribute(part.Header, "header-footer-id", header.Id);
            foreach (var block in header.Blocks.OrderBy(block => block.Order))
            {
                foreach (var element in await WriteBlockAsync(block, context, cancellationToken))
                {
                    part.Header.Append(element);
                }
            }

            if (!part.Header.ChildElements.Any())
            {
                part.Header.Append(new W.Paragraph());
            }

            part.Header.Save();
            section.PrependChild(new W.HeaderReference { Type = ToHeaderFooterValues(header.Scope), Id = _mainPart.GetIdOfPart(part) });
        }

        foreach (var footer in _document.HeadersFooters.Where(h => h.Type == DocumentHeaderFooterType.Footer))
        {
            var part = _mainPart.AddNewPart<FooterPart>();
            var context = new DocxPartWriterContext(part, DocumentRenditionAnchorScope.Footer, HeaderFooterId: footer.Id);
            part.Footer = new W.Footer();
            part.Footer.AddNamespaceDeclaration(TempoPrefix, TempoNamespace);
            SetTempoAttribute(part.Footer, "header-footer-id", footer.Id);
            foreach (var block in footer.Blocks.OrderBy(block => block.Order))
            {
                foreach (var element in await WriteBlockAsync(block, context, cancellationToken))
                {
                    part.Footer.Append(element);
                }
            }

            if (!part.Footer.ChildElements.Any())
            {
                part.Footer.Append(new W.Paragraph());
            }

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

    private async Task AddNotesPartsAsync(CancellationToken cancellationToken)
    {
        var footnotes = _document.Notes.Where(note => note.Type == DocumentNoteType.Footnote).ToList();
        if (footnotes.Count > 0)
        {
            var part = _mainPart.AddNewPart<FootnotesPart>();
            part.Footnotes = new W.Footnotes();
            for (var index = 0; index < footnotes.Count; index++)
            {
                var note = footnotes[index];
                var context = new DocxPartWriterContext(part, DocumentRenditionAnchorScope.Footnote);
                var footnote = new W.Footnote { Id = int.TryParse(note.Id, out var id) ? id : index + 1 };
                foreach (var block in note.Blocks.OrderBy(block => block.Order))
                {
                    foreach (var element in await WriteBlockAsync(block, context, cancellationToken))
                    {
                        footnote.Append(element);
                    }
                }

                if (!footnote.Elements<W.Paragraph>().Any())
                {
                    footnote.Append(new W.Paragraph());
                }

                part.Footnotes.Append(footnote);
            }

            part.Footnotes.Save();
        }

        var endnotes = _document.Notes.Where(note => note.Type == DocumentNoteType.Endnote).ToList();
        if (endnotes.Count > 0)
        {
            var part = _mainPart.AddNewPart<EndnotesPart>();
            part.Endnotes = new W.Endnotes();
            for (var index = 0; index < endnotes.Count; index++)
            {
                var note = endnotes[index];
                var context = new DocxPartWriterContext(part, DocumentRenditionAnchorScope.Endnote);
                var endnote = new W.Endnote { Id = int.TryParse(note.Id, out var id) ? id : index + 1 };
                foreach (var block in note.Blocks.OrderBy(block => block.Order))
                {
                    foreach (var element in await WriteBlockAsync(block, context, cancellationToken))
                    {
                        endnote.Append(element);
                    }
                }

                if (!endnote.Elements<W.Paragraph>().Any())
                {
                    endnote.Append(new W.Paragraph());
                }

                part.Endnotes.Append(endnote);
            }

            part.Endnotes.Save();
        }
    }

    private async Task AddCommentsPartAsync(CancellationToken cancellationToken)
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
        var context = new DocxPartWriterContext(part, DocumentRenditionAnchorScope.Comment);
        part.Comments = new W.Comments();
        foreach (var comment in _document.Comments)
        {
            var firstEntry = comment.Entries.FirstOrDefault();
            var docxComment = new W.Comment
            {
                Id = _commentIds[comment.Id],
                Author = firstEntry?.Author.DisplayName ?? string.Empty,
                Date = firstEntry?.CreatedAt.UtcDateTime ?? DateTime.UtcNow
            };
            foreach (var entry in comment.Entries)
            {
                if (entry.Inlines.Count > 0)
                {
                    docxComment.Append(await WriteParagraphAsync(entry.Inlines, context, cancellationToken: cancellationToken));
                }
                else
                {
                    docxComment.Append(new W.Paragraph(new W.Run(new W.Text(entry.Text ?? string.Empty))));
                }
            }

            if (!docxComment.Elements<W.Paragraph>().Any())
            {
                docxComment.Append(new W.Paragraph());
            }

            part.Comments.Append(docxComment);
        }

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
