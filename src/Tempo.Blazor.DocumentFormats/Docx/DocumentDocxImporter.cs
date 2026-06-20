using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Internal;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WordprocessingDocument _document;
    private readonly DocumentFormatImportOptions _options;
    private readonly List<DocumentFormatCompatibilityWarning> _warnings = [];
    private readonly List<DocumentFormatPreservedPart> _preservedParts = [];
    private readonly Dictionary<string, string> _hyperlinks = new(StringComparer.Ordinal);
    private readonly Dictionary<int, DocumentNumberingDefinition> _numberingDefinitionsByInstanceId = new();
    private int _order;
    private int _preservedDrawingIndex;

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
        ReadStyles(doc, main);
        ReadNumberingDefinitions(doc, main);

        foreach (var relationship in main.HyperlinkRelationships)
        {
            _hyperlinks[relationship.Id] = relationship.Uri.ToString();
        }

        var bodyContext = new DocxPartReadContext(main, DocumentRenditionAnchorScope.Body);
        foreach (var element in main.Document.Body.Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element is W.Paragraph paragraph)
            {
                var blocks = await ReadParagraphAsync(paragraph, bodyContext, cancellationToken);
                doc.Blocks.AddRange(blocks);
            }
            else if (element is W.Table table)
            {
                doc.Blocks.Add(await ReadTableAsync(table, bodyContext, cancellationToken));
            }
            else if (element is W.SdtBlock sdtBlock)
            {
                var contentControlBlock = await ReadContentControlBlockAsync(sdtBlock, bodyContext, cancellationToken);
                if (contentControlBlock is not null)
                {
                    doc.Blocks.Add(contentControlBlock);
                    continue;
                }

                var firstBlockIndex = doc.Blocks.Count;
                foreach (var child in sdtBlock.GetFirstChild<W.SdtContentBlock>()?.Elements() ?? Enumerable.Empty<OpenXmlElement>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (child is W.Paragraph sdtParagraph)
                    {
                        doc.Blocks.AddRange(await ReadParagraphAsync(sdtParagraph, bodyContext, cancellationToken));
                    }
                    else if (child is W.Table sdtTable)
                    {
                        doc.Blocks.Add(await ReadTableAsync(sdtTable, bodyContext, cancellationToken));
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

        doc.HeadersFooters.AddRange(await ReadHeadersFootersAsync(main, cancellationToken));
        doc.Notes.AddRange(await ReadNotesAsync(main, cancellationToken));
        doc.Comments.AddRange(await ReadCommentsAsync(main, cancellationToken));
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

    private void ReadStyles(DocumentEditorDocument doc, MainDocumentPart main)
    {
        var styles = main.StyleDefinitionsPart?.Styles?.Elements<W.Style>() ?? [];
        foreach (var style in styles)
        {
            var model = DeserializeTempoJson<DocumentStyleDefinition>(
                GetTempoAttribute(style, "style-json"),
                "word/styles.xml",
                "docx.styleMetadataInvalid");
            if (model is not null && !doc.Styles.Any(existing => string.Equals(existing.Id, model.Id, StringComparison.Ordinal)))
            {
                doc.Styles.Add(model);
            }
        }
    }

    private void ReadNumberingDefinitions(DocumentEditorDocument doc, MainDocumentPart main)
    {
        var numbering = main.NumberingDefinitionsPart?.Numbering;
        if (numbering is null)
        {
            return;
        }

        var listStyles = DeserializeTempoJson<List<DocumentListStyle>>(
            GetTempoAttribute(numbering, "list-styles-json"),
            "word/numbering.xml",
            "docx.listStyleMetadataInvalid");
        if (listStyles is not null)
        {
            foreach (var style in listStyles.Where(style => !doc.ListStyles.Any(existing => existing.Id == style.Id)))
            {
                doc.ListStyles.Add(style);
            }
        }

        var definitionsByAbstractId = new Dictionary<int, DocumentNumberingDefinition>();
        foreach (var abstractNumber in numbering.Elements<W.AbstractNum>())
        {
            var abstractId = abstractNumber.AbstractNumberId?.Value;
            if (!abstractId.HasValue)
            {
                continue;
            }

            var model = DeserializeTempoJson<DocumentNumberingDefinition>(
                GetTempoAttribute(abstractNumber, "numbering-json"),
                "word/numbering.xml",
                "docx.numberingMetadataInvalid")
                ?? ReadNumberingDefinitionFallback(abstractNumber);
            definitionsByAbstractId[abstractId.Value] = model;
        }

        foreach (var instance in numbering.Elements<W.NumberingInstance>())
        {
            var instanceId = instance.NumberID?.Value;
            var abstractId = instance.AbstractNumId?.Val?.Value;
            if (!instanceId.HasValue || !abstractId.HasValue || !definitionsByAbstractId.TryGetValue(abstractId.Value, out var definition))
            {
                continue;
            }

            _numberingDefinitionsByInstanceId[instanceId.Value] = definition;
            if (!doc.NumberingDefinitions.Any(existing => existing.Id == definition.Id))
            {
                doc.NumberingDefinitions.Add(definition);
            }
        }
    }

    private static DocumentNumberingDefinition ReadNumberingDefinitionFallback(W.AbstractNum abstractNumber)
    {
        var abstractId = abstractNumber.AbstractNumberId?.Value.ToString(CultureInfo.InvariantCulture) ?? Guid.NewGuid().ToString("N");
        return new DocumentNumberingDefinition
        {
            Id = $"docx-numbering-{abstractId}",
            AbstractId = $"docx-abstract-{abstractId}",
            Levels = abstractNumber.Elements<W.Level>()
                .Select(level => new DocumentNumberingLevel
                {
                    Level = level.LevelIndex?.Value ?? 0,
                    Format = FromDocxNumberFormat(level.GetFirstChild<W.NumberingFormat>()?.Val?.Value),
                    Text = level.GetFirstChild<W.LevelText>()?.Val?.Value ?? "%1.",
                    StartAt = level.GetFirstChild<W.StartNumberingValue>()?.Val?.Value ?? 1,
                    Suffix = FromDocxLevelSuffix(level.GetFirstChild<W.LevelSuffix>()?.Val?.Value),
                    Indent = TwipsToPointsOrZero(level.GetFirstChild<W.ParagraphProperties>()?.GetFirstChild<W.Indentation>()?.Left?.Value),
                    Hanging = TwipsToPointsOrZero(level.GetFirstChild<W.ParagraphProperties>()?.GetFirstChild<W.Indentation>()?.Hanging?.Value)
                })
                .ToList()
        };
    }

    private static string FromDocxNumberFormat(W.NumberFormatValues? value)
    {
        if (value == W.NumberFormatValues.Bullet)
        {
            return "bullet";
        }

        if (value == W.NumberFormatValues.LowerRoman)
        {
            return "lower-roman";
        }

        if (value == W.NumberFormatValues.UpperRoman)
        {
            return "upper-roman";
        }

        if (value == W.NumberFormatValues.LowerLetter)
        {
            return "lower-letter";
        }

        if (value == W.NumberFormatValues.UpperLetter)
        {
            return "upper-letter";
        }

        if (value == W.NumberFormatValues.None)
        {
            return "none";
        }

        return "decimal";
    }

    private static string FromDocxLevelSuffix(W.LevelSuffixValues? value)
    {
        if (value == W.LevelSuffixValues.Space)
        {
            return "space";
        }

        if (value == W.LevelSuffixValues.Nothing)
        {
            return "none";
        }

        return "tab";
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

    private async Task<DocumentBlock?> ReadContentControlBlockAsync(
        W.SdtBlock sdtBlock,
        DocxPartReadContext context,
        CancellationToken cancellationToken)
    {
        var control = DeserializeTempoJson<DocumentContentControl>(
            GetTempoAttribute(sdtBlock, "content-control-json")
            ?? (sdtBlock.SdtProperties is null ? null : GetTempoAttribute(sdtBlock.SdtProperties, "control-json")),
            GetPartSourcePath(context.OwnerPart),
            "docx.blockContentControlMetadataInvalid");
        if (control is null)
        {
            return null;
        }

        var blocks = new List<DocumentBlock>();
        foreach (var child in sdtBlock.GetFirstChild<W.SdtContentBlock>()?.Elements() ?? Enumerable.Empty<OpenXmlElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (child is W.Paragraph paragraph)
            {
                blocks.AddRange(await ReadParagraphAsync(paragraph, context, cancellationToken));
            }
            else if (child is W.Table table)
            {
                blocks.Add(await ReadTableAsync(table, context, cancellationToken));
            }
            else if (child is W.SdtBlock nestedSdt)
            {
                var nested = await ReadContentControlBlockAsync(nestedSdt, context, cancellationToken);
                if (nested is not null)
                {
                    blocks.Add(nested);
                }
            }
        }

        for (var index = 0; index < blocks.Count; index++)
        {
            blocks[index].Order = index;
        }

        return new DocumentBlock
        {
            Id = ReadElementId(sdtBlock, "block-id"),
            SectionId = GetTempoAttribute(sdtBlock, "section-id"),
            Type = DocumentBlockType.ContentControl,
            Order = _order++,
            Content = new ContentControlBlockContent
            {
                Control = control,
                Blocks = blocks
            }
        };
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

    private async Task<List<DocumentBlock>> ReadParagraphAsync(
        W.Paragraph paragraph,
        DocxPartReadContext context,
        CancellationToken cancellationToken)
    {
        var blocks = new List<DocumentBlock>();
        var pageBreakSeen = false;
        if (paragraph.Descendants<W.Break>().Any(b => b.Type?.Value == W.BreakValues.Page || b.Type?.Value == W.BreakValues.Column))
        {
            pageBreakSeen = true;
        }

        var inlines = await ReadInlinesAsync(paragraph.ChildElements, context, cancellationToken: cancellationToken);
        var drawings = inlines.OfType<DocumentDrawingRun>().ToList();
        if (drawings.Count == 1
            && TryReadImageCaption(paragraph, inlines, out var caption))
        {
            drawings[0].Caption = caption;
            inlines.RemoveAll(inline => inline is TextRun);
        }

        if (TryCreateTempoImageBlock(paragraph, inlines, out var imageBlock))
        {
            blocks.Insert(0, imageBlock);
        }
        else if (inlines.Count > 0 || blocks.Count == 0)
        {
            var blockType = GetParagraphType(paragraph, out var headingLevel, out var ordered, out var indent);
            var content = CreateTextContent(blockType, inlines, headingLevel, ordered, indent);
            if (content is ListBlockContent list)
            {
                ApplyListMetadata(paragraph, list, ordered, indent);
            }

            var block = new DocumentBlock
            {
                Id = ReadElementId(paragraph, "block-id"),
                SectionId = GetTempoAttribute(paragraph, "section-id"),
                Type = blockType,
                Order = _order++,
                Content = content
            };
            NormalizeDrawingAnchors(block, inlines);
            blocks.Insert(0, block);
        }

        if (pageBreakSeen)
        {
            blocks.Add(new DocumentBlock
            {
                Type = DocumentBlockType.PageBreak,
                Order = _order++,
                SectionId = GetTempoAttribute(paragraph, "section-id"),
                Content = new PageBreakBlockContent
                {
                    BreakType = ReadBreakType(paragraph),
                    NextSectionId = GetTempoAttribute(paragraph, "next-section-id")
                }
            });
        }

        return blocks;
    }

    private bool TryCreateTempoImageBlock(W.Paragraph paragraph, IReadOnlyList<InlineContent> inlines, out DocumentBlock block)
    {
        block = new DocumentBlock();
        // First (not Single): a paragraph may legitimately contain several inline
        // drawings (e.g. two images in one sentence). Those are not single-image
        // blocks - HasNonImageBlockInlineContent below rejects them so they stay
        // inline. SingleOrDefault() here threw "Sequence contains more than one
        // element" and aborted the whole import.
        var drawing = inlines.OfType<DocumentDrawingRun>().FirstOrDefault();
        if (drawing is null)
        {
            return false;
        }

        var isTempoImageBlock = string.Equals(GetTempoAttribute(paragraph, "block-type"), "image", StringComparison.OrdinalIgnoreCase)
            || DocumentImagePersistence.IsImageBlockOrigin(drawing);
        if (!isTempoImageBlock || HasNonImageBlockInlineContent(inlines, drawing))
        {
            return false;
        }

        var blockId = ReadElementId(paragraph, "block-id");
        drawing.Layout ??= DocumentObjectLayout.Inline();
        drawing.Layout.Anchor ??= new DocumentObjectAnchor();
        drawing.Layout.Anchor.BlockId ??= blockId;
        block = new DocumentBlock
        {
            Id = blockId,
            SectionId = GetTempoAttribute(paragraph, "section-id"),
            Type = DocumentBlockType.Image,
            Order = _order++,
            Content = DocumentImagePersistence.ToImageBlockContent(drawing)
        };
        return true;
    }

    private static bool HasNonImageBlockInlineContent(IReadOnlyList<InlineContent> inlines, DocumentDrawingRun imageDrawing)
        => inlines.Any(inline => inline switch
        {
            DocumentDrawingRun drawing => !ReferenceEquals(drawing, imageDrawing),
            TextRun text => !string.IsNullOrWhiteSpace(text.Text),
            _ => true
        });

    private static void NormalizeDrawingAnchors(DocumentBlock block, IReadOnlyList<InlineContent> inlines)
    {
        var offset = 0;
        for (var index = 0; index < inlines.Count; index++)
        {
            if (inlines[index] is DocumentDrawingRun drawing)
            {
                drawing.Layout.Anchor.BlockId ??= block.Id;
                drawing.Layout.Anchor.InlineIndex ??= index;
                drawing.Layout.Anchor.Offset ??= offset;
                continue;
            }

            if (inlines[index] is TextRun text)
            {
                offset += text.Text.Length;
            }
        }
    }

    private static bool TryReadImageCaption(W.Paragraph paragraph, IReadOnlyList<InlineContent> inlines, out string caption)
    {
        caption = string.Empty;
        if (inlines.Count(inline => inline is DocumentDrawingRun) != 1)
        {
            return false;
        }

        var textAfterDrawing = new StringBuilder();
        var seenDrawing = false;
        var seenBreakAfterDrawing = false;
        var textBeforeDrawing = new StringBuilder();

        foreach (var child in paragraph.ChildElements)
        {
            if (child.Descendants<W.Drawing>().Any())
            {
                seenDrawing = true;
                continue;
            }

            if (!seenDrawing)
            {
                foreach (var text in child.Descendants<W.Text>())
                {
                    textBeforeDrawing.Append(text.Text);
                }

                continue;
            }

            if (child.Descendants<W.Break>().Any())
            {
                seenBreakAfterDrawing = true;
            }

            if (seenBreakAfterDrawing)
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

        if (!string.IsNullOrWhiteSpace(textBeforeDrawing.ToString()) || !seenBreakAfterDrawing)
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

    private static DocumentBlockContent CreateTextContent(DocumentBlockType blockType, List<InlineContent> inlines, int headingLevel, bool ordered, int indent)
    {
        return blockType switch
        {
            DocumentBlockType.Heading => new HeadingBlockContent { Level = headingLevel <= 0 ? 1 : headingLevel, Inlines = inlines },
            DocumentBlockType.List => new ListBlockContent { Ordered = ordered, IndentLevel = indent, Inlines = inlines },
            _ => new ParagraphBlockContent { Inlines = inlines }
        };
    }

    private void ApplyListMetadata(W.Paragraph paragraph, ListBlockContent list, bool ordered, int indent)
    {
        var numId = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
        if (numId.HasValue && _numberingDefinitionsByInstanceId.TryGetValue(numId.Value, out var definition))
        {
            list.NumberingId = definition.Id;
            list.AbstractNumberingId = definition.AbstractId;
            var level = definition.Levels.FirstOrDefault(item => item.Level == indent);
            if (level is not null)
            {
                list.NumberFormat = level.Format;
                list.LevelText = level.Text;
                list.Suffix = level.Suffix;
                list.LabelIndent = level.Indent;
                list.HangingIndent = level.Hanging;
                list.Ordered = !string.Equals(level.Format, "bullet", StringComparison.OrdinalIgnoreCase);
            }
        }

        list.Ordered = ParseBool(GetTempoAttribute(paragraph, "list-ordered"), list.Ordered || ordered);
        list.IndentLevel = ParseInt(GetTempoAttribute(paragraph, "list-level"), indent);
        list.StartNumber = ParseInt(GetTempoAttribute(paragraph, "list-start-number"), list.StartNumber);
        list.NumberingId = GetTempoAttribute(paragraph, "numbering-id") ?? list.NumberingId;
        list.AbstractNumberingId = GetTempoAttribute(paragraph, "abstract-numbering-id") ?? list.AbstractNumberingId;
        list.ListStyleId = GetTempoAttribute(paragraph, "list-style-id") ?? list.ListStyleId;
        list.NumberFormat = GetTempoAttribute(paragraph, "number-format") ?? list.NumberFormat;
        list.LevelText = GetTempoAttribute(paragraph, "level-text") ?? list.LevelText;
        list.Suffix = GetTempoAttribute(paragraph, "list-suffix") ?? list.Suffix;
        list.LabelIndent = ParseNullableDouble(GetTempoAttribute(paragraph, "label-indent")) ?? list.LabelIndent;
        list.HangingIndent = ParseNullableDouble(GetTempoAttribute(paragraph, "hanging-indent")) ?? list.HangingIndent;
        list.RestartNumbering = ParseBool(GetTempoAttribute(paragraph, "restart-numbering"), list.RestartNumbering);
        list.ContinueNumbering = ParseBool(GetTempoAttribute(paragraph, "continue-numbering"), list.ContinueNumbering);
        list.NumberingValue = ParseNullableInt(GetTempoAttribute(paragraph, "numbering-value")) ?? list.NumberingValue;
    }

    private static DocumentSectionBreakType ReadBreakType(W.Paragraph paragraph)
    {
        if (Enum.TryParse<DocumentSectionBreakType>(GetTempoAttribute(paragraph, "break-type"), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return paragraph.Descendants<W.Break>().Any(b => b.Type?.Value == W.BreakValues.Column)
            ? DocumentSectionBreakType.Column
            : DocumentSectionBreakType.Page;
    }

    private async Task<List<InlineContent>> ReadInlinesAsync(
        IEnumerable<OpenXmlElement> elements,
        DocxPartReadContext context,
        List<InlineMark>? inheritedMarks = null,
        CancellationToken cancellationToken = default)
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
                await ReadRunContentAsync(run, context, marks, result, cancellationToken);
            }
            else if (element is W.Hyperlink hyperlink)
            {
                var href = hyperlink.Id is not null && TryGetHyperlink(context.OwnerPart, hyperlink.Id!, out var link)
                    ? link
                    : hyperlink.Anchor?.Value ?? string.Empty;
                var linkMarks = MergeMarks(currentInherited, [new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = href } }]);
                result.AddRange(await ReadInlinesAsync(hyperlink.ChildElements, context, linkMarks, cancellationToken));
            }
            else if (element is W.SimpleField simpleField)
            {
                result.Add(await ReadFieldInlineAsync(simpleField, context, currentInherited, cancellationToken));
            }
            else if (element is W.SdtRun sdtRun)
            {
                result.Add(await ReadContentControlInlineAsync(sdtRun, context, currentInherited, cancellationToken));
            }
            else if (element is W.InsertedRun inserted)
            {
                var revisionId = $"docx-rev-{inserted.Id?.Value ?? Guid.NewGuid().ToString("N")}";
                var marks = MergeMarks(currentInherited, [new InlineMark { Type = InlineMarkType.Revision, RevisionId = revisionId }]);
                result.AddRange(await ReadInlinesAsync(inserted.ChildElements, context, marks, cancellationToken));
            }
            else if (element is W.DeletedRun deleted)
            {
                var revisionId = $"docx-rev-{deleted.Id?.Value ?? Guid.NewGuid().ToString("N")}";
                var marks = MergeMarks(currentInherited, [new InlineMark { Type = InlineMarkType.Revision, RevisionId = revisionId }]);
                result.AddRange(await ReadInlinesAsync(deleted.ChildElements, context, marks, cancellationToken));
            }
        }

        return result;
    }

    private async Task ReadRunContentAsync(
        W.Run run,
        DocxPartReadContext context,
        List<InlineMark> marks,
        List<InlineContent> result,
        CancellationToken cancellationToken)
    {
        if (string.Equals(GetTempoAttribute(run, "inline-kind"), "math", StringComparison.Ordinal)
            && DeserializeTempoJson<DocumentMathRun>(
                GetTempoAttribute(run, "math-json"),
                GetPartSourcePath(context.OwnerPart),
                "docx.mathMetadataInvalid") is { } math)
        {
            if (math.Marks.Count == 0)
            {
                math.Marks = marks.Select(CloneMark).ToList();
            }

            result.Add(math);
            return;
        }

        foreach (var child in run.ChildElements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (child is W.Text text && !string.IsNullOrEmpty(text.Text))
            {
                result.Add(new TextRun { Text = text.Text, Marks = marks.Select(CloneMark).ToList() });
            }
            else if (child is W.Drawing drawing)
            {
                var drawingRun = await ReadDrawingRunAsync(drawing, context, marks, cancellationToken);
                if (drawingRun is not null)
                {
                    result.Add(drawingRun);
                }
            }
            else if (child is W.FootnoteReference footnote)
            {
                result.Add(new DocumentNoteReferenceRun { NoteId = footnote.Id?.ToString() ?? string.Empty, NoteType = DocumentNoteType.Footnote });
            }
            else if (child is W.EndnoteReference endnote)
            {
                result.Add(new DocumentNoteReferenceRun { NoteId = endnote.Id?.ToString() ?? string.Empty, NoteType = DocumentNoteType.Endnote });
            }
            else if (child is W.TabChar)
            {
                result.Add(new TextRun { Text = "\t", Marks = marks.Select(CloneMark).ToList() });
            }
        }
    }

    private async Task<DocumentFieldRun> ReadFieldInlineAsync(
        W.SimpleField simpleField,
        DocxPartReadContext context,
        IReadOnlyList<InlineMark> inheritedMarks,
        CancellationToken cancellationToken)
    {
        var field = DeserializeTempoJson<DocumentFieldRun>(
            GetTempoAttribute(simpleField, "field-json"),
            GetPartSourcePath(context.OwnerPart),
            "docx.fieldMetadataInvalid");
        if (field is not null)
        {
            if (field.Marks.Count == 0)
            {
                field.Marks = inheritedMarks.Select(CloneMark).ToList();
            }

            return field;
        }

        var inlines = await ReadInlinesAsync(simpleField.ChildElements, context, inheritedMarks.ToList(), cancellationToken);
        var instruction = simpleField.Instruction?.Value ?? string.Empty;
        return new DocumentFieldRun
        {
            Id = GetTempoAttribute(simpleField, "inline-id"),
            FieldType = ParseFieldType(instruction),
            InstrText = instruction,
            CachedResult = GetInlineText(inlines),
            DisplayText = GetInlineText(inlines),
            Marks = inheritedMarks.Select(CloneMark).ToList()
        };
    }

    private async Task<DocumentContentControlRun> ReadContentControlInlineAsync(
        W.SdtRun sdtRun,
        DocxPartReadContext context,
        IReadOnlyList<InlineMark> inheritedMarks,
        CancellationToken cancellationToken)
    {
        var model = DeserializeTempoJson<DocumentContentControlRun>(
            GetTempoAttribute(sdtRun, "content-control-json"),
            GetPartSourcePath(context.OwnerPart),
            "docx.contentControlMetadataInvalid");
        if (model is not null)
        {
            if (model.Marks.Count == 0)
            {
                model.Marks = inheritedMarks.Select(CloneMark).ToList();
            }

            return model;
        }

        var content = sdtRun.GetFirstChild<W.SdtContentRun>();
        var inlines = await ReadInlinesAsync(content?.ChildElements ?? [], context, inheritedMarks.ToList(), cancellationToken);
        return new DocumentContentControlRun
        {
            Id = GetTempoAttribute(sdtRun, "inline-id"),
            Control = ReadContentControlProperties(sdtRun.SdtProperties),
            Inlines = inlines,
            Marks = inheritedMarks.Select(CloneMark).ToList()
        };
    }

    private DocumentContentControl ReadContentControlProperties(W.SdtProperties? properties)
    {
        var model = DeserializeTempoJson<DocumentContentControl>(
            properties is null ? null : GetTempoAttribute(properties, "control-json"),
            "word/document.xml",
            "docx.contentControlPropertiesInvalid");
        if (model is not null)
        {
            return model;
        }

        return new DocumentContentControl
        {
            Alias = properties?.GetFirstChild<W.SdtAlias>()?.Val?.Value,
            Tag = properties?.GetFirstChild<W.Tag>()?.Val?.Value,
            LockContent = properties?.GetFirstChild<W.Lock>()?.Val?.Value == W.LockingValues.ContentLocked
                || properties?.GetFirstChild<W.Lock>()?.Val?.Value == W.LockingValues.SdtLocked
        };
    }

    private static DocumentFieldType ParseFieldType(string instruction)
    {
        var normalized = instruction.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() ?? string.Empty;
        return normalized switch
        {
            "PAGE" => DocumentFieldType.PageNumber,
            "NUMPAGES" => DocumentFieldType.PageCount,
            "DATE" => DocumentFieldType.Date,
            "TIME" => DocumentFieldType.Time,
            "TITLE" => DocumentFieldType.DocumentTitle,
            "AUTHOR" => DocumentFieldType.Author,
            "SAVEDATE" => DocumentFieldType.LastSaved,
            "FILENAME" => DocumentFieldType.FileName,
            "REVNUM" => DocumentFieldType.RevisionNumber,
            "STYLEREF" => DocumentFieldType.StyleRef,
            "REF" => DocumentFieldType.Ref,
            "SEQ" => DocumentFieldType.Seq,
            "TOC" => DocumentFieldType.TableOfFigures,
            "BIBLIOGRAPHY" => DocumentFieldType.Bibliography,
            "CITATION" => DocumentFieldType.Citation,
            "SECTIONPAGE" => DocumentFieldType.SectionPageNumber,
            "SECTIONPAGES" => DocumentFieldType.SectionPageCount,
            _ => DocumentFieldType.Unknown
        };
    }

    private async Task<DocumentDrawingRun?> ReadDrawingRunAsync(
        W.Drawing drawing,
        DocxPartReadContext context,
        IReadOnlyList<InlineMark> marks,
        CancellationToken cancellationToken)
    {
        if (!ValidateDrawingForImageImport(drawing, context.OwnerPart))
        {
            return null;
        }

        var drawingRun = await ReadDrawingRunImageAsync(drawing, context.OwnerPart, cancellationToken);
        if (drawingRun is null)
        {
            return null;
        }

        var link = marks.FirstOrDefault(mark => mark.Type == InlineMarkType.Link && mark.Link is not null)?.Link;
        if (link is not null)
        {
            drawingRun.LinkUrl = link.Href;
        }

        drawingRun.Marks = marks
            .Where(mark => mark.Type != InlineMarkType.Link)
            .Select(CloneMark)
            .ToList();
        ApplyImportContext(drawingRun.Layout.Anchor, context);
        ApplyImportedDrawingIdentity(drawingRun, drawing, drawingRun.Docx!, context);
        ApplyImportedImageBlockOrigin(drawingRun, drawing);
        return drawingRun;
    }

    private static void ApplyImportedDrawingIdentity(
        DocumentDrawingRun drawingRun,
        W.Drawing drawing,
        DocumentDocxDrawingMetadata metadata,
        DocxPartReadContext context)
    {
        var host = GetDrawingHost(drawing);
        var objectId = host is null ? null : GetTempoAttribute(host, "object-id");
        objectId ??= CreateStableImportedDrawingObjectId(metadata, context);
        if (!string.IsNullOrWhiteSpace(objectId))
        {
            drawingRun.ObjectId = objectId;
        }

        var runId = host is null ? null : GetTempoAttribute(host, "run-id");
        drawingRun.Id = FirstNonWhiteSpace(runId, drawingRun.Id, $"{drawingRun.ObjectId}-run");
    }

    private static void ApplyImportedImageBlockOrigin(DocumentDrawingRun drawingRun, W.Drawing drawing)
    {
        var host = GetDrawingHost(drawing);
        if (host is null || !ParseBool(GetTempoAttribute(host, "image-block-origin"), false))
        {
            return;
        }

        DocumentImagePersistence.MarkImageBlockOrigin(
            drawingRun,
            GetTempoAttribute(host, "image-block-id")
            ?? drawingRun.Layout?.Anchor?.BlockId
            ?? drawingRun.ObjectId);
    }

    private static OpenXmlElement? GetDrawingHost(W.Drawing drawing)
        => (OpenXmlElement?)drawing.Descendants<DW.Inline>().FirstOrDefault()
            ?? drawing.Descendants<DW.Anchor>().FirstOrDefault();

    private static string CreateStableImportedDrawingObjectId(
        DocumentDocxDrawingMetadata metadata,
        DocxPartReadContext context)
    {
        var label = FirstNonWhiteSpace(
            metadata.DocPrName,
            metadata.PictureName,
            metadata.DocPrDescription,
            metadata.PictureDescription,
            metadata.Media?.OriginalFileName,
            metadata.Media?.ImagePartUri,
            "drawing")!;
        var source = string.Join("|",
        [
            context.Region.ToString(),
            context.HeaderFooterId ?? string.Empty,
            context.TableId ?? string.Empty,
            context.CellId ?? string.Empty,
            metadata.DocPrId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            metadata.DocPrName ?? string.Empty,
            metadata.DocPrDescription ?? string.Empty,
            metadata.PictureNonVisualId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            metadata.PictureName ?? string.Empty,
            metadata.PictureDescription ?? string.Empty,
            metadata.Media?.SourcePartUri ?? string.Empty,
            metadata.Media?.ImagePartUri ?? string.Empty
        ]);

        return $"docx-{SlugForId(label)}-{StableHash(source)}";
    }

    private static string SlugForId(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }

            if (builder.Length >= 36)
            {
                break;
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "drawing" : slug;
    }

    private static string StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }
    }

    private static string? CreateDrawingWarningObjectId(W.Drawing drawing, OpenXmlPartContainer ownerPart)
    {
        var host = GetDrawingHost(drawing);
        var objectId = host is null ? null : GetTempoAttribute(host, "object-id");
        if (!string.IsNullOrWhiteSpace(objectId))
        {
            return objectId;
        }

        var docPr = drawing.Descendants<DW.DocProperties>().FirstOrDefault();
        var pictureProperties = drawing.Descendants<PIC.NonVisualDrawingProperties>().FirstOrDefault();
        var label = FirstNonWhiteSpace(
            docPr?.Name?.Value,
            pictureProperties?.Name?.Value,
            docPr?.Description?.Value,
            pictureProperties?.Description?.Value,
            "drawing")!;
        var source = string.Join("|",
        [
            GetPartSourcePath(ownerPart),
            ToInvariantString(docPr?.Id),
            docPr?.Name?.Value ?? string.Empty,
            docPr?.Description?.Value ?? string.Empty,
            ToInvariantString(pictureProperties?.Id),
            pictureProperties?.Name?.Value ?? string.Empty,
            pictureProperties?.Description?.Value ?? string.Empty
        ]);

        return $"docx-{SlugForId(label)}-{StableHash(source)}";
    }

    private static string ToInvariantString(UInt32Value? value)
        => value is null ? string.Empty : value.Value.ToString(CultureInfo.InvariantCulture);

    private static bool IsSafePackagePartPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("/", StringComparison.Ordinal)
            && !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment =>
                segment.Equals("..", StringComparison.Ordinal)
                || segment.Contains(':', StringComparison.Ordinal));
    }

    private bool ValidateDrawingForImageImport(W.Drawing drawing, OpenXmlPartContainer ownerPart)
    {
        var sourcePath = GetPartSourcePath(ownerPart);
        var host = (OpenXmlElement?)drawing.Descendants<DW.Inline>().FirstOrDefault()
            ?? drawing.Descendants<DW.Anchor>().FirstOrDefault();
        if (host is null)
        {
            _warnings.Add(Warning(
                "docx.drawingHostMissing",
                "DOCX drawing is missing wp:inline/wp:anchor and cannot be imported as an editor image.",
                DocumentFormatCompatibilitySeverity.Dropped,
                sourcePath));
            PreserveUnsupportedDrawingXml(drawing, sourcePath, "host-missing");
            return false;
        }

        var graphicData = drawing.Descendants<A.GraphicData>().FirstOrDefault();
        if (graphicData is null)
        {
            _warnings.Add(Warning(
                "docx.drawingGraphicDataMissing",
                "DOCX drawing is missing a:graphicData and cannot be imported as an editor image.",
                DocumentFormatCompatibilitySeverity.Dropped,
                sourcePath));
            PreserveUnsupportedDrawingXml(drawing, sourcePath, "graphicData-missing");
            return false;
        }

        var hasPicture = graphicData.Descendants<PIC.Picture>().Any();
        var uri = graphicData.Uri?.Value ?? string.Empty;
        if (!hasPicture)
        {
            var (code, label) = ClassifyUnsupportedGraphicData(uri, graphicData);
            _warnings.Add(Warning(
                code,
                $"DOCX drawing graphicData '{label}' is not an image picture and was preserved as unsupported DrawingML metadata.",
                DocumentFormatCompatibilitySeverity.Dropped,
                sourcePath));
            PreserveUnsupportedDrawingXml(drawing, sourcePath, label);
            return false;
        }

        if (host.GetFirstChild<DW.Extent>() is null)
        {
            _warnings.Add(Warning(
                "docx.drawingExtentMissing",
                "DOCX image drawing is missing wp:extent; the importer used a default image size.",
                DocumentFormatCompatibilitySeverity.Warning,
                sourcePath));
        }

        if (host.GetFirstChild<DW.DocProperties>() is null)
        {
            _warnings.Add(Warning(
                "docx.drawingDocPrMissing",
                "DOCX image drawing is missing wp:docPr metadata.",
                DocumentFormatCompatibilitySeverity.Warning,
                sourcePath));
        }

        return true;
    }

    private static (string Code, string Label) ClassifyUnsupportedGraphicData(string uri, A.GraphicData graphicData)
    {
        if (uri.Contains("chart", StringComparison.OrdinalIgnoreCase)
            || graphicData.Descendants().Any(element => element.LocalName.Equals("chart", StringComparison.OrdinalIgnoreCase)))
        {
            return ("docx.drawingChartUnsupported", "chart");
        }

        if (uri.Contains("diagram", StringComparison.OrdinalIgnoreCase)
            || graphicData.Descendants().Any(element => element.LocalName.Equals("relIds", StringComparison.OrdinalIgnoreCase)))
        {
            return ("docx.drawingSmartArtUnsupported", "smartArt");
        }

        if (uri.Contains("wordprocessingCanvas", StringComparison.OrdinalIgnoreCase)
            || graphicData.Descendants().Any(element =>
                element.LocalName.Equals("wpc", StringComparison.OrdinalIgnoreCase)
                || element.LocalName.Equals("grpSp", StringComparison.OrdinalIgnoreCase)))
        {
            return ("docx.drawingCanvasGroupUnsupported", "canvas-group");
        }

        return ("docx.drawingUnsupportedGraphicData", string.IsNullOrWhiteSpace(uri) ? "unknown" : uri);
    }

    private void PreserveUnsupportedDrawingXml(W.Drawing drawing, string sourcePath, string reason)
    {
        var rawXml = ReadRawDrawingXml(drawing, sourcePath, warningCode: "docx.rawDrawingXmlTooLarge");
        if (string.IsNullOrWhiteSpace(rawXml))
        {
            return;
        }

        _preservedDrawingIndex++;
        _preservedParts.Add(new DocumentFormatPreservedPart
        {
            Path = $"{sourcePath}#drawing/{_preservedDrawingIndex}-{reason}.xml",
            ContentType = "application/vnd.openxmlformats-officedocument.drawingml+xml",
            Content = Encoding.UTF8.GetBytes(rawXml)
        });
    }

    private static void ApplyImportContext(DocumentObjectAnchor anchor, DocxPartReadContext context)
    {
        anchor.Region = context.Region;
        anchor.HeaderFooterId = context.HeaderFooterId ?? anchor.HeaderFooterId;
        anchor.TableId = context.TableId ?? anchor.TableId;
        anchor.CellId = context.CellId ?? anchor.CellId;
    }

    private bool TryGetHyperlink(OpenXmlPartContainer ownerPart, string relationshipId, out string href)
    {
        var relationship = ownerPart.HyperlinkRelationships.FirstOrDefault(item => item.Id == relationshipId);
        if (relationship is not null)
        {
            href = relationship.Uri.ToString();
            return true;
        }

        return _hyperlinks.TryGetValue(relationshipId, out href!);
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

    private async Task<DocumentBlock> ReadTableAsync(
        W.Table table,
        DocxPartReadContext context,
        CancellationToken cancellationToken)
    {
        var tableId = ReadElementId(table, "block-id");
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
                var cellId = ReadElementId(cell, "cell-id");
                var cellContext = context.ForTableCell(tableId, cellId);
                var properties = cell.TableCellProperties;
                var columnSpan = Math.Max(1, properties?.GridSpan?.Val?.Value ?? 1);
                var verticalMerge = properties?.VerticalMerge;
                var rowSpan = verticalMerge?.Val?.Value == W.MergedCellValues.Restart ? 2 : 1;
                var blocks = new List<DocumentBlock>();
                foreach (var paragraph in cell.Elements<W.Paragraph>())
                {
                    var inlines = await ReadInlinesAsync(paragraph.ChildElements, cellContext, cancellationToken: cancellationToken);
                    var block = new DocumentBlock
                    {
                        Id = ReadElementId(paragraph, "block-id"),
                        Type = DocumentBlockType.Paragraph,
                        Order = 0,
                        Content = new ParagraphBlockContent { Inlines = inlines }
                    };
                    NormalizeDrawingAnchors(block, inlines);
                    blocks.Add(block);
                }

                cells.Add(new TableCellContent
                {
                    Id = cellId,
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
            Id = tableId,
            SectionId = GetTempoAttribute(table, "section-id"),
            Type = DocumentBlockType.Table,
            Order = _order++,
            Content = new TableBlockContent { Layout = tableLayout, Rows = rows }
        };
    }

    private static double? TwipsToNullablePoints(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var twips) && twips > 0
            ? Math.Round(DocxUnitConverter.TwipToPoint(twips), 2)
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

    private async Task<DocumentDrawingRun?> ReadDrawingRunImageAsync(W.Drawing drawing, OpenXmlPartContainer ownerPart, CancellationToken cancellationToken)
    {
        var sourcePath = GetPartSourcePath(ownerPart);
        var warningObjectId = CreateDrawingWarningObjectId(drawing, ownerPart);
        var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        if (blip is null)
        {
            _warnings.Add(Warning(
                "docx.imageBlipMissing",
                "DOCX picture is missing a:blip and cannot be imported as an embedded image.",
                DocumentFormatCompatibilitySeverity.Dropped,
                sourcePath,
                warningObjectId));
            return null;
        }

        var relationshipId = blip?.Embed?.Value;
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            if (!string.IsNullOrWhiteSpace(blip?.Link?.Value))
            {
                _warnings.Add(Warning(
                    "docx.imageExternalReference",
                    "DOCX image uses an external relationship, which is not imported as embedded image data.",
                    DocumentFormatCompatibilitySeverity.Dropped,
                    sourcePath,
                    warningObjectId));
            }
            else
            {
                _warnings.Add(Warning(
                    "docx.imageRelationshipMissing",
                    "DOCX picture blip does not contain an embedded image relationship.",
                    DocumentFormatCompatibilitySeverity.Dropped,
                    sourcePath,
                    warningObjectId));
            }

            return null;
        }

        if (!ownerPart.TryGetPartById(relationshipId, out var part) || part is not ImagePart imagePart)
        {
            _warnings.Add(Warning(
                "docx.imageMissingPart",
                $"DOCX image relationship '{relationshipId}' does not resolve to a readable image part.",
                DocumentFormatCompatibilitySeverity.Dropped,
                sourcePath,
                warningObjectId));
            return null;
        }

        var imagePartPath = imagePart.Uri.ToString();
        if (!IsSafePackagePartPath(imagePartPath))
        {
            _warnings.Add(Warning(
                "docx.imageUnsafePartPath",
                $"DOCX image part '{imagePartPath}' has an unsafe package path and was not imported.",
                DocumentFormatCompatibilitySeverity.Dropped,
                imagePartPath,
                warningObjectId));
            return null;
        }

        using var memory = new MemoryStream();
        try
        {
            await using var stream = imagePart.GetStream(FileMode.Open, FileAccess.Read);
            if (!await CopyImagePartWithinLimitAsync(stream, memory, imagePartPath, warningObjectId, cancellationToken))
            {
                return null;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OpenXmlPackageException)
        {
            _warnings.Add(Warning(
                "docx.imageUnreadablePart",
                $"DOCX image part '{imagePart.Uri}' could not be read.",
                DocumentFormatCompatibilitySeverity.Dropped,
                imagePartPath,
                warningObjectId));
            return null;
        }

        var bytes = memory.ToArray();
        var fileName = Path.GetFileName(imagePart.Uri.ToString());
        if (DocxImageContentTypeMapper.HasContentTypeSignatureMismatch(imagePart.ContentType, bytes, out var detectedContentType))
        {
            _warnings.Add(Warning(
                "docx.imageContentTypeMismatch",
                $"DOCX image part '{imagePart.Uri}' declares content type '{imagePart.ContentType}' but its byte signature is '{detectedContentType}'.",
                DocumentFormatCompatibilitySeverity.Dropped,
                imagePartPath,
                warningObjectId));
            return null;
        }

        if (!DocxImageContentTypeMapper.TryResolve(imagePart.ContentType, fileName, bytes, out var partInfo))
        {
            _warnings.Add(Warning(
                "docx.imageUnsupportedContentType",
                $"DOCX image part '{imagePart.Uri}' has unsupported content type '{imagePart.ContentType}'.",
                DocumentFormatCompatibilitySeverity.Dropped,
                imagePartPath,
                warningObjectId));
            return null;
        }

        var assetId = relationshipId;
        string? url = null;

        if (_options.ImageImporter is not null)
        {
            var imported = await _options.ImageImporter(new DocumentFormatImageImportRequest
            {
                SourcePath = imagePart.Uri.ToString(),
                ContentType = partInfo.ContentType,
                Content = bytes,
                FileName = fileName
            }, cancellationToken);
            assetId = imported.AssetId ?? assetId;
            url = imported.Url;
        }
        else
        {
            url = $"data:{partInfo.ContentType};base64,{Convert.ToBase64String(bytes)}";
        }

        var metadata = CreateDrawingMetadata(drawing, ownerPart, imagePart, relationshipId, partInfo, fileName);
        var layout = ReadObjectLayout(drawing, metadata);
        var extent = drawing.Descendants<DW.Extent>().FirstOrDefault();
        var size = new DocumentImageSize
        {
            Width = extent?.Cx is null ? 120 : Math.Round(DocxUnitConverter.EmuToPoint(extent.Cx.Value), 2),
            Height = extent?.Cy is null ? 90 : Math.Round(DocxUnitConverter.EmuToPoint(extent.Cy.Value), 2)
        };
        layout.Transform.Width = size.Width;
        layout.Transform.Height = size.Height;
        var altText = FirstNonWhiteSpace(metadata.PictureDescription, metadata.DocPrDescription);

        return new DocumentDrawingRun
        {
            Source = url is not null ? DocumentImageSource.Url : DocumentImageSource.Asset,
            Url = url,
            AssetId = url is null ? assetId : null,
            AltText = altText,
            Size = size,
            Layout = layout,
            Docx = metadata
        };
    }

    private string? ReadRawDrawingXml(W.Drawing drawing, string sourcePath, string warningCode)
    {
        var rawXml = drawing.OuterXml;
        if (string.IsNullOrWhiteSpace(rawXml))
        {
            return null;
        }

        var limit = Math.Max(1, _options.MaxRawDrawingXmlChars);
        if (rawXml.Length > limit)
        {
            _warnings.Add(Warning(
                warningCode,
                $"DOCX raw DrawingML payload in '{sourcePath}' is larger than the configured import limit of {limit} characters.",
                DocumentFormatCompatibilitySeverity.Warning,
                sourcePath));
            return null;
        }

        return rawXml;
    }

    private async Task<bool> CopyImagePartWithinLimitAsync(
        Stream source,
        MemoryStream destination,
        string sourcePath,
        string? objectId,
        CancellationToken cancellationToken)
    {
        var maxBytes = Math.Max(1L, _options.MaxImagePartBytes);
        if (source.CanSeek && source.Length > maxBytes)
        {
            _warnings.Add(Warning(
                "docx.imagePartTooLarge",
                $"DOCX image part '{sourcePath}' is larger than the configured import limit of {maxBytes} bytes.",
                DocumentFormatCompatibilitySeverity.Dropped,
                sourcePath,
                objectId));
            return false;
        }

        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return true;
            }

            total += read;
            if (total > maxBytes)
            {
                _warnings.Add(Warning(
                    "docx.imagePartTooLarge",
                    $"DOCX image part '{sourcePath}' is larger than the configured import limit of {maxBytes} bytes.",
                    DocumentFormatCompatibilitySeverity.Dropped,
                    sourcePath,
                    objectId));
                return false;
            }

            destination.Write(buffer, 0, read);
        }
    }

    private DocumentDocxDrawingMetadata CreateDrawingMetadata(
        W.Drawing drawing,
        OpenXmlPartContainer ownerPart,
        ImagePart imagePart,
        string relationshipId,
        DocxImagePartInfo partInfo,
        string? fileName)
    {
        var docPr = drawing.Descendants<DW.DocProperties>().FirstOrDefault();
        var pictureProperties = drawing.Descendants<PIC.NonVisualDrawingProperties>().FirstOrDefault();
        var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        var blipFill = drawing.Descendants<PIC.BlipFill>().FirstOrDefault();
        var shapeProperties = drawing.Descendants<PIC.ShapeProperties>().FirstOrDefault();
        var anchor = drawing.Descendants<DW.Anchor>().FirstOrDefault();
        var simplePosition = anchor?.GetFirstChild<DW.SimplePosition>();
        var fillMode = ReadBlipFillMode(blipFill);
        var presetGeometry = ReadPresetGeometry(drawing, shapeProperties);
        var unsupportedGeometry = !IsRectPreset(presetGeometry);
        var sourcePath = GetPartSourcePath(ownerPart);
        var hasUnsupportedEffects = HasUnsupportedPictureEffects(shapeProperties);
        if (hasUnsupportedEffects)
        {
            _warnings.Add(Warning(
                "docx.drawingUnsupportedEffectPreserved",
                "DOCX picture contains DrawingML effects that are not editable in the editor; raw DrawingML was preserved for export fallback.",
                DocumentFormatCompatibilitySeverity.Warning,
                sourcePath));
        }

        return new DocumentDocxDrawingMetadata
        {
            DocPrId = docPr?.Id?.Value,
            DocPrName = docPr?.Name?.Value,
            DocPrTitle = docPr?.Title?.Value,
            DocPrDescription = docPr?.Description?.Value,
            PictureNonVisualId = pictureProperties?.Id?.Value,
            PictureName = pictureProperties?.Name?.Value,
            PictureDescription = pictureProperties?.Description?.Value,
            RelationshipId = relationshipId,
            ImageReferenceMode = DocumentDocxImageReferenceMode.Embedded,
            BlipCompressionState = blip?.CompressionState?.Value.ToString(),
            BlipFillMode = fillMode,
            RawBlipFillXml = fillMode == DocumentDocxBlipFillMode.Tile || fillMode == DocumentDocxBlipFillMode.Unknown
                ? blipFill?.OuterXml
                : null,
            PresetGeometry = presetGeometry,
            RawShapePropertiesXml = unsupportedGeometry ? shapeProperties?.OuterXml : null,
            RawDrawingXml = hasUnsupportedEffects ? ReadRawDrawingXml(drawing, sourcePath, "docx.rawDrawingXmlTooLarge") : null,
            Media = new DocumentImageMediaInfo
            {
                SourcePartUri = ownerPart is OpenXmlPart sourcePart ? sourcePart.Uri.ToString() : null,
                ImagePartUri = imagePart.Uri.ToString(),
                ContentType = partInfo.ContentType,
                OriginalFileName = fileName,
                Extension = partInfo.Extension
            },
            EffectExtent = ReadEffectExtent(drawing),
            LayoutInCell = anchor?.LayoutInCell?.Value,
            Hidden = anchor?.Hidden?.Value,
            UsesSimplePosition = anchor?.SimplePos?.Value,
            SimplePosition = simplePosition is null
                ? null
                : new DocumentObjectPoint
                {
                    X = simplePosition.X?.Value ?? 0L,
                    Y = simplePosition.Y?.Value ?? 0L
                },
            AnchorId = anchor?.AnchorId?.Value,
            EditId = anchor?.EditId?.Value
        };
    }

    private static bool HasUnsupportedPictureEffects(PIC.ShapeProperties? shapeProperties)
    {
        if (shapeProperties is null)
        {
            return false;
        }

        return shapeProperties.Descendants<A.EffectList>().Any(effectList => effectList.ChildElements.Count > 0)
            || shapeProperties.Descendants<A.EffectDag>().Any()
            || shapeProperties.Descendants().Any(element => element.LocalName is "outerShdw" or "innerShdw" or "glow" or "softEdge" or "reflection" or "scene3d" or "sp3d");
    }

    private DocumentDocxBlipFillMode ReadBlipFillMode(PIC.BlipFill? blipFill)
    {
        if (blipFill is null)
        {
            _warnings.Add(Warning(
                "docx.drawingBlipFillMissing",
                "DOCX picture is missing pic:blipFill; stretch/fillRect fallback metadata was used.",
                DocumentFormatCompatibilitySeverity.Warning,
                "word/document.xml"));
            return DocumentDocxBlipFillMode.Unknown;
        }

        if (blipFill.GetFirstChild<A.Tile>() is not null)
        {
            _warnings.Add(Warning(
                "docx.drawingBlipFillTileUnsupported",
                "DOCX picture uses DrawingML tile fill, which is preserved in metadata but rendered as stretch/fillRect in the editor.",
                DocumentFormatCompatibilitySeverity.Warning,
                "word/document.xml"));
            return DocumentDocxBlipFillMode.Tile;
        }

        if (blipFill.GetFirstChild<A.Stretch>() is not null)
        {
            return DocumentDocxBlipFillMode.Stretch;
        }

        _warnings.Add(Warning(
            "docx.drawingBlipFillUnsupported",
            "DOCX picture uses an unsupported blip fill mode, which is preserved in metadata but rendered as stretch/fillRect in the editor.",
            DocumentFormatCompatibilitySeverity.Warning,
            "word/document.xml"));
        return DocumentDocxBlipFillMode.Unknown;
    }

    private string? ReadPresetGeometry(W.Drawing drawing, PIC.ShapeProperties? shapeProperties)
    {
        var presetGeometry = (OpenXmlElement?)shapeProperties?.GetFirstChild<A.PresetGeometry>()
            ?? shapeProperties?.ChildElements.FirstOrDefault(element => element.LocalName == "prstGeom")
            ?? drawing.Descendants().FirstOrDefault(element => element.LocalName == "prstGeom");
        var value = (presetGeometry as A.PresetGeometry)?.Preset?.Value.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = presetGeometry?.GetAttribute("prst", string.Empty).Value;
        }

        var rawValue = ReadPresetGeometryFromRawXml(drawing.OuterXml);
        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            value = rawValue;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!IsRectPreset(value))
        {
            _warnings.Add(Warning(
                "docx.drawingPresetGeometryFallback",
                $"DOCX picture preset geometry '{value}' is preserved in metadata but rendered as rectangular image geometry in the editor.",
                DocumentFormatCompatibilitySeverity.Warning,
                "word/document.xml"));
        }

        return value;
    }

    private static string? ReadPresetGeometryFromRawXml(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            return XDocument.Parse(xml)
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "prstGeom")
                ?.Attribute("prst")
                ?.Value;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static bool IsRectPreset(string? preset)
        => string.IsNullOrWhiteSpace(preset)
            || preset.Equals("rect", StringComparison.OrdinalIgnoreCase)
            || preset.Equals(A.ShapeTypeValues.Rectangle.ToString(), StringComparison.OrdinalIgnoreCase);

    private static DocumentObjectEffectExtent ReadEffectExtent(W.Drawing drawing)
    {
        var extent = drawing.Descendants<DW.EffectExtent>().FirstOrDefault();
        return new DocumentObjectEffectExtent
        {
            Left = extent?.LeftEdge?.Value ?? 0L,
            Top = extent?.TopEdge?.Value ?? 0L,
            Right = extent?.RightEdge?.Value ?? 0L,
            Bottom = extent?.BottomEdge?.Value ?? 0L
        };
    }

    private static string? FirstNonWhiteSpace(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record DocxPartReadContext(
        OpenXmlPartContainer OwnerPart,
        DocumentRenditionAnchorScope Region,
        string? HeaderFooterId = null,
        string? TableId = null,
        string? CellId = null)
    {
        public DocxPartReadContext ForTableCell(string tableId, string cellId)
            => this with
            {
                Region = DocumentRenditionAnchorScope.TableCell,
                TableId = tableId,
                CellId = cellId
            };
    }

    private DocumentObjectLayout ReadObjectLayout(W.Drawing drawing, DocumentDocxDrawingMetadata? metadata = null)
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
                fallbackWrapSide: DocumentObjectWrapSide.BothSides,
                fallbackWrapContourPoints: [],
                fallbackHorizontalPosition: null,
                fallbackVerticalAlignment: DocumentObjectVerticalAlignment.None,
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
        var wrapElement = GetWrapElement(anchor);
        var fallbackWrapMode = GetWrapMode(anchor, wrapElement);
        var extent = anchor.GetFirstChild<DW.Extent>();

        var hAlign = horizontal?.GetFirstChild<DW.HorizontalAlignment>()?.Text?.Trim().ToLowerInvariant();
        var horizontalPosition = FromDocxHorizontalAlignment(hAlign);
        var verticalAlignment = FromDocxVerticalAlignment(vertical?.GetFirstChild<DW.VerticalAlignment>()?.Text?.Trim().ToLowerInvariant());
        var fallbackKind = IsFixedAnchor(horizontal, vertical)
            ? DocumentObjectLayoutKind.Fixed
            : DocumentObjectLayoutKind.Anchored;

        return ReadTempoLayout(
            layoutElement,
            drawing,
            fallbackKind: fallbackKind,
            fallbackWrapMode: fallbackWrapMode,
            fallbackWrapSide: FromDocxWrapText(wrapElement),
            fallbackWrapContourPoints: ReadWrapContourPoints(wrapElement, extent, fallbackWrapMode),
            fallbackHorizontalPosition: horizontalPosition,
            fallbackVerticalAlignment: verticalAlignment,
            fallbackHorizontalRelativeTo: FromDocxHorizontalRelative(horizontal?.RelativeFrom?.Value),
            fallbackVerticalRelativeTo: FromDocxVerticalRelative(vertical?.RelativeFrom?.Value),
            fallbackX: EmuToPoint(horizontal?.GetFirstChild<DW.PositionOffset>()?.Text),
            fallbackY: EmuToPoint(vertical?.GetFirstChild<DW.PositionOffset>()?.Text),
            fallbackDistanceLeft: ReadWrapDistanceLeft(wrapElement, anchor),
            fallbackDistanceRight: ReadWrapDistanceRight(wrapElement, anchor),
            fallbackDistanceTop: ReadWrapDistanceTop(wrapElement, anchor),
            fallbackDistanceBottom: ReadWrapDistanceBottom(wrapElement, anchor),
            fallbackZIndex: (int)(anchor.RelativeHeight?.Value ?? 0),
            fallbackAllowOverlap: anchor.AllowOverlap?.Value == true,
            fallbackLockAnchor: anchor.Locked?.Value == true);
    }

    private static bool IsFixedAnchor(DW.HorizontalPosition? horizontal, DW.VerticalPosition? vertical)
        => horizontal?.RelativeFrom?.Value == DW.HorizontalRelativePositionValues.Page
            && vertical?.RelativeFrom?.Value == DW.VerticalRelativePositionValues.Page;

    private static OpenXmlElement? GetWrapElement(DW.Anchor anchor)
        => anchor.ChildElements.FirstOrDefault(element =>
            element is DW.WrapNone or DW.WrapSquare or DW.WrapTight or DW.WrapThrough or DW.WrapTopBottom);

    private static DocumentWrapMode GetWrapMode(DW.Anchor anchor, OpenXmlElement? wrapElement)
        => wrapElement switch
        {
            DW.WrapTopBottom => DocumentWrapMode.TopBottom,
            DW.WrapTight => DocumentWrapMode.Tight,
            DW.WrapThrough => DocumentWrapMode.Through,
            DW.WrapNone => anchor.BehindDoc?.Value == true ? DocumentWrapMode.BehindText : DocumentWrapMode.InFrontOfText,
            _ => DocumentWrapMode.Square
        };

    private static DocumentObjectWrapSide FromDocxWrapText(OpenXmlElement? wrapElement)
    {
        var value = wrapElement switch
        {
            DW.WrapSquare square => square.WrapText?.Value,
            DW.WrapTight tight => tight.WrapText?.Value,
            DW.WrapThrough through => through.WrapText?.Value,
            _ => null
        };

        if (value == DW.WrapTextValues.Left)
        {
            return DocumentObjectWrapSide.Left;
        }

        if (value == DW.WrapTextValues.Right)
        {
            return DocumentObjectWrapSide.Right;
        }

        if (value == DW.WrapTextValues.Largest)
        {
            return DocumentObjectWrapSide.Largest;
        }

        return DocumentObjectWrapSide.BothSides;
    }

    private IReadOnlyList<DocumentObjectWrapPoint> ReadWrapContourPoints(OpenXmlElement? wrapElement, DW.Extent? extent, DocumentWrapMode mode)
    {
        if (mode is not (DocumentWrapMode.Tight or DocumentWrapMode.Through))
        {
            return [];
        }

        var polygon = wrapElement?.GetFirstChild<DW.WrapPolygon>();
        if (polygon is null)
        {
            _warnings.Add(Warning(
                "docx.drawingWrapPolygonMissing",
                "DOCX tight/through drawing wrap is missing wp:wrapPolygon; a rectangular contour fallback was imported.",
                DocumentFormatCompatibilitySeverity.Warning,
                "word/document.xml"));
            return DefaultWrapContourPoints();
        }

        var cx = extent?.Cx?.Value ?? 0L;
        var cy = extent?.Cy?.Value ?? 0L;
        if (cx <= 0 || cy <= 0)
        {
            _warnings.Add(Warning(
                "docx.drawingWrapPolygonExtentMissing",
                "DOCX drawing wrap polygon could not be normalized because wp:extent is missing or empty; a rectangular contour fallback was imported.",
                DocumentFormatCompatibilitySeverity.Warning,
                "word/document.xml"));
            return DefaultWrapContourPoints();
        }

        var sourcePoints = new List<DW.Point2DType>();
        if (polygon.GetFirstChild<DW.StartPoint>() is { } start)
        {
            sourcePoints.Add(start);
        }

        sourcePoints.AddRange(polygon.Elements<DW.LineTo>());
        var points = sourcePoints
            .Select(point => new DocumentObjectWrapPoint
            {
                X = Math.Clamp((double)(point.X?.Value ?? 0L) / cx, 0, 1),
                Y = Math.Clamp((double)(point.Y?.Value ?? 0L) / cy, 0, 1)
            })
            .ToList();
        if (points.Count >= 3)
        {
            return points;
        }

        _warnings.Add(Warning(
            "docx.drawingWrapPolygonMissing",
            "DOCX tight/through drawing wrap polygon has too few points; a rectangular contour fallback was imported.",
            DocumentFormatCompatibilitySeverity.Warning,
            "word/document.xml"));
        return DefaultWrapContourPoints();
    }

    private static IReadOnlyList<DocumentObjectWrapPoint> DefaultWrapContourPoints()
        =>
        [
            new() { X = 0, Y = 0 },
            new() { X = 1, Y = 0 },
            new() { X = 1, Y = 1 },
            new() { X = 0, Y = 1 }
        ];

    private static double ReadWrapDistanceLeft(OpenXmlElement? wrapElement, DW.Anchor anchor)
        => wrapElement switch
        {
            DW.WrapSquare square when square.DistanceFromLeft is not null => EmuToPoint(square.DistanceFromLeft.Value.ToString(CultureInfo.InvariantCulture)),
            DW.WrapTight tight when tight.DistanceFromLeft is not null => EmuToPoint(tight.DistanceFromLeft.Value.ToString(CultureInfo.InvariantCulture)),
            DW.WrapThrough through when through.DistanceFromLeft is not null => EmuToPoint(through.DistanceFromLeft.Value.ToString(CultureInfo.InvariantCulture)),
            _ => EmuToPoint(anchor.DistanceFromLeft?.Value.ToString(CultureInfo.InvariantCulture))
        };

    private static double ReadWrapDistanceRight(OpenXmlElement? wrapElement, DW.Anchor anchor)
        => wrapElement switch
        {
            DW.WrapSquare square when square.DistanceFromRight is not null => EmuToPoint(square.DistanceFromRight.Value.ToString(CultureInfo.InvariantCulture)),
            DW.WrapTight tight when tight.DistanceFromRight is not null => EmuToPoint(tight.DistanceFromRight.Value.ToString(CultureInfo.InvariantCulture)),
            DW.WrapThrough through when through.DistanceFromRight is not null => EmuToPoint(through.DistanceFromRight.Value.ToString(CultureInfo.InvariantCulture)),
            _ => EmuToPoint(anchor.DistanceFromRight?.Value.ToString(CultureInfo.InvariantCulture))
        };

    private static double ReadWrapDistanceTop(OpenXmlElement? wrapElement, DW.Anchor anchor)
        => wrapElement switch
        {
            DW.WrapSquare { DistanceFromTop: not null } square => EmuToPoint(square.DistanceFromTop.Value.ToString(CultureInfo.InvariantCulture)),
            DW.WrapTopBottom { DistanceFromTop: not null } topBottom => EmuToPoint(topBottom.DistanceFromTop.Value.ToString(CultureInfo.InvariantCulture)),
            _ => EmuToPoint(anchor.DistanceFromTop?.Value.ToString(CultureInfo.InvariantCulture))
        };

    private static double ReadWrapDistanceBottom(OpenXmlElement? wrapElement, DW.Anchor anchor)
        => wrapElement switch
        {
            DW.WrapSquare { DistanceFromBottom: not null } square => EmuToPoint(square.DistanceFromBottom.Value.ToString(CultureInfo.InvariantCulture)),
            DW.WrapTopBottom { DistanceFromBottom: not null } topBottom => EmuToPoint(topBottom.DistanceFromBottom.Value.ToString(CultureInfo.InvariantCulture)),
            _ => EmuToPoint(anchor.DistanceFromBottom?.Value.ToString(CultureInfo.InvariantCulture))
        };

    private static DocumentObjectLayout ReadTempoLayout(
        OpenXmlElement element,
        W.Drawing drawing,
        DocumentObjectLayoutKind fallbackKind,
        DocumentWrapMode fallbackWrapMode,
        DocumentObjectWrapSide fallbackWrapSide,
        IReadOnlyList<DocumentObjectWrapPoint> fallbackWrapContourPoints,
        DocumentImageHorizontalPosition? fallbackHorizontalPosition,
        DocumentObjectVerticalAlignment fallbackVerticalAlignment,
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
                TableId = GetTempoAttribute(element, "table-id"),
                CellId = GetTempoAttribute(element, "cell-id"),
                HeaderFooterId = GetTempoAttribute(element, "header-footer-id"),
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
                VerticalAlignment = ParseEnum(GetTempoAttribute(element, "vertical-alignment"), fallbackVerticalAlignment)
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = ParseEnum(GetTempoAttribute(element, "wrap-mode"), fallbackWrapMode),
                Side = ParseEnum(GetTempoAttribute(element, "wrap-side"), fallbackWrapSide),
                DistanceLeft = ParseDouble(GetTempoAttribute(element, "distance-left"), fallbackDistanceLeft),
                DistanceRight = ParseDouble(GetTempoAttribute(element, "distance-right"), fallbackDistanceRight),
                DistanceTop = ParseDouble(GetTempoAttribute(element, "distance-top"), fallbackDistanceTop),
                DistanceBottom = ParseDouble(GetTempoAttribute(element, "distance-bottom"), fallbackDistanceBottom),
                WrapContourPoints = fallbackWrapContourPoints.Select(point => new DocumentObjectWrapPoint { X = point.X, Y = point.Y }).ToList()
            },
            Transform = new DocumentObjectTransform
            {
                Width = ParseNullableDouble(GetTempoAttribute(element, "width")),
                Height = ParseNullableDouble(GetTempoAttribute(element, "height")),
                NaturalWidth = ParseNullableDouble(GetTempoAttribute(element, "natural-width")),
                NaturalHeight = ParseNullableDouble(GetTempoAttribute(element, "natural-height")),
                LockAspectRatio = ParseBool(GetTempoAttribute(element, "lock-aspect-ratio"), true),
                Rotation = ParseDouble(GetTempoAttribute(element, "rotation"), DocxTransformConverter.ReadRotation(drawing)),
                Crop = DocxCropConverter.FromSourceRectangle(drawing.Descendants<A.SourceRectangle>().FirstOrDefault()),
                Flip = DocxTransformConverter.ReadFlip(drawing)
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

    private T? DeserializeTempoJson<T>(string? json, string sourcePath, string warningCode)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            _warnings.Add(Warning(
                warningCode,
                $"DOCX Tempo metadata '{typeof(T).Name}' could not be parsed and was ignored.",
                DocumentFormatCompatibilitySeverity.Warning,
                sourcePath));
            return default;
        }
    }

    private static string ReadElementId(OpenXmlElement element, string tempoAttributeName)
        => GetTempoAttribute(element, tempoAttributeName) ?? Guid.NewGuid().ToString("N");

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

    private static double EmuToPoint(string? value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var emu)
            ? Math.Round(DocxUnitConverter.EmuToPoint(emu), 2)
            : 0;
    }

    private DocumentRelativePosition FromDocxHorizontalRelative(DW.HorizontalRelativePositionValues? value)
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

        if (value == DW.HorizontalRelativePositionValues.LeftMargin
            || value == DW.HorizontalRelativePositionValues.RightMargin
            || value == DW.HorizontalRelativePositionValues.InsideMargin
            || value == DW.HorizontalRelativePositionValues.OutsideMargin)
        {
            _warnings.Add(Warning(
                "docx.drawingHorizontalReferenceFallback",
                $"DOCX drawing horizontal reference '{value}' was approximated as page margin.",
                DocumentFormatCompatibilitySeverity.Warning,
                "word/document.xml"));
            return DocumentRelativePosition.Margin;
        }

        return DocumentRelativePosition.Page;
    }

    private DocumentRelativePosition FromDocxVerticalRelative(DW.VerticalRelativePositionValues? value)
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

        if (value == DW.VerticalRelativePositionValues.TopMargin
            || value == DW.VerticalRelativePositionValues.BottomMargin
            || value == DW.VerticalRelativePositionValues.InsideMargin
            || value == DW.VerticalRelativePositionValues.OutsideMargin)
        {
            _warnings.Add(Warning(
                "docx.drawingVerticalReferenceFallback",
                $"DOCX drawing vertical reference '{value}' was approximated as page margin.",
                DocumentFormatCompatibilitySeverity.Warning,
                "word/document.xml"));
            return DocumentRelativePosition.Margin;
        }

        return DocumentRelativePosition.Paragraph;
    }

    private DocumentImageHorizontalPosition? FromDocxHorizontalAlignment(string? value)
    {
        return value switch
        {
            "left" => DocumentImageHorizontalPosition.Left,
            "center" => DocumentImageHorizontalPosition.Center,
            "right" => DocumentImageHorizontalPosition.Right,
            "inside" => WarnAndReturnHorizontalAlignment(value, DocumentImageHorizontalPosition.Left),
            "outside" => WarnAndReturnHorizontalAlignment(value, DocumentImageHorizontalPosition.Right),
            _ => null
        };
    }

    private DocumentImageHorizontalPosition WarnAndReturnHorizontalAlignment(string value, DocumentImageHorizontalPosition fallback)
    {
        _warnings.Add(Warning(
            "docx.drawingHorizontalAlignmentFallback",
            $"DOCX drawing horizontal alignment '{value}' was approximated as '{fallback}'.",
            DocumentFormatCompatibilitySeverity.Warning,
            "word/document.xml"));
        return fallback;
    }

    private DocumentObjectVerticalAlignment FromDocxVerticalAlignment(string? value)
    {
        return value switch
        {
            "top" => DocumentObjectVerticalAlignment.Top,
            "center" => DocumentObjectVerticalAlignment.Middle,
            "bottom" => DocumentObjectVerticalAlignment.Bottom,
            "inside" or "outside" => WarnAndReturnVerticalAlignment(value),
            _ => DocumentObjectVerticalAlignment.None
        };
    }

    private DocumentObjectVerticalAlignment WarnAndReturnVerticalAlignment(string value)
    {
        _warnings.Add(Warning(
            "docx.drawingVerticalAlignmentFallback",
            $"DOCX drawing vertical alignment '{value}' was approximated as no vertical alignment.",
            DocumentFormatCompatibilitySeverity.Warning,
            "word/document.xml"));
        return DocumentObjectVerticalAlignment.None;
    }

    private async Task<List<DocumentHeaderFooter>> ReadHeadersFootersAsync(MainDocumentPart mainPart, CancellationToken cancellationToken)
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
            var header = new DocumentHeaderFooter
            {
                Id = part.Header is null ? Guid.NewGuid().ToString("N") : ReadElementId(part.Header, "header-footer-id"),
                Type = DocumentHeaderFooterType.Header,
                Scope = headerScopes.GetValueOrDefault(relationshipId, DocumentHeaderFooterScope.Primary)
            };
            header.Blocks = await ReadPartParagraphBlocksAsync(
                part.Header?.Elements<W.Paragraph>(),
                new DocxPartReadContext(part, DocumentRenditionAnchorScope.Header, HeaderFooterId: header.Id),
                cancellationToken);
            result.Add(header);
        }

        foreach (var part in mainPart.FooterParts)
        {
            var relationshipId = mainPart.GetIdOfPart(part);
            var footer = new DocumentHeaderFooter
            {
                Id = part.Footer is null ? Guid.NewGuid().ToString("N") : ReadElementId(part.Footer, "header-footer-id"),
                Type = DocumentHeaderFooterType.Footer,
                Scope = footerScopes.GetValueOrDefault(relationshipId, DocumentHeaderFooterScope.Primary)
            };
            footer.Blocks = await ReadPartParagraphBlocksAsync(
                part.Footer?.Elements<W.Paragraph>(),
                new DocxPartReadContext(part, DocumentRenditionAnchorScope.Footer, HeaderFooterId: footer.Id),
                cancellationToken);
            result.Add(footer);
        }

        return result;
    }

    private async Task<List<DocumentBlock>> ReadPartParagraphBlocksAsync(
        IEnumerable<W.Paragraph>? paragraphs,
        DocxPartReadContext context,
        CancellationToken cancellationToken)
    {
        var blocks = new List<DocumentBlock>();
        var order = 0;
        foreach (var paragraph in paragraphs ?? [])
        {
            var inlines = await ReadInlinesAsync(paragraph.ChildElements, context, cancellationToken: cancellationToken);
            var block = new DocumentBlock
            {
                Id = ReadElementId(paragraph, "block-id"),
                Type = DocumentBlockType.Paragraph,
                Order = order++,
                Content = new ParagraphBlockContent { Inlines = inlines }
            };
            NormalizeDrawingAnchors(block, inlines);
            blocks.Add(block);
        }

        return blocks;
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

    private async Task<List<DocumentNote>> ReadNotesAsync(MainDocumentPart mainPart, CancellationToken cancellationToken)
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
                    Blocks = await ReadPartParagraphBlocksAsync(
                        footnote.Elements<W.Paragraph>(),
                        new DocxPartReadContext(mainPart.FootnotesPart, DocumentRenditionAnchorScope.Footnote),
                        cancellationToken)
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
                    Blocks = await ReadPartParagraphBlocksAsync(
                        endnote.Elements<W.Paragraph>(),
                        new DocxPartReadContext(mainPart.EndnotesPart, DocumentRenditionAnchorScope.Endnote),
                        cancellationToken)
                });
            }
        }

        return result;
    }

    private async Task<List<DocumentComment>> ReadCommentsAsync(MainDocumentPart mainPart, CancellationToken cancellationToken)
    {
        var commentsPart = mainPart.WordprocessingCommentsPart;
        if (commentsPart?.Comments is null)
        {
            return [];
        }

        var result = new List<DocumentComment>();
        foreach (var comment in commentsPart.Comments.Elements<W.Comment>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entries = new List<DocumentCommentEntry>();
            foreach (var paragraph in comment.Elements<W.Paragraph>())
            {
                var inlines = await ReadInlinesAsync(
                    paragraph.ChildElements,
                    new DocxPartReadContext(commentsPart, DocumentRenditionAnchorScope.Comment),
                    cancellationToken: cancellationToken);
                entries.Add(new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor
                    {
                        DisplayName = comment.Author?.Value ?? string.Empty
                    },
                    Text = GetInlineText(inlines),
                    Inlines = inlines,
                    CreatedAt = comment.Date?.Value ?? DateTimeOffset.UtcNow
                });
            }

            if (entries.Count == 0)
            {
                entries.Add(new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor
                    {
                        DisplayName = comment.Author?.Value ?? string.Empty
                    },
                    Text = string.Join("\n", comment.Descendants<W.Text>().Select(t => t.Text)),
                    CreatedAt = comment.Date?.Value ?? DateTimeOffset.UtcNow
                });
            }

            result.Add(new DocumentComment
            {
                Id = comment.Id?.Value ?? Guid.NewGuid().ToString("N"),
                SourceFormat = "docx",
                ExternalId = comment.Id?.Value,
                Anchor = new DocumentCommentAnchor
                {
                    Type = DocumentCommentAnchorType.ImportedDocx,
                    ExternalAnchorId = comment.Id?.Value
                },
                Entries = entries
            });
        }

        return result;
    }

    private static string GetInlineText(IEnumerable<InlineContent> inlines)
        => string.Concat(inlines.Select(inline => inline switch
        {
            TextRun text => text.Text,
            TokenRun token => token.DisplayName,
            DocumentFieldRun field => field.DisplayText ?? field.FallbackText ?? string.Empty,
            DocumentMathRun math => math.AltText ?? DocumentMathText.FlattenMathContent(math.Content),
            DocumentContentControlRun control => GetContentControlText(control),
            DocumentNoteReferenceRun note => note.NoteId,
            DocumentDrawingRun drawing => drawing.AltText ?? string.Empty,
            _ => string.Empty
        }));

    private static string GetContentControlText(DocumentContentControlRun control)
    {
        var inlineText = GetInlineText(control.Inlines);
        if (!string.IsNullOrWhiteSpace(inlineText))
        {
            return inlineText;
        }

        return control.Control.Value.Text
            ?? control.Control.Value.SelectedValue
            ?? control.Control.Value.DateIso
            ?? control.Control.Value.AssetId
            ?? control.Control.PlaceholderText
            ?? control.Control.Alias
            ?? control.Control.Tag
            ?? string.Empty;
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

        var sections = DeserializeTempoJson<List<DocumentSection>>(
            GetTempoAttribute(sectionProperties, "sections-json"),
            "word/document.xml",
            "docx.sectionMetadataInvalid");
        if (sections is { Count: > 0 })
        {
            doc.Sections = sections.OrderBy(item => item.Order).ToList();
            section = doc.Sections[0];
        }
        else
        {
            var sectionId = GetTempoAttribute(sectionProperties, "section-id");
            if (!string.IsNullOrWhiteSpace(sectionId))
            {
                section.Id = sectionId;
            }
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

        var columns = sectionProperties.GetFirstChild<W.Columns>();
        if (columns is not null)
        {
            section.Properties.Columns = new DocumentSectionColumns
            {
                Count = Math.Max(1, (int)(columns.ColumnCount?.Value ?? 1)),
                Spacing = TwipsToPointsOrZero(columns.Space?.Value),
                SeparatorLine = columns.Separator?.Value == true,
                Preset = (columns.ColumnCount?.Value ?? 1) switch
                {
                    2 => "two",
                    3 => "three",
                    _ => "custom"
                },
                Items = columns.Elements<W.Column>()
                    .Select(column => new DocumentSectionColumn
                    {
                        Width = TwipsToNullablePoints(column.Width?.Value),
                        SpacingAfter = TwipsToNullablePoints(column.Space?.Value)
                    })
                    .ToList()
            };
        }

        var lineNumbering = sectionProperties.GetFirstChild<W.LineNumberType>();
        if (lineNumbering is not null)
        {
            section.Properties.LineNumbering = new DocumentLineNumbering
            {
                Enabled = true,
                StartAt = lineNumbering.Start?.Value ?? 1,
                Increment = lineNumbering.CountBy?.Value ?? 1,
                DistanceFromText = TwipsToPointsOrZero(lineNumbering.Distance?.Value),
                Restart = ToLineNumberingRestart(lineNumbering.Restart?.Value)
            };
        }
    }

    private static double TwipsToPoints(double twips) => DocxUnitConverter.TwipToPoint(twips);

    private static DocumentLineNumberingRestart ToLineNumberingRestart(W.LineNumberRestartValues? value)
    {
        if (value == W.LineNumberRestartValues.NewPage)
        {
            return DocumentLineNumberingRestart.Page;
        }

        if (value == W.LineNumberRestartValues.NewSection)
        {
            return DocumentLineNumberingRestart.Section;
        }

        return DocumentLineNumberingRestart.Continuous;
    }

    private static double TwipsToPointsOrZero(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var twips)
            ? TwipsToPoints(twips)
            : 0;

    private static string GetPartSourcePath(OpenXmlPartContainer ownerPart)
        => ownerPart is OpenXmlPart part
            ? part.Uri.ToString().TrimStart('/')
            : "word/document.xml";

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

    private static DocumentFormatCompatibilityWarning Warning(
        string code,
        string message,
        DocumentFormatCompatibilitySeverity severity,
        string? path = null,
        string? objectId = null)
    {
        return new DocumentFormatCompatibilityWarning
        {
            Code = code,
            Message = message,
            Severity = severity,
            SourcePath = path,
            ObjectId = objectId
        };
    }
}
