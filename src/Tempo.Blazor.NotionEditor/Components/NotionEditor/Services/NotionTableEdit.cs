namespace Tempo.Blazor.Components.NotionEditor.Services;

using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.NotionEditor.Models;
using TableBlockContent = Tempo.Blazor.NotionEditor.Models.TableBlockContent;

/// <summary>
/// Column and row edits on a table block. Rebuilding a row from its plain <c>Cells</c> alone would
/// silently drop the rich cells that carry merges and background colours, and rebuilding the table
/// from its header flags alone would drop the per-column alignment.
/// </summary>
internal static class NotionTableEdit
{
    /// <summary>Appends an empty cell to the row, keeping its rich cells aligned with it.</summary>
    public static TableRowBlockContent AddColumn(ITableRowBlockContent row)
    {
        var rich = row.RichCells.ToList();
        rich.Add(new NotionTableCell());

        return new TableRowBlockContent { RichCells = rich };
    }

    /// <summary>Removes one column from the row, from both the plain and the rich cells.</summary>
    public static TableRowBlockContent RemoveColumn(ITableRowBlockContent row, int columnIndex)
    {
        var rich = row.RichCells.ToList();
        if (columnIndex >= 0 && columnIndex < rich.Count) rich.RemoveAt(columnIndex);

        return new TableRowBlockContent { RichCells = rich };
    }

    /// <summary>Clones the table with one more column; the new column has no explicit alignment.</summary>
    public static TableBlockContent AddColumn(ITableBlockContent table)
    {
        var alignments = table.ColumnAlignments.ToList();
        if (alignments.Count > 0) alignments.Add(TableColumnAlignment.None);
        var widths = table.ColumnWidths.ToList();
        if (widths.Count > 0) widths.Add(null);

        return new TableBlockContent
        {
            HasHeaderRow     = table.HasHeaderRow,
            HasHeaderColumn  = table.HasHeaderColumn,
            ColumnCount      = table.ColumnCount + 1,
            ColumnAlignments = alignments,
            ColumnWidths = widths
        };
    }

    /// <summary>Clones the table with one column removed, dropping that column's alignment with it.</summary>
    public static TableBlockContent RemoveColumn(ITableBlockContent table, int columnIndex)
    {
        var alignments = table.ColumnAlignments.ToList();
        if (columnIndex >= 0 && columnIndex < alignments.Count) alignments.RemoveAt(columnIndex);
        var widths = table.ColumnWidths.ToList();
        if (columnIndex >= 0 && columnIndex < widths.Count) widths.RemoveAt(columnIndex);

        return new TableBlockContent
        {
            HasHeaderRow     = table.HasHeaderRow,
            HasHeaderColumn  = table.HasHeaderColumn,
            ColumnCount      = Math.Max(1, table.ColumnCount - 1),
            ColumnAlignments = alignments,
            ColumnWidths = widths
        };
    }
}
