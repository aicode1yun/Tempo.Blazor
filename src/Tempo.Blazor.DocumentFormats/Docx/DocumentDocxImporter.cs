using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using System.Globalization;
using System.Text;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Internal;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Docx;

/// <summary>Imports DOCX packages into the document editor JSON model.</summary>
public sealed class DocumentDocxImporter : IDocumentFormatImporter
{
    /// <inheritdoc />
    public async Task<DocumentFormatImportResult> ImportAsync(Stream stream, DocumentFormatImportOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DocumentFormatImportOptions();

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        using var word = WordprocessingDocument.Open(memory, false);
        var reader = new DocxPackageReader(word, options);
        return await reader.ReadAsync(cancellationToken);
    }
}

/// <summary>Reads WordprocessingML package parts into the editor model.</summary>
public sealed class DocxPackageReader
{
    private const string TempoNamespace = "urn:tempo-blazor:document-editor:1.0";

    private readonly WordprocessingDocument _document;
    private readonly DocumentFormatImportOptions _options;
    private readonly List<DocumentFormatCompatibilityWarning> _warnings = [];
    private readonly List<DocumentFormatPreservedPart> _preservedParts = [];
    private readonly Dictionary<string, string> _hyperlinks = new(StringComparer.Ordinal);
    private int _order;

    /// <summary>Creates a DOCX package reader.</summary>
    public DocxPackageReader(WordprocessingDocument document, DocumentFormatImportOptions options)
    {
        _document = document;
        _options = options;
    }

    /// <summary>Reads the package.</summary>
    public async Task<DocumentFormatImportResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var doc = DocumentEditorDocument.Empty(_options.DocumentId);
        doc.Metadata.Title = GetTitleFallback();
        doc.Metadata.ModifiedAt = DateTimeOffset.UtcNow;

        var main = _document.MainDocumentPart;
        if (main?.Document.Body is null)
        {
            _warnings.Add(Warning("docx.empty", "DOCX package does not contain a main document body.", DocumentFormatCompatibilitySeverity.Dropped, "word/document.xml"));
            return Result(doc);
        }

        ReadProtectionSettings(doc, main);

        foreach (var relationship in main.HyperlinkRelationships)
        {
            _hyperlinks[relationship.Id] = relationship.Uri.ToString();
        }

