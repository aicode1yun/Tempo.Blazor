namespace Tempo.Blazor.Components.Spreadsheet.Enums;

/// <summary>
/// Specifies the horizontal alignment of cell content.
/// </summary>
public enum SpreadsheetHorizontalAlign
{
    /// <summary>Auto-detect alignment based on cell content type (numbers right, text left, booleans centered).</summary>
    General = 0,

    /// <summary>Align content to the left.</summary>
    Left = 1,

    /// <summary>Center content horizontally.</summary>
    Center = 2,

    /// <summary>Align content to the right.</summary>
    Right = 3,

    /// <summary>Justify content to fill the cell width.</summary>
    Justify = 4
}
