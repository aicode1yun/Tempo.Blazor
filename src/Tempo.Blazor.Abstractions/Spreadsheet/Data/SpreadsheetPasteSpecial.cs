namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>What part of the copied cells a paste-special operation transfers to the target.</summary>
public enum SpreadsheetPasteContent
{
    /// <summary>Everything: values/formulas and full formatting.</summary>
    All,

    /// <summary>Only the resulting values (formulas are flattened to their computed value, no formatting).</summary>
    Values,

    /// <summary>Only the formulas (or values for non-formula cells), no formatting.</summary>
    Formulas,

    /// <summary>Only the formatting (cell values and formulas are left untouched).</summary>
    Formats,

    /// <summary>Values plus formatting, but formulas are flattened to values.</summary>
    ValuesAndFormats,

    /// <summary>Everything except cell borders.</summary>
    AllExceptBorders
}

/// <summary>An arithmetic operation combining the copied value with the existing target value.</summary>
public enum SpreadsheetPasteOperation
{
    /// <summary>No arithmetic; the copied value replaces the target.</summary>
    None,

    /// <summary>target = target + source.</summary>
    Add,

    /// <summary>target = target − source.</summary>
    Subtract,

    /// <summary>target = target × source.</summary>
    Multiply,

    /// <summary>target = target ÷ source.</summary>
    Divide
}

/// <summary>
/// Options for a paste-special operation: which content to transfer, an optional arithmetic operation
/// against the existing target values, whether to skip blank source cells, and whether to transpose
/// the copied block (swap rows and columns).
/// </summary>
public sealed class SpreadsheetPasteSpecialOptions
{
    /// <summary>What part of the copied cells to paste.</summary>
    public SpreadsheetPasteContent Content { get; set; } = SpreadsheetPasteContent.All;

    /// <summary>The arithmetic operation to combine source and target values.</summary>
    public SpreadsheetPasteOperation Operation { get; set; } = SpreadsheetPasteOperation.None;

    /// <summary>When true, blank source cells do not overwrite the target.</summary>
    public bool SkipBlanks { get; set; }

    /// <summary>When true, the copied block is transposed (rows become columns).</summary>
    public bool Transpose { get; set; }
}
