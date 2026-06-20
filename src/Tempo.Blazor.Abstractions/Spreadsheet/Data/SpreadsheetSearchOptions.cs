using Tempo.Blazor.Components.Spreadsheet.Enums;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>
/// Options controlling a spreadsheet find/replace operation, mirroring the OnlyOffice search panel.
/// </summary>
public sealed class SpreadsheetSearchOptions
{
    /// <summary>The text to search for. An empty query yields no hits.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Whether the search is case sensitive.</summary>
    public bool MatchCase { get; set; }

    /// <summary>Whether the entire cell text must equal the query (rather than a substring match).</summary>
    public bool WholeCell { get; set; }

    /// <summary>Whether to search displayed values or formulas.</summary>
    public SpreadsheetSearchIn SearchIn { get; set; } = SpreadsheetSearchIn.Values;

    /// <summary>Whether to search the active sheet only or the whole workbook.</summary>
    public SpreadsheetSearchScope Scope { get; set; } = SpreadsheetSearchScope.Sheet;
}
