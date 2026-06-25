namespace Tempo.Blazor.Components.NotionEditor.Blocks.Table;

public readonly record struct TableCellSelectionRequest(int RowIndex, int ColumnIndex, bool Extend);
