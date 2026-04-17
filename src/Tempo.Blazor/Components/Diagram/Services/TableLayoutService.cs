using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Provides table cell manipulation helpers for diagram table nodes.</summary>
public static class TableLayoutService
{
    public static int GetRowCount(DiagramNode node)
    {
        if (node.Data.TryGetValue("rowCount", out var value) && value is not null)
        {
            if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
                return je.GetInt32();
            if (int.TryParse(value.ToString(), out var n)) return n;
        }
        return 0;
    }

    public static int GetColumnCount(DiagramNode node)
    {
        if (node.Data.TryGetValue("columnCount", out var value) && value is not null)
        {
            if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
                return je.GetInt32();
            if (int.TryParse(value.ToString(), out var n)) return n;
        }
        return 0;
    }

    public static List<DiagramTableCellData> GetCells(DiagramNode node)
    {
        if (node.Data.TryGetValue("cells", out var value) && value is not null)
        {
            if (value is List<DiagramTableCellData> list) return list;
            if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return je.EnumerateArray().Select(e => new DiagramTableCellData
                {
                    Row = e.GetProperty("row").GetInt32(),
                    Column = e.GetProperty("column").GetInt32(),
                    RowSpan = e.TryGetProperty("rowSpan", out var rs) ? rs.GetInt32() : 1,
                    ColSpan = e.TryGetProperty("colSpan", out var cs) ? cs.GetInt32() : 1,
                    Text = e.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "",
                    Style = e.TryGetProperty("style", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.Object
                        ? new DiagramTableCellStyle
                        {
                            BackgroundColor = s.TryGetProperty("backgroundColor", out var bc) ? bc.GetString() : null,
                            BorderColor = s.TryGetProperty("borderColor", out var boc) ? boc.GetString() : null,
                            TextAlign = s.TryGetProperty("textAlign", out var ta) ? ta.GetString() : null,
                            FontWeight = s.TryGetProperty("fontWeight", out var fw) ? fw.GetString() : null,
                        }
                        : null
                }).ToList();
            }
        }
        return [];
    }

    public static void SetRowCount(DiagramNode node, int count) => node.Data["rowCount"] = count;
    public static void SetColumnCount(DiagramNode node, int count) => node.Data["columnCount"] = count;
    public static void SetCells(DiagramNode node, List<DiagramTableCellData> cells) => node.Data["cells"] = cells;

    public static void InsertRow(DiagramNode node, int index)
    {
        var rowCount = GetRowCount(node);
        var cells = GetCells(node);
        // Shift cells at or below the insertion point down by one row
        foreach (var cell in cells)
        {
            if (cell.Row >= index)
                cell.Row++;
        }
        SetRowCount(node, rowCount + 1);
        SetCells(node, cells);
    }

    public static void DeleteRow(DiagramNode node, int index)
    {
        var rowCount = GetRowCount(node);
        var cells = GetCells(node);
        // Remove cells that intersect the deleted row
        cells.RemoveAll(c => c.Row == index || (c.Row < index && c.Row + c.RowSpan > index));
        // Shift cells below the deleted row up by one row
        foreach (var cell in cells)
        {
            if (cell.Row > index)
                cell.Row--;
        }
        SetRowCount(node, Math.Max(1, rowCount - 1));
        SetCells(node, cells);
    }

    public static void InsertColumn(DiagramNode node, int index)
    {
        var columnCount = GetColumnCount(node);
        var cells = GetCells(node);
        foreach (var cell in cells)
        {
            if (cell.Column >= index)
                cell.Column++;
        }
        SetColumnCount(node, columnCount + 1);
        SetCells(node, cells);
    }

    public static void DeleteColumn(DiagramNode node, int index)
    {
        var columnCount = GetColumnCount(node);
        var cells = GetCells(node);
        // Remove cells that intersect the deleted column
        cells.RemoveAll(c => c.Column == index || (c.Column < index && c.Column + c.ColSpan > index));
        // Shift cells right of the deleted column left by one column
        foreach (var cell in cells)
        {
            if (cell.Column > index)
                cell.Column--;
        }
        SetColumnCount(node, Math.Max(1, columnCount - 1));
        SetCells(node, cells);
    }

    public static bool CanMerge(DiagramNode node, IEnumerable<(int Row, int Column)> selection)
    {
        var cells = GetCells(node);
        var list = selection.ToList();
        if (list.Count < 2) return false;

        int minRow = list.Min(s => s.Row);
        int maxRow = list.Max(s => s.Row);
        int minCol = list.Min(s => s.Column);
        int maxCol = list.Max(s => s.Column);

        // Selection must form a solid rectangle
        for (int r = minRow; r <= maxRow; r++)
        {
            for (int c = minCol; c <= maxCol; c++)
            {
                if (!list.Any(s => s.Row == r && s.Column == c))
                    return false;
            }
        }

        // No existing merged cell should overlap partially
        foreach (var cell in cells)
        {
            bool cellIntersects = cell.Row < maxRow + 1 && cell.Row + cell.RowSpan > minRow
                               && cell.Column < maxCol + 1 && cell.Column + cell.ColSpan > minCol;
            bool selectionContainsCell = minRow <= cell.Row && maxRow + 1 >= cell.Row + cell.RowSpan
                                      && minCol <= cell.Column && maxCol + 1 >= cell.Column + cell.ColSpan;
            if (cellIntersects && !selectionContainsCell) return false;
        }

        return true;
    }

    public static void MergeCells(DiagramNode node, IEnumerable<(int Row, int Column)> selection)
    {
        var cells = GetCells(node);
        var list = selection.ToList();
        int minRow = list.Min(s => s.Row);
        int maxRow = list.Max(s => s.Row);
        int minCol = list.Min(s => s.Column);
        int maxCol = list.Max(s => s.Column);

        var mergedText = string.Join(" ", list
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Column)
            .Select(s => cells.FirstOrDefault(c => s.Row >= c.Row && s.Row < c.Row + c.RowSpan
                                                && s.Column >= c.Column && s.Column < c.Column + c.ColSpan)?.Text ?? "")
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        cells.RemoveAll(c => list.Any(s => s.Row >= c.Row && s.Row < c.Row + c.RowSpan
                                        && s.Column >= c.Column && s.Column < c.Column + c.ColSpan));

        cells.Add(new DiagramTableCellData
        {
            Row = minRow,
            Column = minCol,
            RowSpan = maxRow - minRow + 1,
            ColSpan = maxCol - minCol + 1,
            Text = mergedText
        });

        SetCells(node, cells);
    }

    public static bool CanSplit(DiagramNode node, int row, int column)
    {
        var cell = GetCells(node).FirstOrDefault(c => c.Row == row && c.Column == column);
        return cell is { RowSpan: > 1 } or { ColSpan: > 1 };
    }

    public static void SplitCell(DiagramNode node, int row, int column)
    {
        var cells = GetCells(node);
        var cell = cells.FirstOrDefault(c => c.Row == row && c.Column == column);
        if (cell is null || (cell.RowSpan <= 1 && cell.ColSpan <= 1)) return;

        cells.Remove(cell);
        for (int r = row; r < row + cell.RowSpan; r++)
        {
            for (int c = column; c < column + cell.ColSpan; c++)
            {
                cells.Add(new DiagramTableCellData
                {
                    Row = r,
                    Column = c,
                    Text = (r == row && c == column) ? cell.Text : ""
                });
            }
        }
        SetCells(node, cells);
    }
}
