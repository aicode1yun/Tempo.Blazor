using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.DocumentFormats.Internal;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Nm = Tempo.Blazor.NotionEditor.Models;
using Dm = Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Notion;

/// <summary>Converts the Tempo document editor model into Notion editor page blocks.</summary>
public static partial class DocumentModelToNotionConverter
{
    private const string ApproximateWarningCode = "document.block.approximate";
    private const string TableCompatibilityWarningCode = "document.table.compatibility";

    /// <summary>Converts an ordered document into Notion page blocks for the specified page id.</summary>
    public static DocumentModelToNotionConversionResult ConvertDocument(Dm.DocumentEditorDocument document, Guid pageId)
    {
        ArgumentNullException.ThrowIfNull(document);

        var warnings = new List<DocumentFormatCompatibilityWarning>();
        var blocks = ConvertBlocks(document.Blocks, pageId, warnings);
        return new DocumentModelToNotionConversionResult(blocks, warnings);
    }

    /// <summary>Converts ordered document blocks into Notion page blocks.</summary>
    public static List<IPageBlock> ConvertBlocks(
        IReadOnlyList<Dm.DocumentBlock> blocks,
        Guid pageId,
        IList<DocumentFormatCompatibilityWarning>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var result = new List<IPageBlock>();
        foreach (var block in blocks.OrderBy(block => block.Order).ThenBy(block => block.Id, StringComparer.Ordinal))
        {
            AppendBlock(result, block, pageId, result.Count, warnings);
        }

        return result;
    }

    private static void AppendBlock(
        ICollection<IPageBlock> result,
        Dm.DocumentBlock block,
        Guid pageId,
        int order,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        switch (block.Content)
        {
            case Dm.ParagraphBlockContent paragraph when TryCreateImageFromStandaloneDrawing(paragraph.Inlines, out var image):
                AppendImage(result, pageId, order, image, block, warnings);
                break;
            case Dm.ParagraphBlockContent paragraph:
                result.Add(CreateBlock(pageId, null, BlockType.Paragraph, order, new Nm.TextBlockContent
                {
                    Html = RenderInlines(paragraph.Inlines)
                }));
                break;
            case Dm.HeadingBlockContent heading:
                var level = Math.Clamp(heading.Level, 1, 3);
                result.Add(CreateBlock(pageId, null, level switch
                {
                    1 => BlockType.Heading1,
                    2 => BlockType.Heading2,
                    _ => BlockType.Heading3
                }, order, new Nm.HeadingBlockContent
                {
                    Level = level,
                    Html = RenderInlines(heading.Inlines)
                }));
                break;
            case Dm.ListBlockContent list:
                AppendListBlock(result, pageId, order, list);
                break;
            case Dm.QuoteBlockContent quote:
                result.Add(CreateBlock(pageId, null, BlockType.Quote, order, new Nm.TextBlockContent
                {
                    Html = RenderInlines(quote.Inlines)
                }));
                break;
            case Dm.TableBlockContent table:
                AppendTable(result, pageId, order, table, block, warnings);
                break;
            case Dm.ImageBlockContent image:
                AppendImage(result, pageId, order, image, block, warnings);
                break;
            case Dm.CodeBlockContent code:
                result.Add(CreateBlock(pageId, null, BlockType.Code, order, new Nm.CodeBlockContent
                {
                    Language = code.Language ?? string.Empty,
                    Code = code.Code
                }));
                break;
            case Dm.PageBreakBlockContent:
                result.Add(CreateBlock(pageId, null, BlockType.Divider, order, new Nm.DividerBlockContent()));
                break;
            default:
                AddApproximateWarning(warnings, block, $"{block.Type} was imported as a paragraph fallback.");
                result.Add(CreateBlock(pageId, null, BlockType.Paragraph, order, new Nm.TextBlockContent
                {
                    Html = WebUtility.HtmlEncode(DocumentModelText.GetBlockText(block))
                }));
                break;
        }
    }

