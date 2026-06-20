namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Describes a parsed formula reference token and its position within the editor text.
/// </summary>
public sealed class SpreadsheetFormulaReferenceToken
{
    /// <summary>The token text as written in the formula.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The inclusive token start index.</summary>
    public int Start { get; set; }

    /// <summary>The exclusive token end index.</summary>
    public int End { get; set; }

    /// <summary>The zero-based color slot assigned to the token.</summary>
    public int ColorIndex { get; set; }
}
