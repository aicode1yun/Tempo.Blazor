namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>
/// Represents metadata for a single row in a spreadsheet sheet.
/// </summary>
public sealed class SpreadsheetRow
{
    /// <summary>The zero-based row index.</summary>
    public int Index { get; set; }

    /// <summary>The row height in pixels. Null means use the sheet default.</summary>
    public double? Height { get; set; }

    /// <summary>Whether the row is hidden.</summary>
    public bool IsHidden { get; set; }
}
