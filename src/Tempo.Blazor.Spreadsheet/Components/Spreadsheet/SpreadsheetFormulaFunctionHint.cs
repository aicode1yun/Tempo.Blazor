namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Represents the currently active function call and argument index under the caret.
/// </summary>
public sealed class SpreadsheetFormulaFunctionHint
{
    /// <summary>The resolved function metadata.</summary>
    public SpreadsheetFormulaFunctionMetadata Function { get; init; } = new();

    /// <summary>The zero-based index of the active argument.</summary>
    public int ActiveArgumentIndex { get; init; }
}