        foreach (var element in main.Document.Body.Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element is W.Paragraph paragraph)
            {
                var blocks = await ReadParagraphAsync(paragraph, main, cancellationToken);
                doc.Blocks.AddRange(blocks);
            }
            else if (element is W.Table table)
            {
                doc.Blocks.Add(ReadTable(table));
            }
            else if (element is W.SdtBlock sdtBlock)
            {
                var firstBlockIndex = doc.Blocks.Count;
                foreach (var child in sdtBlock.GetFirstChild<W.SdtContentBlock>()?.Elements() ?? Enumerable.Empty<OpenXmlElement>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (child is W.Paragraph sdtParagraph)
                    {
                        doc.Blocks.AddRange(await ReadParagraphAsync(sdtParagraph, main, cancellationToken));
                    }
                    else if (child is W.Table sdtTable)
                    {
                        doc.Blocks.Add(ReadTable(sdtTable));
                    }
                }

                if (TryReadEditableRegion(sdtBlock, doc.Blocks.Skip(firstBlockIndex).ToList(), out var marker))
                {
                    doc.IsProtected = true;
                    doc.RestrictedMarkers.Add(marker);
                }
            }
            else if (element is not W.SectionProperties)
            {
                _warnings.Add(Warning(
                    "docx.unsupportedBodyElement",
                    $"DOCX body element '{element.LocalName}' is not mapped into the editor model.",
                    DocumentFormatCompatibilitySeverity.Warning,
                    "word/document.xml"));
            }
        }

        doc.HeadersFooters.AddRange(ReadHeadersFooters(main));
        doc.Notes.AddRange(ReadNotes(main));
        doc.Comments.AddRange(ReadComments(main));
        doc.Revisions.AddRange(ReadRevisions(main.Document.Body));
        ReadSectionProperties(doc, main.Document.Body);
        PreserveUnsupportedParts();

        if (doc.Blocks.Count == 0)
        {
            doc.Blocks.Add(DocumentModelText.Paragraph(string.Empty, _order++));
        }

        return Result(doc);
    }

    private static void ReadProtectionSettings(DocumentEditorDocument doc, MainDocumentPart main)
    {
        var protection = main.DocumentSettingsPart?.Settings?.GetFirstChild<W.DocumentProtection>();
        if (protection?.Enforcement?.Value == true)
        {
            doc.IsProtected = true;
        }
    }

    private static bool TryReadEditableRegion(W.SdtBlock sdtBlock, IReadOnlyList<DocumentBlock> blocks, out DocumentRestrictedMarker marker)
    {
        marker = new DocumentRestrictedMarker();
        if (blocks.Count == 0)
        {
            return false;
        }

        var tag = sdtBlock.SdtProperties?.GetFirstChild<W.Tag>()?.Val?.Value;
        if (string.IsNullOrWhiteSpace(tag) || !tag.StartsWith("tm-editable:", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = tag.Split(':');
        var markerId = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : Guid.NewGuid().ToString("N");
        var startOffset = parts.Length > 2 && int.TryParse(parts[2], out var parsedStart) ? parsedStart : 0;
        var endOffset = parts.Length > 3 && int.TryParse(parts[3], out var parsedEnd) ? parsedEnd : GetBlockTextLength(blocks[^1]);
        var label = sdtBlock.SdtProperties?.GetFirstChild<W.SdtAlias>()?.Val?.Value;

        marker = new DocumentRestrictedMarker
        {
            Id = markerId,
            StartBlockId = blocks[0].Id,
            StartOffset = Math.Max(0, startOffset),
            EndBlockId = blocks[^1].Id,
            EndOffset = Math.Max(0, endOffset),
            Label = label
        };
        return true;
    }

    private static int GetBlockTextLength(DocumentBlock block)
        => DocumentModelText.GetBlockText(block).Length;

    private DocumentFormatImportResult Result(DocumentEditorDocument doc)
    {
        return new DocumentFormatImportResult
        {
            Document = doc,
            Format = DocumentFormatKind.Docx,
            Warnings = _warnings,
            PreservedParts = _preservedParts
        };
    }

    private string GetTitleFallback()
    {
        var title = _document.PackageProperties.Title;
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        if (!string.IsNullOrWhiteSpace(_options.FileName))
        {
            return Path.GetFileNameWithoutExtension(_options.FileName);
        }

        return "Imported DOCX";
    }

    private async Task<List<DocumentBlock>> ReadParagraphAsync(W.Paragraph paragraph, MainDocumentPart mainPart, CancellationToken cancellationToken)
    {
        var blocks = new List<DocumentBlock>();
        var pageBreakSeen = false;
        if (paragraph.Descendants<W.Break>().Any(b => b.Type?.Value == W.BreakValues.Page))
        {
            pageBreakSeen = true;
        }

        foreach (var drawing in paragraph.Descendants<W.Drawing>())
        {
            var image = await ReadImageAsync(drawing, mainPart, cancellationToken);
            if (image is not null)
            {
                blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Image,
                    Order = _order++,
                    Content = image
                });
            }
        }

        var inlines = ReadInlines(paragraph.ChildElements);
        if (blocks.Count == 1
            && blocks[0].Content is ImageBlockContent imageBlock
            && TryReadImageCaption(paragraph, inlines, out var caption))
        {
            imageBlock.Caption = caption;
            inlines.Clear();
        }

        if (inlines.Count > 0 || blocks.Count == 0)
        {
            blocks.Insert(0, new DocumentBlock
            {
                Type = GetParagraphType(paragraph, out var headingLevel, out var ordered, out var indent),
                Order = _order++,
                Content = CreateTextContent(paragraph, inlines, headingLevel, ordered, indent)
            });
        }

        if (pageBreakSeen)
        {
            blocks.Add(new DocumentBlock
            {
                Type = DocumentBlockType.PageBreak,
                Order = _order++,
                Content = new PageBreakBlockContent()
            });
        }

        return blocks;
    }

    private static bool TryReadImageCaption(W.Paragraph paragraph, IReadOnlyList<InlineContent> inlines, out string caption)
    {
        caption = string.Empty;
        var textAfterDrawing = new StringBuilder();
        var seenDrawing = false;

        foreach (var child in paragraph.ChildElements)
        {
            if (child.Descendants<W.Drawing>().Any())
            {
                seenDrawing = true;
                continue;
            }

            if (seenDrawing)
            {
                foreach (var text in child.Descendants<W.Text>())
                {
                    textAfterDrawing.Append(text.Text);
                }
            }
        }

        caption = textAfterDrawing.ToString().Trim();
        if (string.IsNullOrWhiteSpace(caption))
        {
            return false;
        }

        var inlineText = string.Concat(inlines.OfType<TextRun>().Select(run => run.Text)).Trim();
        return string.Equals(inlineText, caption, StringComparison.Ordinal);
    }

    private static DocumentBlockType GetParagraphType(W.Paragraph paragraph, out int headingLevel, out bool ordered, out int indent)
    {
        headingLevel = 0;
        ordered = false;
        indent = 0;

        var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (!string.IsNullOrWhiteSpace(style) && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(new string(style.Where(char.IsDigit).ToArray()), out var parsed))
            {
                headingLevel = Math.Clamp(parsed, 1, 6);
            }
            else
            {
                headingLevel = 1;
            }

            return DocumentBlockType.Heading;
        }

        var numbering = paragraph.ParagraphProperties?.NumberingProperties;
        if (numbering is not null)
        {
            ordered = numbering.NumberingId?.Val?.Value != 1;
            indent = numbering.NumberingLevelReference?.Val?.Value ?? 0;
            return DocumentBlockType.List;
        }

        return DocumentBlockType.Paragraph;
    }

    private static DocumentBlockContent CreateTextContent(W.Paragraph paragraph, List<InlineContent> inlines, int headingLevel, bool ordered, int indent)
    {
        return GetParagraphType(paragraph, out _, out _, out _) switch
        {
            DocumentBlockType.Heading => new HeadingBlockContent { Level = headingLevel <= 0 ? 1 : headingLevel, Inlines = inlines },
            DocumentBlockType.List => new ListBlockContent { Ordered = ordered, IndentLevel = indent, Inlines = inlines },
            _ => new ParagraphBlockContent { Inlines = inlines }
        };
    }

    private List<InlineContent> ReadInlines(IEnumerable<OpenXmlElement> elements, List<InlineMark>? inheritedMarks = null)
    {
        var result = new List<InlineContent>();
        var inherited = inheritedMarks ?? [];
        string? activeCommentId = null;

        foreach (var element in elements)
        {
            if (element is W.CommentRangeStart commentStart)
            {
                activeCommentId = commentStart.Id?.Value;
                continue;
            }

            if (element is W.CommentRangeEnd)
            {
                activeCommentId = null;
                continue;
            }

            var currentInherited = !string.IsNullOrWhiteSpace(activeCommentId)
                ? MergeMarks(inherited, [new InlineMark { Type = InlineMarkType.CommentAnchor, CommentAnchor = new CommentAnchorMarkData { CommentId = activeCommentId, AnchorId = activeCommentId } }])
                : inherited;

            if (element is W.Run run)
            {
                var marks = MergeMarks(currentInherited, ReadRunMarks(run.RunProperties));
                if (run.Descendants<W.Drawing>().Any())
                {
                    continue;
                }

                var text = string.Concat(run.Elements<W.Text>().Select(t => t.Text));
                if (!string.IsNullOrEmpty(text))
                {
                    result.Add(new TextRun { Text = text, Marks = marks });
                }

                foreach (var noteRef in run.Elements<W.FootnoteReference>())
                {
                    result.Add(new DocumentNoteReferenceRun { NoteId = noteRef.Id?.ToString() ?? string.Empty, NoteType = DocumentNoteType.Footnote });
                }

                foreach (var noteRef in run.Elements<W.EndnoteReference>())
                {
                    result.Add(new DocumentNoteReferenceRun { NoteId = noteRef.Id?.ToString() ?? string.Empty, NoteType = DocumentNoteType.Endnote });
                }
            }
            else if (element is W.Hyperlink hyperlink)
            {
                var href = hyperlink.Id is not null && _hyperlinks.TryGetValue(hyperlink.Id!, out var link)
                    ? link
                    : hyperlink.Anchor?.Value ?? string.Empty;
                var linkMarks = MergeMarks(currentInherited, [new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = href } }]);
                result.AddRange(ReadInlines(hyperlink.ChildElements, linkMarks));
            }
            else if (element is W.InsertedRun inserted)
            {
                var revisionId = $"docx-rev-{inserted.Id?.Value ?? Guid.NewGuid().ToString("N")}";
                var marks = MergeMarks(currentInherited, [new InlineMark { Type = InlineMarkType.Revision, RevisionId = revisionId }]);
                result.AddRange(ReadInlines(inserted.ChildElements, marks));
            }
            else if (element is W.DeletedRun deleted)
            {
                var revisionId = $"docx-rev-{deleted.Id?.Value ?? Guid.NewGuid().ToString("N")}";
                var marks = MergeMarks(currentInherited, [new InlineMark { Type = InlineMarkType.Revision, RevisionId = revisionId }]);
                result.AddRange(ReadInlines(deleted.ChildElements, marks));
            }
        }

        return result;
    }

    private static List<InlineMark> ReadRunMarks(W.RunProperties? properties)
    {
        var marks = new List<InlineMark>();
        if (properties is null)
        {
            return marks;
        }

        if (properties.Bold is not null) marks.Add(new InlineMark { Type = InlineMarkType.Bold });
        if (properties.Italic is not null) marks.Add(new InlineMark { Type = InlineMarkType.Italic });
        if (properties.Underline is not null) marks.Add(new InlineMark { Type = InlineMarkType.Underline });
        if (properties.Strike is not null) marks.Add(new InlineMark { Type = InlineMarkType.Strikethrough });
        if (properties.VerticalTextAlignment?.Val?.Value == W.VerticalPositionValues.Superscript) marks.Add(new InlineMark { Type = InlineMarkType.Superscript });
        if (properties.VerticalTextAlignment?.Val?.Value == W.VerticalPositionValues.Subscript) marks.Add(new InlineMark { Type = InlineMarkType.Subscript });
        if (!string.IsNullOrWhiteSpace(properties.Color?.Val?.Value)) marks.Add(new InlineMark { Type = InlineMarkType.TextColor, Value = $"#{properties.Color.Val.Value}" });
        if (properties.Highlight?.Val is not null) marks.Add(new InlineMark { Type = InlineMarkType.Highlight, Value = properties.Highlight.Val.Value.ToString() });
        return marks;
    }

    private static List<InlineMark> MergeMarks(IEnumerable<InlineMark> left, IEnumerable<InlineMark> right)
    {
        return left.Concat(right.Select(CloneMark)).Select(CloneMark).ToList();
    }

    private static InlineMark CloneMark(InlineMark mark)
    {
        return new InlineMark
        {
            Type = mark.Type,
            Value = mark.Value,
            RevisionId = mark.RevisionId,
            Link = mark.Link is null ? null : new LinkMarkData { Href = mark.Link.Href, Title = mark.Link.Title },
            CommentAnchor = mark.CommentAnchor is null ? null : new CommentAnchorMarkData { CommentId = mark.CommentAnchor.CommentId, AnchorId = mark.CommentAnchor.AnchorId }
        };
    }

    private DocumentBlock ReadTable(W.Table table)
    {
        var tableProperties = table.GetFirstChild<W.TableProperties>();
        var tableLayout = new TableLayoutContent
        {
            Width = TwipsToNullablePoints(tableProperties?.GetFirstChild<W.TableWidth>()?.Width?.Value),
            Alignment = FromDocxTableAlignment(tableProperties?.GetFirstChild<W.TableJustification>()?.Val?.Value),
            BackgroundColor = FromDocxColor(tableProperties?.GetFirstChild<W.Shading>()?.Fill?.Value)
        };
        var rows = new List<TableRowContent>();
        foreach (var row in table.Elements<W.TableRow>())
        {
            var cells = new List<TableCellContent>();
            foreach (var cell in row.Elements<W.TableCell>())
            {
                var properties = cell.TableCellProperties;
                var columnSpan = Math.Max(1, properties?.GridSpan?.Val?.Value ?? 1);
                var verticalMerge = properties?.VerticalMerge;
                var rowSpan = verticalMerge?.Val?.Value == W.MergedCellValues.Restart ? 2 : 1;
                var blocks = cell.Elements<W.Paragraph>()
                    .Select(p => new DocumentBlock
                    {
                        Type = DocumentBlockType.Paragraph,
                        Order = 0,
                        Content = new ParagraphBlockContent { Inlines = ReadInlines(p.ChildElements) }
                    })
                    .ToList();

                cells.Add(new TableCellContent
                {
                    ColumnSpan = columnSpan,
                    RowSpan = rowSpan,
                    Merge = new TableCellMerge { IsOrigin = verticalMerge?.Val?.Value != W.MergedCellValues.Continue },
                    Width = TwipsToNullablePoints(properties?.GetFirstChild<W.TableCellWidth>()?.Width?.Value),
                    BackgroundColor = FromDocxColor(properties?.GetFirstChild<W.Shading>()?.Fill?.Value),
                    VerticalAlignment = FromDocxCellVerticalAlignment(properties?.GetFirstChild<W.TableCellVerticalAlignment>()?.Val?.Value),
                    Blocks = blocks.Count == 0 ? [DocumentModelText.Paragraph(string.Empty)] : blocks
                });
            }

            rows.Add(new TableRowContent { Cells = cells });
        }

        return new DocumentBlock
        {
            Type = DocumentBlockType.Table,
            Order = _order++,
            Content = new TableBlockContent { Layout = tableLayout, Rows = rows }
        };
    }

    private static double? TwipsToNullablePoints(string? value)
    {
        return double.TryParse(value, out var twips) && twips > 0
            ? Math.Round(twips / 20d, 2)
            : null;
    }

    private static TableHorizontalAlignment FromDocxTableAlignment(W.TableRowAlignmentValues? value)
    {
        if (value == W.TableRowAlignmentValues.Center)
        {
            return TableHorizontalAlignment.Center;
        }

        if (value == W.TableRowAlignmentValues.Right)
        {
            return TableHorizontalAlignment.Right;
        }

        return TableHorizontalAlignment.Left;
    }

    private static TableCellVerticalAlignment FromDocxCellVerticalAlignment(W.TableVerticalAlignmentValues? value)
    {
        if (value == W.TableVerticalAlignmentValues.Center)
        {
            return TableCellVerticalAlignment.Middle;
        }

        if (value == W.TableVerticalAlignmentValues.Bottom)
        {
            return TableCellVerticalAlignment.Bottom;
        }

        return TableCellVerticalAlignment.Top;
    }

    private static string? FromDocxColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var color = value.Trim();
        return color.Length == 6 && color.All(Uri.IsHexDigit)
            ? $"#{color.ToUpperInvariant()}"
            : null;
    }

    private async Task<ImageBlockContent?> ReadImageAsync(W.Drawing drawing, MainDocumentPart mainPart, CancellationToken cancellationToken)
    {
        var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        var relationshipId = blip?.Embed?.Value;
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            return null;
        }

        if (mainPart.GetPartById(relationshipId) is not ImagePart imagePart)
        {
            return null;
        }

        await using var stream = imagePart.GetStream(FileMode.Open, FileAccess.Read);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var fileName = Path.GetFileName(imagePart.Uri.ToString());
        var assetId = relationshipId;
        string? url = null;

        if (_options.ImageImporter is not null)
        {
            var imported = await _options.ImageImporter(new DocumentFormatImageImportRequest
            {
                SourcePath = imagePart.Uri.ToString(),
                ContentType = imagePart.ContentType,
                Content = bytes,
                FileName = fileName
            }, cancellationToken);
            assetId = imported.AssetId ?? assetId;
            url = imported.Url;
        }
        else
        {
            url = $"data:{imagePart.ContentType};base64,{Convert.ToBase64String(bytes)}";
        }

        var layout = ReadObjectLayout(drawing);
        var extent = drawing.Descendants<DW.Extent>().FirstOrDefault();
        var size = new DocumentImageSize
        {
            Width = extent?.Cx is null ? 120 : Math.Round(extent.Cx.Value / 12700d, 2),
            Height = extent?.Cy is null ? 90 : Math.Round(extent.Cy.Value / 12700d, 2)
        };
        layout.Transform.Width = size.Width;
        layout.Transform.Height = size.Height;

        return new ImageBlockContent
        {
            Source = url is not null ? DocumentImageSource.Url : DocumentImageSource.Asset,
            Url = url,
            AssetId = url is null ? assetId : null,
            AltText = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties>().FirstOrDefault()?.Description?.Value,
            Size = size,
            Layout = layout
        };
    }

    private static DocumentObjectLayout ReadObjectLayout(W.Drawing drawing)
    {
        var anchor = drawing.Descendants<DW.Anchor>().FirstOrDefault();
        var inline = drawing.Descendants<DW.Inline>().FirstOrDefault();
        var layoutElement = (OpenXmlElement?)anchor ?? inline;
        if (layoutElement is null)
        {
            return DocumentObjectLayout.Inline();
        }

        if (anchor is null)
        {
            return ReadTempoLayout(
                layoutElement,
                drawing,
                fallbackKind: DocumentObjectLayoutKind.Inline,
                fallbackWrapMode: DocumentWrapMode.Inline,
                fallbackHorizontalPosition: null,
                fallbackHorizontalRelativeTo: DocumentRelativePosition.Page,
                fallbackVerticalRelativeTo: DocumentRelativePosition.Paragraph,
                fallbackX: 0,
                fallbackY: 0,
                fallbackDistanceLeft: 0,
                fallbackDistanceRight: 0,
                fallbackDistanceTop: 0,
                fallbackDistanceBottom: 0,
                fallbackZIndex: 0,
                fallbackAllowOverlap: false,
                fallbackLockAnchor: false);
        }

        var horizontal = anchor.GetFirstChild<DW.HorizontalPosition>();
        var vertical = anchor.GetFirstChild<DW.VerticalPosition>();
        var fallbackWrapMode = anchor.Descendants<DW.WrapTopBottom>().Any()
            ? DocumentWrapMode.TopBottom
            : anchor.BehindDoc?.Value == true
                ? DocumentWrapMode.BehindText
                : anchor.Descendants<DW.WrapNone>().Any()
                    ? DocumentWrapMode.InFrontOfText
                    : DocumentWrapMode.Square;

        var hAlign = horizontal?.GetFirstChild<DW.HorizontalAlignment>()?.Text?.Trim().ToLowerInvariant();
        DocumentImageHorizontalPosition? horizontalPosition = hAlign switch
        {
            "left" => DocumentImageHorizontalPosition.Left,
            "center" => DocumentImageHorizontalPosition.Center,
            "right" => DocumentImageHorizontalPosition.Right,
            _ => null
        };

        return ReadTempoLayout(
            layoutElement,
            drawing,
            fallbackKind: DocumentObjectLayoutKind.Anchored,
            fallbackWrapMode: fallbackWrapMode,
            fallbackHorizontalPosition: horizontalPosition,
            fallbackHorizontalRelativeTo: FromDocxHorizontalRelative(horizontal?.RelativeFrom?.Value),
            fallbackVerticalRelativeTo: FromDocxVerticalRelative(vertical?.RelativeFrom?.Value),
            fallbackX: EmuToPoint(horizontal?.GetFirstChild<DW.PositionOffset>()?.Text),
            fallbackY: EmuToPoint(vertical?.GetFirstChild<DW.PositionOffset>()?.Text),
            fallbackDistanceLeft: EmuToPoint(anchor.DistanceFromLeft?.Value.ToString(CultureInfo.InvariantCulture)),
            fallbackDistanceRight: EmuToPoint(anchor.DistanceFromRight?.Value.ToString(CultureInfo.InvariantCulture)),
            fallbackDistanceTop: EmuToPoint(anchor.DistanceFromTop?.Value.ToString(CultureInfo.InvariantCulture)),
            fallbackDistanceBottom: EmuToPoint(anchor.DistanceFromBottom?.Value.ToString(CultureInfo.InvariantCulture)),
            fallbackZIndex: (int)(anchor.RelativeHeight?.Value ?? 0),
            fallbackAllowOverlap: anchor.AllowOverlap?.Value == true,
            fallbackLockAnchor: anchor.Locked?.Value == true);
    }

    private static DocumentObjectLayout ReadTempoLayout(
        OpenXmlElement element,
        W.Drawing drawing,
        DocumentObjectLayoutKind fallbackKind,
        DocumentWrapMode fallbackWrapMode,
        DocumentImageHorizontalPosition? fallbackHorizontalPosition,
        DocumentRelativePosition fallbackHorizontalRelativeTo,
        DocumentRelativePosition fallbackVerticalRelativeTo,
        double fallbackX,
        double fallbackY,
        double fallbackDistanceLeft,
        double fallbackDistanceRight,
        double fallbackDistanceTop,
        double fallbackDistanceBottom,
        int fallbackZIndex,
        bool fallbackAllowOverlap,
        bool fallbackLockAnchor)
    {
        var kind = ParseEnum(GetTempoAttribute(element, "layout-kind"), fallbackKind);
        var horizontalPosition = ParseNullableEnum<DocumentImageHorizontalPosition>(GetTempoAttribute(element, "horizontal-alignment"))
            ?? fallbackHorizontalPosition;

        return new DocumentObjectLayout
        {
            Kind = kind,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = GetTempoAttribute(element, "anchor-block-id"),
                InlineIndex = ParseNullableInt(GetTempoAttribute(element, "anchor-inline-index")),
                Offset = ParseNullableInt(GetTempoAttribute(element, "anchor-offset")),
                Region = ParseEnum(GetTempoAttribute(element, "anchor-region"), DocumentRenditionAnchorScope.Body),
                MoveWithText = ParseBool(GetTempoAttribute(element, "move-with-text"), kind != DocumentObjectLayoutKind.Fixed),
                FixedOnPage = ParseBool(GetTempoAttribute(element, "fixed-on-page"), kind == DocumentObjectLayoutKind.Fixed),
                LockAnchor = ParseBool(GetTempoAttribute(element, "lock-anchor"), fallbackLockAnchor)
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = ParseEnum(GetTempoAttribute(element, "horizontal-relative-to"), fallbackHorizontalRelativeTo),
                VerticalRelativeTo = ParseEnum(GetTempoAttribute(element, "vertical-relative-to"), fallbackVerticalRelativeTo),
                X = ParseDouble(GetTempoAttribute(element, "x"), fallbackX),
                Y = ParseDouble(GetTempoAttribute(element, "y"), fallbackY),
                HorizontalAlignment = horizontalPosition,
                VerticalAlignment = ParseEnum(GetTempoAttribute(element, "vertical-alignment"), DocumentObjectVerticalAlignment.None)
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = ParseEnum(GetTempoAttribute(element, "wrap-mode"), fallbackWrapMode),
                DistanceLeft = ParseDouble(GetTempoAttribute(element, "distance-left"), fallbackDistanceLeft),
                DistanceRight = ParseDouble(GetTempoAttribute(element, "distance-right"), fallbackDistanceRight),
                DistanceTop = ParseDouble(GetTempoAttribute(element, "distance-top"), fallbackDistanceTop),
                DistanceBottom = ParseDouble(GetTempoAttribute(element, "distance-bottom"), fallbackDistanceBottom)
            },
            Transform = new DocumentObjectTransform
            {
                Width = ParseNullableDouble(GetTempoAttribute(element, "width")),
                Height = ParseNullableDouble(GetTempoAttribute(element, "height")),
                NaturalWidth = ParseNullableDouble(GetTempoAttribute(element, "natural-width")),
                NaturalHeight = ParseNullableDouble(GetTempoAttribute(element, "natural-height")),
                LockAspectRatio = ParseBool(GetTempoAttribute(element, "lock-aspect-ratio"), true),
                Rotation = ParseDouble(GetTempoAttribute(element, "rotation"), ReadDrawingRotation(drawing))
            },
            Stacking = new DocumentObjectStacking
            {
                ZIndex = ParseInt(GetTempoAttribute(element, "z-index"), fallbackZIndex),
                AllowOverlap = ParseBool(GetTempoAttribute(element, "allow-overlap"), fallbackAllowOverlap)
            }
        };
    }

    private static string? GetTempoAttribute(OpenXmlElement element, string name)
    {
        var attribute = element.GetAttributes()
            .FirstOrDefault(attribute => attribute.LocalName == name && attribute.NamespaceUri == TempoNamespace);
        return string.IsNullOrWhiteSpace(attribute.Value) ? null : attribute.Value;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static TEnum? ParseNullableEnum<TEnum>(string? value)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;
    }

    private static bool ParseBool(string? value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static double ParseDouble(string? value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static double? ParseNullableDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static double ReadDrawingRotation(W.Drawing drawing)
    {
        var rotation = drawing.Descendants<A.Transform2D>().FirstOrDefault()?.Rotation?.Value;
        return rotation.HasValue ? Math.Round(rotation.Value / 60000d, 4) : 0;
    }

    private static double EmuToPoint(string? value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var emu)
            ? Math.Round(emu / 12700d, 2)
            : 0;
    }

    private static DocumentRelativePosition FromDocxHorizontalRelative(DW.HorizontalRelativePositionValues? value)
    {
        if (value == DW.HorizontalRelativePositionValues.Margin)
        {
            return DocumentRelativePosition.Margin;
        }

        if (value == DW.HorizontalRelativePositionValues.Column)
        {
            return DocumentRelativePosition.Column;
        }

        if (value == DW.HorizontalRelativePositionValues.Character)
        {
            return DocumentRelativePosition.Character;
        }

        return DocumentRelativePosition.Page;
    }

    private static DocumentRelativePosition FromDocxVerticalRelative(DW.VerticalRelativePositionValues? value)
    {
        if (value == DW.VerticalRelativePositionValues.Margin)
        {
            return DocumentRelativePosition.Margin;
        }

        if (value == DW.VerticalRelativePositionValues.Line)
        {
            return DocumentRelativePosition.Line;
        }

        if (value == DW.VerticalRelativePositionValues.Page)
        {
            return DocumentRelativePosition.Page;
        }

        return DocumentRelativePosition.Paragraph;
    }

    private List<DocumentHeaderFooter> ReadHeadersFooters(MainDocumentPart mainPart)
    {
        var result = new List<DocumentHeaderFooter>();
        var sectionProperties = mainPart.Document.Body?.Elements<W.SectionProperties>().LastOrDefault();
        var headerScopes = sectionProperties?.Elements<W.HeaderReference>()
            .Where(reference => reference.Id is not null)
            .ToDictionary(reference => reference.Id!.Value!, reference => FromHeaderFooterValues(reference.Type?.Value), StringComparer.Ordinal)
            ?? [];
        var footerScopes = sectionProperties?.Elements<W.FooterReference>()
            .Where(reference => reference.Id is not null)
            .ToDictionary(reference => reference.Id!.Value!, reference => FromHeaderFooterValues(reference.Type?.Value), StringComparer.Ordinal)
            ?? [];

        foreach (var part in mainPart.HeaderParts)
        {
            var relationshipId = mainPart.GetIdOfPart(part);
            result.Add(new DocumentHeaderFooter
            {
                Type = DocumentHeaderFooterType.Header,
                Scope = headerScopes.GetValueOrDefault(relationshipId, DocumentHeaderFooterScope.Primary),
                Blocks = part.Header?.Elements<W.Paragraph>().Select((p, i) => new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Order = i,
                    Content = new ParagraphBlockContent { Inlines = ReadInlines(p.ChildElements) }
                }).ToList() ?? []
            });
        }

        foreach (var part in mainPart.FooterParts)
        {
            var relationshipId = mainPart.GetIdOfPart(part);
            result.Add(new DocumentHeaderFooter
            {
                Type = DocumentHeaderFooterType.Footer,
                Scope = footerScopes.GetValueOrDefault(relationshipId, DocumentHeaderFooterScope.Primary),
                Blocks = part.Footer?.Elements<W.Paragraph>().Select((p, i) => new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Order = i,
                    Content = new ParagraphBlockContent { Inlines = ReadInlines(p.ChildElements) }
                }).ToList() ?? []
            });
        }

        return result;
    }

    private static DocumentHeaderFooterScope FromHeaderFooterValues(W.HeaderFooterValues? value)
    {
        if (value == W.HeaderFooterValues.First)
        {
            return DocumentHeaderFooterScope.FirstPage;
        }

        if (value == W.HeaderFooterValues.Even)
        {
            return DocumentHeaderFooterScope.EvenPages;
        }

        return DocumentHeaderFooterScope.Primary;
    }

    private List<DocumentNote> ReadNotes(MainDocumentPart mainPart)
    {
        var result = new List<DocumentNote>();
        if (mainPart.FootnotesPart?.Footnotes is not null)
        {
            foreach (var footnote in mainPart.FootnotesPart.Footnotes.Elements<W.Footnote>().Where(f => f.Id?.Value > 0))
            {
                result.Add(new DocumentNote
                {
                    Id = footnote.Id?.Value.ToString() ?? Guid.NewGuid().ToString("N"),
                    Type = DocumentNoteType.Footnote,
                    Blocks = footnote.Elements<W.Paragraph>().Select((p, i) => new DocumentBlock
                    {
                        Type = DocumentBlockType.Paragraph,
                        Order = i,
                        Content = new ParagraphBlockContent { Inlines = ReadInlines(p.ChildElements) }
                    }).ToList()
                });
            }
        }

        if (mainPart.EndnotesPart?.Endnotes is not null)
        {
            foreach (var endnote in mainPart.EndnotesPart.Endnotes.Elements<W.Endnote>().Where(f => f.Id?.Value > 0))
            {
                result.Add(new DocumentNote
                {
                    Id = endnote.Id?.Value.ToString() ?? Guid.NewGuid().ToString("N"),
                    Type = DocumentNoteType.Endnote,
                    Blocks = endnote.Elements<W.Paragraph>().Select((p, i) => new DocumentBlock
                    {
                        Type = DocumentBlockType.Paragraph,
                        Order = i,
                        Content = new ParagraphBlockContent { Inlines = ReadInlines(p.ChildElements) }
                    }).ToList()
                });
            }
        }

        return result;
    }

    private List<DocumentComment> ReadComments(MainDocumentPart mainPart)
    {
        var comments = mainPart.WordprocessingCommentsPart?.Comments;
        if (comments is null)
        {
            return [];
        }

        return comments.Elements<W.Comment>().Select(comment => new DocumentComment
        {
            Id = comment.Id?.Value ?? Guid.NewGuid().ToString("N"),
            SourceFormat = "docx",
            ExternalId = comment.Id?.Value,
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.ImportedDocx,
                ExternalAnchorId = comment.Id?.Value
            },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor
                    {
                        DisplayName = comment.Author?.Value ?? string.Empty
                    },
                    Text = string.Join("\n", comment.Descendants<W.Text>().Select(t => t.Text)),
                    CreatedAt = comment.Date?.Value ?? DateTimeOffset.UtcNow
                }
            ]
        }).ToList();
    }

    private static List<DocumentRevision> ReadRevisions(W.Body body)
    {
        var revisions = new List<DocumentRevision>();
        revisions.AddRange(body.Descendants<W.InsertedRun>().Select(run => CreateRevision(DocumentRevisionType.Insertion, run.Id?.Value, run.Author?.Value, run.Date?.Value)));
        revisions.AddRange(body.Descendants<W.DeletedRun>().Select(run => CreateRevision(DocumentRevisionType.Deletion, run.Id?.Value, run.Author?.Value, run.Date?.Value)));
        revisions.AddRange(body.Descendants<W.RunPropertiesChange>().Select(change => CreateRevision(DocumentRevisionType.Formatting, change.Id?.Value, change.Author?.Value, change.Date?.Value)));
        return revisions;
    }

    private static DocumentRevision CreateRevision(DocumentRevisionType type, string? id, string? author, DateTime? date)
    {
        return new DocumentRevision
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : $"docx-rev-{id}",
            Type = type,
            Author = new DocumentRevisionAuthor { DisplayName = author ?? string.Empty },
            CreatedAt = date is null ? DateTimeOffset.UtcNow : new DateTimeOffset(DateTime.SpecifyKind(date.Value, DateTimeKind.Utc))
        };
    }

    private void ReadSectionProperties(DocumentEditorDocument doc, W.Body body)
    {
        var section = doc.Sections.FirstOrDefault() ?? new DocumentSection { Order = 0 };
        var sectionProperties = body.Elements<W.SectionProperties>().LastOrDefault();
        if (sectionProperties is null)
        {
            return;
        }

        var size = sectionProperties.GetFirstChild<W.PageSize>();
        if (size?.Width is not null && size.Height is not null)
        {
            section.Properties.PageSettings.Size = new DocumentPageSize
            {
                Name = "DOCX",
                Width = TwipsToPoints(size.Width),
                Height = TwipsToPoints(size.Height)
            };
            section.Properties.PageSettings.Landscape = size.Orient?.Value == W.PageOrientationValues.Landscape;
        }

        var margin = sectionProperties.GetFirstChild<W.PageMargin>();
        if (margin is not null)
        {
            section.Properties.PageSettings.Margins = new DocumentPageMargins
            {
                Top = TwipsToPoints(margin.Top?.Value ?? 1440),
                Right = TwipsToPoints(margin.Right?.Value ?? 1440),
                Bottom = TwipsToPoints(margin.Bottom?.Value ?? 1440),
                Left = TwipsToPoints(margin.Left?.Value ?? 1440)
            };
        }
    }

    private static double TwipsToPoints(double twips) => twips / 20d;

    private void PreserveUnsupportedParts()
    {
        var parts = _document.Parts.Select(part => part.OpenXmlPart).ToList();
        for (var i = 0; i < parts.Count; i++)
        {
            parts.AddRange(parts[i].Parts.Select(part => part.OpenXmlPart));
        }

        _preservedParts.AddRange(parts
            .Where(part => !part.Uri.ToString().EndsWith("/document.xml", StringComparison.OrdinalIgnoreCase))
            .Where(part => !part.Uri.ToString().Contains("/media/", StringComparison.OrdinalIgnoreCase))
            .Select(part =>
            {
                using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return new DocumentFormatPreservedPart
                {
                    Path = part.Uri.ToString(),
                    ContentType = part.ContentType,
                    Content = memory.ToArray()
                };
            }));
    }

    private static DocumentFormatCompatibilityWarning Warning(string code, string message, DocumentFormatCompatibilitySeverity severity, string? path = null)
    {
        return new DocumentFormatCompatibilityWarning
        {
            Code = code,
            Message = message,
            Severity = severity,
            SourcePath = path
        };
    }
}