    private static bool TryCreateImageFromStandaloneDrawing(
        IReadOnlyList<Dm.InlineContent> inlines,
        out Dm.ImageBlockContent image)
    {
        image = new Dm.ImageBlockContent();
        if (inlines.Count != 1 || inlines[0] is not Dm.DocumentDrawingRun drawing || drawing.Kind != Dm.DocumentDrawingKind.Image)
        {
            return false;
        }

        image = new Dm.ImageBlockContent
        {
            Source = drawing.Source,
            Url = drawing.Url,
            AssetId = drawing.AssetId,
            AltText = drawing.AltText,
            IsDecorative = drawing.IsDecorative,
            Caption = drawing.Caption,
            Size = drawing.Size,
            NaturalSize = drawing.NaturalSize,
            Layout = drawing.Layout,
            LinkUrl = drawing.LinkUrl
        };
        return true;
    }

    private static void AppendListBlock(
        ICollection<IPageBlock> result,
        Guid pageId,
        int order,
        Dm.ListBlockContent list)
    {
        var html = RenderInlines(list.Inlines);

        // Preferred encoding: the checkbox is model state.
        if (list.IsChecked is { } state)
        {
            result.Add(CreateBlock(pageId, null, BlockType.TodoItem, order, new Nm.TodoBlockContent
            {
                IsChecked = state,
                Html = html
            }));
            return;
        }

        // Legacy encoding: the checkbox travelled as a literal "[x] " prefix inside the text.
        // Documents built by 2.0.x callers still arrive this way.
        var plain = DocumentModelText.GetInlineText(list.Inlines).TrimStart();
        if (TryReadTaskPrefix(plain, out var isChecked, out var remaining))
        {
            result.Add(CreateBlock(pageId, null, BlockType.TodoItem, order, new Nm.TodoBlockContent
            {
                IsChecked = isChecked,
                Html = WebUtility.HtmlEncode(remaining)
            }));
            return;
        }

        result.Add(CreateBlock(pageId, null, list.Ordered ? BlockType.NumberedList : BlockType.BulletList, order, new Nm.ListBlockContent
        {
            Html = html,
            IndentLevel = Math.Max(0, list.IndentLevel)
        }));
    }

