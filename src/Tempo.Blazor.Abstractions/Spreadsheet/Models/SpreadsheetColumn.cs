namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>
/// Represents metadata for a single column in a spreadsheet sheet.
/// </summary>
public sealed class SpreadsheetColumn
{
    /// <summary>The zero-based column index.</summary>
    public int Index { get; set; }

    /// <summary>The column width in pixels. Null means use the sheet default.</summary>
    public double? Width { get; set; }

    /// <summary>Whether the column is hidden.</summary>
    public bool IsHidden { get; set; }
}
