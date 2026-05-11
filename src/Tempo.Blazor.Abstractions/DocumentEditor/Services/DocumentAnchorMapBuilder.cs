using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Builds normalized rendition anchors from the editable document model.</summary>
public class DocumentAnchorMapBuilder
{
    private const double DefaultLineHeight = 24;
    private const double DefaultTableRowHeight = 36;
    private const double DefaultAnchorWidth = 0.2;
    private const double DefaultAnchorHeight = 0.03;

    /// <summary>Builds a normalized anchor map for a finalized rendition.</summary>
    public IReadOnlyList<DocumentRenditionAnchor> Build(DocumentEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var page = GetPageSize(document);
        var margins = document.PageSettings.Margins;
        var context = new BuildContext(page.Width, page.Height, margins);

        foreach (var headerFooter in document.HeadersFooters)
        {
            AddHeaderFooterAnchors(headerFooter, context);
        }

        var y = margins.Top / page.Height;
        AddBlockAnchors(document.Blocks.OrderBy(block => block.Order), context, DocumentRenditionAnchorScope.Body, 1, y);

        foreach (var anchor in document.Anchors)
        {
            var mapped = MapExplicitAnchor(anchor, context);
            if (mapped is not null)
            {
                if (IsImplicitDuplicate(anchor, mapped, context.Anchors))
                {
                    continue;
                }

                context.Anchors.Add(mapped);
            }
        }

        return context.Anchors
            .OrderBy(anchor => anchor.PageNumber)
            .ThenBy(anchor => anchor.Y)
            .ThenBy(anchor => anchor.X)
            .ToList();
    }