    private static void AppendTable(
        ICollection<IPageBlock> result,
        Guid pageId,
        int order,
        Dm.TableBlockContent table,
        Dm.DocumentBlock sourceBlock,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        var rows = table.Rows;
        var columnCount = rows.Count == 0
            ? 0
            : rows.Max(row => row.Cells.Sum(cell => Math.Max(1, cell.ColumnSpan)));
        WarnUnsupportedTableLayout(table.Layout, sourceBlock, warnings);
        var tableBlock = CreateBlock(pageId, null, BlockType.Table, order, new Nm.TableBlockContent
        {
            ColumnCount = columnCount,
            HasHeaderRow = rows.FirstOrDefault()?.Cells.Any(cell => cell.IsHeader) == true,
            ColumnAlignments = NormalizeAlignments(table.ColumnAlignments, columnCount),
            ColumnWidths = ReadColumnWidths(rows.FirstOrDefault(), columnCount)
        });
        result.Add(tableBlock);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            result.Add(CreateBlock(pageId, tableBlock.Id, BlockType.TableRow, rowIndex, new Nm.TableRowBlockContent
            {
                RichCells = rows[rowIndex].Cells
                    .Select((cell, cellIndex) => (cell, cellIndex))
                    .Where(item => item.cell.Merge.IsOrigin)
                    .Select(item => ToNotionCell(
                        item.cell,
                        sourceBlock,
                        rowIndex,
                        item.cellIndex,
                        warnings))
                    .ToList()
            }));
        }
    }

    private static void WarnUnsupportedTableLayout(
        Dm.TableLayoutContent layout,
        Dm.DocumentBlock tableBlock,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        var sourcePath = $"document.blocks[{tableBlock.Id}].table.layout";
        if (layout.Width is > 0)
        {
            AddTableCompatibilityWarning(
                warnings,
                tableBlock,
                $"{sourcePath}.width",
                $"Table width '{layout.Width}' is not representable in the canonical Notion table model.");
        }

        WarnUnsupportedTableLayoutBorder(
            layout.Borders.Top,
            "top",
            sourcePath,
            tableBlock,
            warnings);
        WarnUnsupportedTableLayoutBorder(
            layout.Borders.Right,
            "right",
            sourcePath,
            tableBlock,
            warnings);
        WarnUnsupportedTableLayoutBorder(
            layout.Borders.Bottom,
            "bottom",
            sourcePath,
            tableBlock,
            warnings);
        WarnUnsupportedTableLayoutBorder(
            layout.Borders.Left,
            "left",
            sourcePath,
            tableBlock,
            warnings);
    }

    private static void WarnUnsupportedTableLayoutBorder(
        string? border,
        string side,
        string sourcePath,
        Dm.DocumentBlock tableBlock,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        if (string.IsNullOrWhiteSpace(border))
        {
            return;
        }

        AddTableCompatibilityWarning(
            warnings,
            tableBlock,
            $"{sourcePath}.borders.{side}",
            $"Table-level {side} border '{border}' is not representable in the canonical Notion table model.");
    }

    private static IReadOnlyList<double?> ReadColumnWidths(
        Dm.TableRowContent? row,
        int columnCount)
    {
        if (row is null || columnCount == 0)
        {
            return [];
        }

        var widths = new List<double?>(columnCount);
        foreach (var cell in row.Cells)
        {
            var span = Math.Max(1, cell.ColumnSpan);
            var width = cell.Width is > 0 ? cell.Width / span : null;
            for (var index = 0; index < span && widths.Count < columnCount; index++)
            {
                widths.Add(width);
            }
        }

        while (widths.Count < columnCount)
        {
            widths.Add(null);
        }

        return widths.Any(width => width is > 0) ? widths : [];
    }

    private static Nm.NotionTableCell ToNotionCell(
        Dm.TableCellContent cell,
        Dm.DocumentBlock tableBlock,
        int rowIndex,
        int cellIndex,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        var sourcePath =
            $"document.blocks[{tableBlock.Id}].table.rows[{rowIndex}].cells[{cellIndex}]";
        var inlines = ReadCellInlines(cell, tableBlock, sourcePath, warnings);
        var alignment = ReadCellAlignment(cell, tableBlock, sourcePath, warnings);
        var textColors = inlines
            .Select(inline => inline.TextColor)
            .Where(color => color is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Nm.NotionTableCell
        {
            Html = RenderCell(cell),
            Inlines = inlines,
            BackgroundColor = NormalizeColor(cell.BackgroundColor),
            TextColor = textColors.Count == 1 ? textColors[0] : null,
            HorizontalAlignment = alignment,
            VerticalAlignment = cell.VerticalAlignment switch
            {
                Dm.TableCellVerticalAlignment.Middle =>
                    Nm.NotionTableVerticalAlignment.Middle,
                Dm.TableCellVerticalAlignment.Bottom =>
                    Nm.NotionTableVerticalAlignment.Bottom,
                _ => Nm.NotionTableVerticalAlignment.Top
            },
            RowSpan = Math.Max(1, cell.RowSpan),
            ColSpan = Math.Max(1, cell.ColumnSpan),
            Width = cell.Width is > 0 ? cell.Width : null,
            Borders = new Nm.NotionTableCellBorders
            {
                Top = ReadBorder(
                    cell.Borders.Top,
                    $"{sourcePath}.borders.top",
                    tableBlock,
                    warnings),
                Right = ReadBorder(
                    cell.Borders.Right,
                    $"{sourcePath}.borders.right",
                    tableBlock,
                    warnings),
                Bottom = ReadBorder(
                    cell.Borders.Bottom,
                    $"{sourcePath}.borders.bottom",
                    tableBlock,
                    warnings),
                Left = ReadBorder(
                    cell.Borders.Left,
                    $"{sourcePath}.borders.left",
                    tableBlock,
                    warnings)
            }
        };
    }

    private static IReadOnlyList<Nm.NotionRichTextInline> ReadCellInlines(
        Dm.TableCellContent cell,
        Dm.DocumentBlock tableBlock,
        string sourcePath,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        var result = new List<Nm.NotionRichTextInline>();
        var blocks = cell.Blocks.OrderBy(block => block.Order).ToList();
        for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            if (blockIndex > 0)
            {
                result.Add(new Nm.NotionRichTextInline { Text = "\n" });
            }

            var inlines = blocks[blockIndex].Content switch
            {
                Dm.ParagraphBlockContent paragraph => paragraph.Inlines,
                Dm.HeadingBlockContent heading => heading.Inlines,
                Dm.ListBlockContent list => list.Inlines,
                Dm.QuoteBlockContent quote => quote.Inlines,
                _ => []
            };
            for (var inlineIndex = 0; inlineIndex < inlines.Count; inlineIndex++)
            {
                var inline = inlines[inlineIndex];
                var text = inline switch
                {
                    Dm.TextRun run => run.Text,
                    Dm.TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName)
                        ? token.Key
                        : token.DisplayName,
                    Dm.DocumentFieldRun field => FirstNonEmpty(
                        field.DisplayText,
                        field.FallbackText,
                        field.FieldType.ToString()),
                    Dm.DocumentNoteReferenceRun note => FirstNonEmpty(
                        note.DisplayMarker,
                        note.NoteId),
                    Dm.DocumentDrawingRun drawing => FirstNonEmpty(
                        drawing.Caption,
                        drawing.AltText,
                        drawing.Url,
                        drawing.AssetId),
                    _ => string.Empty
                };
                var textColor = inline.Marks
                    .LastOrDefault(mark => mark.Type == Dm.InlineMarkType.TextColor)
                    ?.Value;
                var backgroundColor = inline.Marks
                    .LastOrDefault(mark => mark.Type == Dm.InlineMarkType.Highlight)
                    ?.Value;
                result.Add(new Nm.NotionRichTextInline
                {
                    Text = text,
                    Href = inline.Marks
                        .LastOrDefault(mark =>
                            mark.Type == Dm.InlineMarkType.Link &&
                            IsSafeHref(mark.Link?.Href))
                        ?.Link?.Href,
                    Bold = inline.Marks.Any(mark => mark.Type == Dm.InlineMarkType.Bold),
                    Italic = inline.Marks.Any(mark => mark.Type == Dm.InlineMarkType.Italic),
                    Underline = inline.Marks.Any(mark => mark.Type == Dm.InlineMarkType.Underline),
                    Strikethrough = inline.Marks.Any(mark =>
                        mark.Type == Dm.InlineMarkType.Strikethrough),
                    Code = inline.Marks.Any(mark =>
                        mark.Type == Dm.InlineMarkType.FontFamily &&
                        mark.Value?.Contains("mono", StringComparison.OrdinalIgnoreCase) == true),
                    TextColor = NormalizeColor(textColor),
                    BackgroundColor = NormalizeColor(backgroundColor)
                });

                var unsupportedMarks = inline.Marks
                    .Where(mark => mark.Type is
                        Dm.InlineMarkType.Superscript or
                        Dm.InlineMarkType.Subscript or
                        Dm.InlineMarkType.SmallCaps or
                        Dm.InlineMarkType.AllCaps or
                        Dm.InlineMarkType.DoubleStrikethrough)
                    .Select(mark => mark.Type)
                    .Distinct()
                    .ToList();
                foreach (var unsupportedMark in unsupportedMarks)
                {
                    AddTableCompatibilityWarning(
                        warnings,
                        tableBlock,
                        $"{sourcePath}.blocks[{blockIndex}].inlines[{inlineIndex}].marks",
                        $"Inline mark '{unsupportedMark}' is not representable in a Notion table cell.");
                }
            }
        }

        return result;
    }

    private static Nm.NotionTableHorizontalAlignment ReadCellAlignment(
        Dm.TableCellContent cell,
        Dm.DocumentBlock tableBlock,
        string sourcePath,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        var alignment = cell.Blocks
            .OrderBy(block => block.Order)
            .Select(block => block.ParagraphProperties.Alignment)
            .FirstOrDefault();
        if (alignment == Dm.DocumentTextAlignment.Justify)
        {
            AddTableCompatibilityWarning(
                warnings,
                tableBlock,
                $"{sourcePath}.horizontalAlignment",
                "Justified table-cell text was normalized to left alignment.");
        }

        return alignment switch
        {
            Dm.DocumentTextAlignment.Center => Nm.NotionTableHorizontalAlignment.Center,
            Dm.DocumentTextAlignment.Right => Nm.NotionTableHorizontalAlignment.Right,
            _ => Nm.NotionTableHorizontalAlignment.Left
        };
    }

    private static Nm.NotionTableBorder? ReadBorder(
        string? value,
        string sourcePath,
        Dm.DocumentBlock tableBlock,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return new Nm.NotionTableBorder
            {
                Style = Nm.NotionTableBorderStyle.None,
                Width = 1
            };
        }

        var match = BorderRegex().Match(value.Trim());
        if (!match.Success ||
            !double.TryParse(
                match.Groups["width"].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var width) ||
            !Enum.TryParse<Nm.NotionTableBorderStyle>(
                match.Groups["style"].Value,
                true,
                out var style) ||
            !Nm.NotionCssNormalizer.TryNormalizeColor(
                match.Groups["color"].Value,
                out var color))
        {
            AddTableCompatibilityWarning(
                warnings,
                tableBlock,
                sourcePath,
                $"Table border '{value}' is not representable in the canonical Notion border model.");
            return null;
        }

        if (match.Groups["unit"].Value.Equals("pt", StringComparison.OrdinalIgnoreCase))
        {
            width *= 96d / 72d;
        }

        return new Nm.NotionTableBorder
        {
            Style = style,
            Width = Math.Round(width, 2),
            Color = color
        };
    }

    private static string? NormalizeColor(string? value)
        => Nm.NotionCssNormalizer.TryNormalizeColor(value, out var normalized)
            ? normalized
            : null;

    private static void AddTableCompatibilityWarning(
        IList<DocumentFormatCompatibilityWarning>? warnings,
        Dm.DocumentBlock tableBlock,
        string sourcePath,
        string message)
    {
        warnings?.Add(new DocumentFormatCompatibilityWarning
        {
            Code = TableCompatibilityWarningCode,
            Message = message,
            Severity = DocumentFormatCompatibilitySeverity.Warning,
            SourcePath = sourcePath,
            ObjectId = tableBlock.Id
        });
    }

    /// <summary>Trims or pads the alignment list so it lines up with the table's column count.</summary>
    private static IReadOnlyList<Dm.TableColumnAlignment> NormalizeAlignments(
        IReadOnlyList<Dm.TableColumnAlignment> alignments,
        int columnCount)
    {
        if (columnCount == 0 || alignments.Count == 0)
        {
            return [];
        }

        if (alignments.Count == columnCount)
        {
            return [.. alignments];
        }

        if (alignments.Count > columnCount)
        {
            return [.. alignments.Take(columnCount)];
        }

        var normalized = new List<Dm.TableColumnAlignment>(alignments);
        normalized.AddRange(Enumerable.Repeat(Dm.TableColumnAlignment.None, columnCount - alignments.Count));
        return normalized;
    }

    private static void AppendImage(
        ICollection<IPageBlock> result,
        Guid pageId,
        int order,
        Dm.ImageBlockContent image,
        Dm.DocumentBlock sourceBlock,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        if (image.Source == Dm.DocumentImageSource.Clipboard
            && string.IsNullOrWhiteSpace(image.Url)
            && string.IsNullOrWhiteSpace(image.AssetId))
        {
            AddApproximateWarning(warnings, sourceBlock, "Clipboard image without persisted asset was imported as text.");
            result.Add(CreateBlock(pageId, null, BlockType.Paragraph, order, new Nm.TextBlockContent
            {
                Html = WebUtility.HtmlEncode(FirstNonEmpty(image.Caption, image.AltText, "Image"))
            }));
            return;
        }

        result.Add(CreateBlock(pageId, null, BlockType.Image, order, new Nm.ImageBlockContent
        {
            Url = image.Source == Dm.DocumentImageSource.Url ? image.Url ?? string.Empty : string.Empty,
            FileId = image.Source == Dm.DocumentImageSource.Asset ? image.AssetId : null,
            AltText = image.AltText,
            Caption = image.Caption,
            Width = image.Size.Width.HasValue ? (int)Math.Round(image.Size.Width.Value) : null,
            Alignment = image.Alignment switch
            {
                Dm.DocumentImageAlignment.Start => MediaAlignment.Left,
                _ => MediaAlignment.Center
            }
        }));
    }

    private static string RenderCell(Dm.TableCellContent cell)
    {
        var rendered = cell.Blocks
            .OrderBy(block => block.Order)
            .Select(RenderCellBlock)
            .Where(value => value.Length > 0)
            .ToList();

        return Nm.NotionHtmlSanitizer.SanitizeHtmlFragment(string.Join("<br>", rendered));
    }

    private static string RenderCellBlock(Dm.DocumentBlock block)
    {
        return block.Content switch
        {
            Dm.ParagraphBlockContent paragraph => RenderInlines(paragraph.Inlines),
            Dm.HeadingBlockContent heading => RenderInlines(heading.Inlines),
            Dm.ListBlockContent list => RenderInlines(list.Inlines),
            Dm.QuoteBlockContent quote => RenderInlines(quote.Inlines),
            Dm.ImageBlockContent image => WebUtility.HtmlEncode(FirstNonEmpty(image.Caption, image.AltText, image.Url, image.AssetId)),
            Dm.CodeBlockContent code => code.Code,
            Dm.PageBreakBlockContent => string.Empty,
            _ => WebUtility.HtmlEncode(DocumentModelText.GetBlockText(block))
        };
    }

    private static string RenderInlines(IEnumerable<Dm.InlineContent> inlines)
    {
        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            var rendered = inline switch
            {
                Dm.TextRun run => WebUtility.HtmlEncode(run.Text),
                Dm.TokenRun token => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName),
                Dm.DocumentFieldRun field => WebUtility.HtmlEncode(FirstNonEmpty(field.DisplayText, field.FallbackText, field.FieldType.ToString())),
                Dm.DocumentNoteReferenceRun note => WebUtility.HtmlEncode(FirstNonEmpty(note.DisplayMarker, note.NoteId)),
                Dm.DocumentDrawingRun drawing => WebUtility.HtmlEncode(FirstNonEmpty(drawing.Caption, drawing.AltText, drawing.Url, drawing.AssetId)),
                _ => string.Empty
            };

            builder.Append(ApplyMarks(rendered, inline.Marks));
        }

        return builder.ToString();
    }

    private static string ApplyMarks(string text, IEnumerable<Dm.InlineMark> marks)
    {
        foreach (var mark in marks)
        {
            text = mark.Type switch
            {
                Dm.InlineMarkType.Bold => $"<strong>{text}</strong>",
                Dm.InlineMarkType.Italic => $"<em>{text}</em>",
                Dm.InlineMarkType.Underline => $"<u>{text}</u>",
                Dm.InlineMarkType.Strikethrough => $"<s>{text}</s>",
                Dm.InlineMarkType.Superscript => $"<sup>{text}</sup>",
                Dm.InlineMarkType.Subscript => $"<sub>{text}</sub>",
                Dm.InlineMarkType.TextColor
                    when NormalizeColor(mark.Value) is { } color =>
                    $"<span style=\"color:{color}\">{text}</span>",
                Dm.InlineMarkType.Highlight
                    when NormalizeColor(mark.Value) is { } background =>
                    $"<span style=\"background-color:{background}\">{text}</span>",
                Dm.InlineMarkType.Link when IsSafeHref(mark.Link?.Href) => $"<a href=\"{WebUtility.HtmlEncode(mark.Link!.Href)}\">{text}</a>",
                _ => text
            };
        }

        return text;
    }

    private static bool TryReadTaskPrefix(string value, out bool isChecked, out string remaining)
    {
        isChecked = false;
        remaining = value;
        if (value.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
        {
            isChecked = true;
            remaining = value[4..];
            return true;
        }

        if (value.StartsWith("[ ] ", StringComparison.Ordinal))
        {
            remaining = value[4..];
            return true;
        }

        return false;
    }

    private static PageBlock CreateBlock(
        Guid pageId,
        Guid? parentBlockId,
        BlockType type,
        int order,
        Nm.IBlockContent content)
        => new()
        {
            Id = Guid.NewGuid(),
            PageId = pageId,
            ParentBlockId = parentBlockId,
            Type = type,
            Order = order,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        };

    private static void AddApproximateWarning(
        IList<DocumentFormatCompatibilityWarning>? warnings,
        Dm.DocumentBlock block,
        string message)
    {
        warnings?.Add(new DocumentFormatCompatibilityWarning
        {
            Code = ApproximateWarningCode,
            Message = message,
            Severity = DocumentFormatCompatibilitySeverity.Warning,
            SourcePath = $"document:block:{block.Type}:{block.Id}",
            ObjectId = block.Id
        });
    }

    private static bool IsSafeHref(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "mailto";
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    [GeneratedRegex(
        @"^(?<width>\d+(?:\.\d+)?)\s*(?<unit>px|pt)\s+(?<style>solid|dashed|dotted|double)\s+(?<color>#[0-9a-fA-F]{3,8}|[a-zA-Z]+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BorderRegex();
}

/// <summary>Result of converting a document model into Notion page blocks.</summary>
public sealed record DocumentModelToNotionConversionResult(
    IReadOnlyList<IPageBlock> Blocks,
    IReadOnlyList<DocumentFormatCompatibilityWarning> Warnings);
