using System.Net;
using System.Text;
using Tempo.Blazor.DocumentFormats.Internal;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Nm = Tempo.Blazor.NotionEditor.Models;
using Dm = Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Notion;

/// <summary>Converts the Tempo document editor model into Notion editor page blocks.</summary>
public static class DocumentModelToNotionConverter
{
    private const string ApproximateWarningCode = "document.block.approximate";

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
                AppendTable(result, pageId, order, table);
                break;
            case Dm.ImageBlockContent image:
                AppendImage(result, pageId, order, image, block, warnings);
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

    private static void AppendListBlock(
        ICollection<IPageBlock> result,
        Guid pageId,
        int order,
        Dm.ListBlockContent list)
    {
        var html = RenderInlines(list.Inlines);
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
        Dm.TableBlockContent table)
    {
        var rows = table.Rows;
        var columnCount = rows.Count == 0 ? 0 : rows.Max(row => row.Cells.Count);
        var tableBlock = CreateBlock(pageId, null, BlockType.Table, order, new Nm.TableBlockContent
        {
            ColumnCount = columnCount,
            HasHeaderRow = rows.FirstOrDefault()?.Cells.Any(cell => cell.IsHeader) == true
        });
        result.Add(tableBlock);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            result.Add(CreateBlock(pageId, tableBlock.Id, BlockType.TableRow, rowIndex, new Nm.TableRowBlockContent
            {
                Cells = rows[rowIndex].Cells.Select(RenderCell).ToList()
            }));
        }
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

        return string.Join("<br>", rendered);
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
}

/// <summary>Result of converting a document model into Notion page blocks.</summary>
public sealed record DocumentModelToNotionConversionResult(
    IReadOnlyList<IPageBlock> Blocks,
    IReadOnlyList<DocumentFormatCompatibilityWarning> Warnings);
