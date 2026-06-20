namespace Tempo.Blazor.NotionEditor.Models;

public static class NotionTableGridValidator
{
    public static bool TryValidate(IReadOnlyList<IReadOnlyList<NotionTableCell>>? rows, int columnCount, out IReadOnlyList<string> errors)
    {
        var found = new List<string>();

        if (rows is null)
        {
            found.Add("Rows cannot be null.");
            errors = found;
            return false;
        }

        if (columnCount < 0)
        {
            found.Add("Column count cannot be negative.");
            errors = found;
            return false;
        }

        var occupied = new bool[Math.Max(0, rows.Count), Math.Max(0, columnCount)];

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.Count < columnCount)
            {
                found.Add($"Row {rowIndex} has fewer cells than the table column count.");
            }

            for (var columnIndex = 0; columnIndex < Math.Min(row.Count, columnCount); columnIndex++)
            {
                var cell = row[columnIndex];
                var colSpan = Math.Max(1, cell.ColSpan);
                var rowSpan = Math.Max(1, cell.RowSpan);

                if (cell.IsMergeHidden)
                    continue;

                if (columnIndex + colSpan > columnCount)
                {
                    found.Add($"Cell {rowIndex}:{columnIndex} extends beyond the table column count.");
                    continue;
                }

                if (rowIndex + rowSpan > rows.Count)
                {
                    found.Add($"Cell {rowIndex}:{columnIndex} extends beyond the table row count.");
                    continue;
                }

                var overlaps = false;
                for (var r = rowIndex; r < rowIndex + rowSpan; r++)
                {
                    for (var c = columnIndex; c < columnIndex + colSpan; c++)
                    {
                        if (occupied[r, c])
                        {
                            found.Add($"Cell {rowIndex}:{columnIndex} overlaps another merged cell.");
                            overlaps = true;
                            break;
                        }

                        occupied[r, c] = true;
                    }

                    if (overlaps)
                        break;
                }
            }
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            for (var columnIndex = 0; columnIndex < Math.Min(row.Count, columnCount); columnIndex++)
            {
                var cell = row[columnIndex];
                if (!cell.IsMergeHidden)
                    continue;

                if (cell.MergeOriginRow < 0 ||
                    cell.MergeOriginColumn < 0 ||
                    cell.MergeOriginRow >= rows.Count ||
                    cell.MergeOriginColumn >= columnCount ||
                    cell.MergeOriginColumn >= rows[cell.MergeOriginRow].Count)
                {
                    found.Add($"Hidden cell {rowIndex}:{columnIndex} has no covering merged cell.");
                    continue;
                }

                var origin = rows[cell.MergeOriginRow][cell.MergeOriginColumn];
                var covered = !origin.IsMergeHidden &&
                    rowIndex >= cell.MergeOriginRow &&
                    rowIndex < cell.MergeOriginRow + Math.Max(1, origin.RowSpan) &&
                    columnIndex >= cell.MergeOriginColumn &&
                    columnIndex < cell.MergeOriginColumn + Math.Max(1, origin.ColSpan);

                if (!covered)
                {
                    found.Add($"Hidden cell {rowIndex}:{columnIndex} has no covering merged cell.");
                }
            }
        }

        errors = found;
        return found.Count == 0;
    }
}
