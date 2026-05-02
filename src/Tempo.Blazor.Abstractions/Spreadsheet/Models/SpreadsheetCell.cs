using Tempo.Blazor.Components.Spreadsheet.Enums;

namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>
/// Represents a single cell within a spreadsheet sheet.
/// </summary>
public sealed class SpreadsheetCell
{
    /// <summary>The raw value stored in the cell.</summary>
    public object? Value { get; set; }

    /// <summary>The formula expression (e.g. =SUM(A1:A10)). Null if the cell has no formula.</summary>
    public string? Formula { get; set; }

    /// <summary>The computed or formatted display value. Null until evaluated.</summary>
    public string? DisplayValue { get; set; }

    /// <summary>The visual style of the cell.</summary>
    public SpreadsheetCellStyle Style { get; set; } = new();

    /// <summary>The data type of the cell value.</summary>
    public SpreadsheetDataType DataType { get; set; } = SpreadsheetDataType.Text;

    /// <summary>Whether the cell is read-only and cannot be edited.</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>An image URL or base64 data to render inside the cell.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>A hyperlink URL associated with this cell.</summary>
    public string? Hyperlink { get; set; }

    /// <summary>Creates a deep copy of this cell including style.</summary>
    public SpreadsheetCell Clone() => new()
    {
        Value = Value,
        Formula = Formula,
        DisplayValue = DisplayValue,
        Style = Style.Clone(),
        DataType = DataType,
        IsReadOnly = IsReadOnly,
        ImageUrl = ImageUrl,
        Hyperlink = Hyperlink
    };
}