    /// <summary>Converts a rendition anchor to a signing-field area.</summary>
    public SigningFieldArea ToSigningFieldArea(DocumentRenditionAnchor anchor, string? attachmentUuid = null)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        return new SigningFieldArea
        {
            Uuid = anchor.Id,
            AttachmentUuid = attachmentUuid,
            Page = Math.Max(0, anchor.PageNumber - 1),
            X = Clamp01(anchor.X),
            Y = Clamp01(anchor.Y),
            Width = Clamp01(anchor.Width),
            Height = Clamp01(anchor.Height)
        };
    }

    /// <summary>Converts a signing placeholder anchor to a signing field definition.</summary>
    public SigningField ToSigningField(DocumentRenditionAnchor anchor, string? attachmentUuid = null)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        var placeholder = anchor.SigningPlaceholder;
        return new SigningField
        {
            Uuid = anchor.Id,
            SubmitterUuid = placeholder?.SubmitterUuid,
            Name = placeholder?.Label ?? anchor.Key,
            Type = placeholder?.FieldType ?? (anchor.Type == DocumentRenditionAnchorType.Token ? SigningFieldType.Text : SigningFieldType.Signature),
            Required = placeholder?.Required ?? true,
            Areas = [ToSigningFieldArea(anchor, attachmentUuid)]
        };
    }

    private static void AddHeaderFooterAnchors(DocumentHeaderFooter headerFooter, BuildContext context)
    {
        var scope = headerFooter.Type == DocumentHeaderFooterType.Footer
            ? DocumentRenditionAnchorScope.Footer
            : DocumentRenditionAnchorScope.Header;
        var y = scope == DocumentRenditionAnchorScope.Footer ? 0.92 : 0.035;
        var x = context.MarginLeftNormalized;
        var width = context.ContentWidthNormalized;
        var height = DefaultAnchorHeight;

        foreach (var block in headerFooter.Blocks.OrderBy(block => block.Order))
        {
            context.Blocks[block.Id] = new LayoutBox(
                1,
                x,
                y,
                width,
                Math.Max(height, DefaultLineHeight / context.PageHeight),
                scope,
                block.SectionId,
                null,
                headerFooter.Id,
                1,
                1);

            AddInlineTokenAnchors(block, context, scope, y);
        }
    }

    private static double AddBlockAnchors(
        IEnumerable<DocumentBlock> blocks,
        BuildContext context,
        DocumentRenditionAnchorScope scope,
        int pageNumber,
        double startY)
    {
        var y = startY;
        foreach (var block in blocks)
        {
            if (block.Content is PageBreakBlockContent)
            {
                pageNumber++;
                y = context.MarginTopNormalized;
                continue;
            }

            if (block.Content is TableBlockContent table)
            {
                y = AddTableAnchors(block, table, context, scope, pageNumber, y);
                continue;
            }

            var height = block.Content is ImageBlockContent image && image.Size.Height is > 0
                ? Math.Max(DefaultLineHeight, image.Size.Height.Value)
                : DefaultLineHeight;

            var box = new LayoutBox(
                pageNumber,
                context.MarginLeftNormalized,
                y,
                context.ContentWidthNormalized,
                height / context.PageHeight,
                scope,
                block.SectionId,
                null,
                null,
                1,
                1);
            context.Blocks[block.Id] = box;

            if (block.Content is ImageBlockContent { FloatingLayout: not null } floatingImage)
            {
                context.FloatingBlocks[block.Id] = MapFloatingBox(floatingImage.FloatingLayout, context, block.SectionId);
            }

            AddInlineTokenAnchors(block, context, scope, y);
            y += Math.Max(box.Height, DefaultLineHeight / context.PageHeight);
        }

        return y;
    }

    private static double AddTableAnchors(
        DocumentBlock tableBlock,
        TableBlockContent table,
        BuildContext context,
        DocumentRenditionAnchorScope scope,
        int pageNumber,
        double y)
    {
        var rows = table.Rows;
        var tableWidth = context.ContentWidthNormalized;
        var totalColumns = rows.Count == 0
            ? 1
            : rows.Max(row => row.Cells.Sum(cell => Math.Max(1, cell.ColumnSpan)));
        var rowHeight = DefaultTableRowHeight / context.PageHeight;
        var tableHeight = Math.Max(rowHeight, rowHeight * Math.Max(1, rows.Count));
        context.Blocks[tableBlock.Id] = new LayoutBox(
            pageNumber,
            context.MarginLeftNormalized,
            y,
            tableWidth,
            tableHeight,
            scope,
            tableBlock.SectionId,
            null,
            null,
            1,
            1);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var column = 0;
            foreach (var cell in row.Cells)
            {
                var columnSpan = Math.Max(1, cell.ColumnSpan);
                var rowSpan = Math.Max(1, cell.RowSpan);
                var cellBox = new LayoutBox(
                    pageNumber,
                    context.MarginLeftNormalized + (tableWidth * column / totalColumns),
                    y + (rowIndex * rowHeight),
                    tableWidth * columnSpan / totalColumns,
                    rowHeight * rowSpan,
                    scope,
                    tableBlock.SectionId,
                    cell.Id,
                    null,
                    columnSpan,
                    rowSpan);
                context.Cells[cell.Id] = cellBox;

                AddBlockAnchors(cell.Blocks.OrderBy(block => block.Order), context, scope, pageNumber, cellBox.Y);
                foreach (var nestedBlock in cell.Blocks)
                {
                    if (context.Blocks.TryGetValue(nestedBlock.Id, out var nestedBox))
                    {
                        context.Blocks[nestedBlock.Id] = nestedBox with
                        {
                            X = cellBox.X,
                            Y = cellBox.Y,
                            Width = cellBox.Width,
                            Height = cellBox.Height,
                            CellId = cell.Id,
                            ColumnSpan = columnSpan,
                            RowSpan = rowSpan
                        };
                    }
                }

                column += columnSpan;
            }
        }

        return y + tableHeight;
    }

    private static void AddInlineTokenAnchors(
        DocumentBlock block,
        BuildContext context,
        DocumentRenditionAnchorScope scope,
        double y)
    {
        var inlines = GetInlines(block.Content);
        if (inlines is null || !context.Blocks.TryGetValue(block.Id, out var box))
        {
            return;
        }

        for (var index = 0; index < inlines.Count; index++)
        {
            if (inlines[index] is not TokenRun token || string.IsNullOrWhiteSpace(token.Key))
            {
                continue;
            }

            var width = DefaultAnchorWidth;
            context.Anchors.Add(new DocumentRenditionAnchor
            {
                Type = DocumentRenditionAnchorType.Token,
                Key = token.Key,
                PageNumber = box.PageNumber,
                X = Clamp01(box.X + Math.Min(Math.Max(0, box.Width - width), index * 0.04)),
                Y = Clamp01(y),
                Width = width,
                Height = DefaultAnchorHeight,
                Scope = scope,
                SectionId = box.SectionId,
                SourceBlockId = block.Id,
                SourceCellId = box.CellId,
                HeaderFooterId = box.HeaderFooterId,
                ColumnSpan = box.ColumnSpan,
                RowSpan = box.RowSpan
            });
        }
    }

    private static DocumentRenditionAnchor? MapExplicitAnchor(DocumentAnchor anchor, BuildContext context)
    {
        var type = anchor.Type switch
        {
            DocumentAnchorType.SigningPlaceholder => DocumentRenditionAnchorType.Placeholder,
            DocumentAnchorType.Token => DocumentRenditionAnchorType.Token,
            DocumentAnchorType.FloatingObject => DocumentRenditionAnchorType.SigningField,
            _ => DocumentRenditionAnchorType.Placeholder
        };

        var source = ResolveSourceBox(anchor, context);
        if (source is null)
        {
            return null;
        }

        var placeholder = anchor.SigningPlaceholder;
        var width = source.Value.CellId is not null
            ? source.Value.Width
            : placeholder?.Width > 0 ? placeholder.Width : Math.Min(source.Value.Width, DefaultAnchorWidth);
        var height = source.Value.CellId is not null
            ? source.Value.Height
            : placeholder?.Height > 0 ? placeholder.Height : Math.Min(source.Value.Height, DefaultAnchorHeight);

        return new DocumentRenditionAnchor
        {
            Id = anchor.Id,
            Type = type,
            Key = string.IsNullOrWhiteSpace(placeholder?.Key) ? anchor.Key : placeholder.Key,
            PageNumber = source.Value.PageNumber,
            X = Clamp01(source.Value.X),
            Y = Clamp01(source.Value.Y),
            Width = Clamp01(Math.Max(width, 0.001)),
            Height = Clamp01(Math.Max(height, 0.001)),
            Scope = anchor.FloatingLayout is not null ? DocumentRenditionAnchorScope.FloatingObject : source.Value.Scope,
            SectionId = source.Value.SectionId,
            SourceBlockId = anchor.BlockId,
            SourceCellId = source.Value.CellId ?? anchor.TableCellId,
            HeaderFooterId = anchor.HeaderFooterId ?? source.Value.HeaderFooterId,
            ColumnSpan = source.Value.ColumnSpan,
            RowSpan = source.Value.RowSpan,
            SigningPlaceholder = placeholder
        };
    }

    private static bool IsImplicitDuplicate(
        DocumentAnchor sourceAnchor,
        DocumentRenditionAnchor mappedAnchor,
        IReadOnlyCollection<DocumentRenditionAnchor> existingAnchors)
    {
        if (!string.IsNullOrWhiteSpace(sourceAnchor.BlockId)
            || !string.IsNullOrWhiteSpace(sourceAnchor.TableCellId)
            || !string.IsNullOrWhiteSpace(sourceAnchor.HeaderFooterId)
            || sourceAnchor.FloatingLayout is not null
            || sourceAnchor.SigningPlaceholder is not null)
        {
            return false;
        }

        return existingAnchors.Any(existing =>
            existing.Type == mappedAnchor.Type
            && string.Equals(existing.Key, mappedAnchor.Key, StringComparison.Ordinal)
            && existing.PageNumber == mappedAnchor.PageNumber);
    }

    private static LayoutBox? ResolveSourceBox(DocumentAnchor anchor, BuildContext context)
    {
        if (anchor.FloatingLayout is not null)
        {
            return MapFloatingBox(anchor.FloatingLayout, context, null);
        }

        if (!string.IsNullOrWhiteSpace(anchor.TableCellId) && context.Cells.TryGetValue(anchor.TableCellId, out var cellBox))
        {
            return cellBox;
        }

        if (!string.IsNullOrWhiteSpace(anchor.BlockId) && context.Blocks.TryGetValue(anchor.BlockId, out var blockBox))
        {
            return blockBox;
        }

        if (!string.IsNullOrWhiteSpace(anchor.BlockId) && context.FloatingBlocks.TryGetValue(anchor.BlockId, out var floatingBox))
        {
            return floatingBox;
        }

        return new LayoutBox(
            1,
            context.MarginLeftNormalized,
            context.MarginTopNormalized,
            DefaultAnchorWidth,
            DefaultAnchorHeight,
            anchor.Scope,
            null,
            null,
            anchor.HeaderFooterId,
            1,
            1);
    }

    private static LayoutBox MapFloatingBox(DocumentFloatingLayout layout, BuildContext context, string? sectionId)
    {
        return new LayoutBox(
            1,
            NormalizePosition(layout.X, context.PageWidth),
            NormalizePosition(layout.Y, context.PageHeight),
            DefaultAnchorWidth,
            DefaultAnchorHeight,
            DocumentRenditionAnchorScope.FloatingObject,
            sectionId,
            null,
            null,
            1,
            1);
    }

    private static List<InlineContent>? GetInlines(DocumentBlockContent content)
    {
        return content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => null
        };
    }

    private static DocumentPageSize GetPageSize(DocumentEditorDocument document)
    {
        var size = document.PageSettings.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            size = DocumentPageSize.A4;
        }

        return document.PageSettings.Landscape
            ? new DocumentPageSize { Name = size.Name, Width = size.Height, Height = size.Width }
            : size;
    }

    private static double NormalizePosition(double value, double denominator)
    {
        return Clamp01(value is >= 0 and <= 1 ? value : value / denominator);
    }

    private static double Clamp01(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Max(0, Math.Min(1, value));
    }

    private sealed class BuildContext
    {
        public BuildContext(double pageWidth, double pageHeight, DocumentPageMargins margins)
        {
            PageWidth = pageWidth;
            PageHeight = pageHeight;
            MarginLeftNormalized = margins.Left / pageWidth;
            MarginTopNormalized = margins.Top / pageHeight;
            ContentWidthNormalized = Math.Max(0.1, (pageWidth - margins.Left - margins.Right) / pageWidth);
        }

        public double PageWidth { get; }

        public double PageHeight { get; }

        public double MarginLeftNormalized { get; }

        public double MarginTopNormalized { get; }

        public double ContentWidthNormalized { get; }

        public List<DocumentRenditionAnchor> Anchors { get; } = [];

        public Dictionary<string, LayoutBox> Blocks { get; } = [];

        public Dictionary<string, LayoutBox> Cells { get; } = [];

        public Dictionary<string, LayoutBox> FloatingBlocks { get; } = [];
    }

    private readonly record struct LayoutBox(
        int PageNumber,
        double X,
        double Y,
        double Width,
        double Height,
        DocumentRenditionAnchorScope Scope,
        string? SectionId,
        string? CellId,
        string? HeaderFooterId,
        int ColumnSpan,
        int RowSpan);
}
