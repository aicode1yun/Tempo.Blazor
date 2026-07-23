using System.Net;
using System.Text;
using System.Text.Json;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using TableColumnAlignment = Tempo.Blazor.DocumentEditor.Models.TableColumnAlignment;

namespace Tempo.Blazor.Components.NotionEditor.Services;

internal static class NotionCanonicalTableBridge
{
    public static (PageBlock Table, List<IPageBlock> Rows) ToView(
        NotionPageSnapshot snapshot,
        Guid tableId)
    {
        var tableBlock = snapshot.Blocks.Single(block => block.Id == tableId);
        var table = tableBlock.Content.Deserialize<NotionAuthoringTable>(
            NotionAggregateJson.Options) ?? throw new InvalidDataException("Table content is missing.");
        var rowBlocks = snapshot.Blocks
            .Where(block => block.ParentBlockId == tableId && block.Type == BlockType.TableRow)
            .OrderBy(block => block.Order)
            .ToList();
        var logicalRows = rowBlocks.Select(block =>
                block.Content.Deserialize<NotionAuthoringTableRow>(NotionAggregateJson.Options)
                ?? throw new InvalidDataException("Table row content is missing."))
            .ToList();
        if (!NotionTableGridProjector.TryProject(
                logicalRows,
                table.ColumnCount,
                "$.rows",
                out var projection,
                out var issues))
        {
            throw new InvalidDataException(string.Join(
                Environment.NewLine,
                issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        }

        var rows = new List<IPageBlock>(logicalRows.Count);
        for (var rowIndex = 0; rowIndex < logicalRows.Count; rowIndex++)
        {
            var richCells = new List<NotionTableCell>(table.ColumnCount);
            for (var columnIndex = 0; columnIndex < table.ColumnCount; columnIndex++)
            {
                var slot = projection!.GetSlot(rowIndex, columnIndex);
                richCells.Add(slot.IsOrigin
                    ? ToViewCell(slot.Cell)
                    : new NotionTableCell
                    {
                        IsMergeHidden = true,
                        MergeOriginRow = slot.OriginRow,
                        MergeOriginColumn = slot.OriginColumn
                    });
            }

            rows.Add(new PageBlock
            {
                Id = rowBlocks[rowIndex].Id,
                PageId = rowBlocks[rowIndex].PageId,
                ParentBlockId = rowBlocks[rowIndex].ParentBlockId,
                Type = BlockType.TableRow,
                Order = rowBlocks[rowIndex].Order,
                CreatedAt = rowBlocks[rowIndex].CreatedAt,
                LastEditedAt = rowBlocks[rowIndex].LastEditedAt,
                Content = new TableRowBlockContent { RichCells = richCells }
            });
        }

        return (new PageBlock
        {
            Id = tableBlock.Id,
            PageId = tableBlock.PageId,
            ParentBlockId = tableBlock.ParentBlockId,
            Type = tableBlock.Type,
            Order = tableBlock.Order,
            CreatedAt = tableBlock.CreatedAt,
            LastEditedAt = tableBlock.LastEditedAt,
            Content = new TableBlockContent
            {
                ColumnCount = table.ColumnCount,
                HasHeaderRow = table.HasHeaderRow,
                HasHeaderColumn = table.HasHeaderColumn,
                ColumnAlignments = table.ColumnAlignments.Select(ToViewAlignment).ToList(),
                ColumnWidths = table.ColumnWidths
            }
        }, rows);
    }

    public static NotionPageSnapshot ReplaceTable(
        NotionPageSnapshot snapshot,
        IPageBlock table,
        IReadOnlyList<IPageBlock> rows)
    {
        var blocks = snapshot.Blocks.ToDictionary(block => block.Id);
        var tableContent = (ITableBlockContent)table.Content;
        blocks[table.Id].Content = JsonSerializer.SerializeToElement(
            new NotionAuthoringTable
            {
                ColumnCount = tableContent.ColumnCount,
                HasHeaderRow = tableContent.HasHeaderRow,
                HasHeaderColumn = tableContent.HasHeaderColumn,
                ColumnAlignments = tableContent.ColumnAlignments
                    .Select(ToCanonicalAlignment)
                    .ToList(),
                ColumnWidths = tableContent.ColumnWidths
            },
            NotionAggregateJson.Options);

        var rowIds = rows.Select(row => row.Id).ToHashSet();
        var retained = snapshot.Blocks
            .Where(block =>
                block.ParentBlockId != table.Id ||
                block.Type != BlockType.TableRow ||
                rowIds.Contains(block.Id))
            .ToList();
        foreach (var row in rows)
        {
            var canonical = new NotionAuthoringTableRow
            {
                Cells = ((ITableRowBlockContent)row.Content).RichCells
                    .Where(cell => !cell.IsMergeHidden)
                    .Select(ToCanonicalCell)
                    .ToList()
            };
            if (blocks.TryGetValue(row.Id, out var existing))
            {
                existing.Order = row.Order;
                existing.Content = JsonSerializer.SerializeToElement(
                    canonical,
                    NotionAggregateJson.Options);
            }
            else
            {
                retained.Add(new NotionBlockSnapshot
                {
                    Id = row.Id,
                    PageId = row.PageId,
                    ParentBlockId = table.Id,
                    Type = BlockType.TableRow,
                    Order = row.Order,
                    CreatedAt = row.CreatedAt,
                    LastEditedAt = row.LastEditedAt,
                    Content = JsonSerializer.SerializeToElement(
                        canonical,
                        NotionAggregateJson.Options)
                });
            }
        }

        snapshot.Blocks = retained.OrderBy(block => block.ParentBlockId)
            .ThenBy(block => block.Order)
            .ThenBy(block => block.Id)
            .ToList();
        return snapshot;
    }

    private static NotionTableCell ToViewCell(NotionAuthoringTableCell cell)
        => new()
        {
            Html = cell.Html,
            DisplayHtml = cell.Inlines.Count > 0 ? RenderInlines(cell.Inlines) : null,
            Inlines = cell.Inlines,
            ColSpan = cell.ColumnSpan,
            RowSpan = cell.RowSpan,
            BackgroundColor = cell.BackgroundColor,
            TextColor = cell.TextColor,
            HorizontalAlignment = cell.HorizontalAlignment,
            VerticalAlignment = cell.VerticalAlignment,
            Width = cell.Width,
            Borders = cell.Borders
        };

    internal static NotionAuthoringTable ToCanonicalTable(ITableBlockContent table)
        => new()
        {
            ColumnCount = table.ColumnCount,
            HasHeaderRow = table.HasHeaderRow,
            HasHeaderColumn = table.HasHeaderColumn,
            ColumnAlignments = table.ColumnAlignments
                .Select(ToCanonicalAlignment)
                .ToList(),
            ColumnWidths = table.ColumnWidths
        };

    internal static NotionAuthoringTableRow ToCanonicalRow(ITableRowBlockContent row)
        => new()
        {
            Cells = row.RichCells
                .Where(cell => !cell.IsMergeHidden)
                .Select(ToCanonicalCell)
                .ToList()
        };

    private static NotionAuthoringTableCell ToCanonicalCell(NotionTableCell cell)
        => new()
        {
            Html = cell.Html,
            Inlines = cell.Inlines,
            ColumnSpan = cell.ColSpan,
            RowSpan = cell.RowSpan,
            BackgroundColor = cell.BackgroundColor,
            TextColor = cell.TextColor,
            HorizontalAlignment = cell.HorizontalAlignment,
            VerticalAlignment = cell.VerticalAlignment,
            Width = cell.Width,
            Borders = cell.Borders
        };

    private static string RenderInlines(IReadOnlyList<NotionRichTextInline> inlines)
    {
        var html = new StringBuilder();
        foreach (var inline in inlines)
        {
            var value = WebUtility.HtmlEncode(inline.Text);
            var styles = new List<string>(2);
            if (NotionCssNormalizer.TryNormalizeColor(inline.TextColor, out var textColor) &&
                textColor is not null)
            {
                styles.Add($"color:{textColor}");
            }
            if (NotionCssNormalizer.TryNormalizeColor(inline.BackgroundColor, out var backgroundColor) &&
                backgroundColor is not null)
            {
                styles.Add($"background-color:{backgroundColor}");
            }
            if (styles.Count > 0)
            {
                value = $"<span style=\"{string.Join(';', styles)}\">{value}</span>";
            }
            if (inline.Code) value = $"<code>{value}</code>";
            if (inline.Strikethrough) value = $"<s>{value}</s>";
            if (inline.Underline) value = $"<u>{value}</u>";
            if (inline.Italic) value = $"<em>{value}</em>";
            if (inline.Bold) value = $"<strong>{value}</strong>";
            if (!string.IsNullOrWhiteSpace(inline.Href) &&
                NotionHtmlSanitizer.IsSafeHref(inline.Href))
            {
                value = $"<a href=\"{WebUtility.HtmlEncode(inline.Href)}\">{value}</a>";
            }
            html.Append(value);
        }
        return NotionHtmlSanitizer.SanitizeBlockContent(html.ToString());
    }

    private static TableColumnAlignment ToViewAlignment(
        NotionTableHorizontalAlignment alignment)
        => alignment switch
        {
            NotionTableHorizontalAlignment.Center => TableColumnAlignment.Center,
            NotionTableHorizontalAlignment.Right => TableColumnAlignment.Right,
            _ => TableColumnAlignment.Left
        };

    private static NotionTableHorizontalAlignment ToCanonicalAlignment(
        TableColumnAlignment alignment)
        => alignment switch
        {
            TableColumnAlignment.Center => NotionTableHorizontalAlignment.Center,
            TableColumnAlignment.Right => NotionTableHorizontalAlignment.Right,
            _ => NotionTableHorizontalAlignment.Left
        };
}
