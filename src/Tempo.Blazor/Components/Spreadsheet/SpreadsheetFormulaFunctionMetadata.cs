namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Describes a spreadsheet function for autocomplete and inline help.
/// </summary>
public sealed class SpreadsheetFormulaFunctionMetadata
{
    /// <summary>The function name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The display signature shown in inline help.</summary>
    public string Signature { get; init; } = string.Empty;

    /// <summary>A short user-facing description.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>The ordered argument labels used for active argument highlighting.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];
}
