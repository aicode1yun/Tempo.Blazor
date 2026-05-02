using Tempo.Blazor.Components.Spreadsheet.Enums;

namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>
/// Represents the border style of a single edge of a spreadsheet cell.
/// </summary>
public sealed class SpreadsheetBorder
{
    /// <summary>The line style of the border. Defaults to <see cref="SpreadsheetBorderStyle.None"/>.</summary>
    public SpreadsheetBorderStyle Style { get; set; } = SpreadsheetBorderStyle.None;

    /// <summary>The color of the border in hex format (e.g. #000000). Defaults to black.</summary>
    public string Color { get; set; } = "#000000";

    public SpreadsheetBorder() { }

    public SpreadsheetBorder(SpreadsheetBorderStyle style, string color)
    {
        Style = style;
        Color = color;
    }
}
