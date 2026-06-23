namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Represents the current editing session state for a spreadsheet formula editor.
/// </summary>
public sealed class SpreadsheetFormulaEditSession
{
    /// <summary>The current editor text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The current selection start within <see cref="Text"/>.</summary>
    public int SelectionStart { get; set; }

    /// <summary>The current selection end within <see cref="Text"/>.</summary>
    public int SelectionEnd { get; set; }

    /// <summary>Whether the current text is a formula expression.</summary>
    public bool IsFormula { get; set; }

    /// <summary>The active reference token under the caret or selection anchor.</summary>
    public SpreadsheetFormulaReferenceToken? ActiveReferenceToken { get; set; }

    /// <summary>All parsed reference tokens from the current text.</summary>
    public IReadOnlyList<SpreadsheetFormulaReferenceToken> ReferenceTokens { get; set; } = [];

    /// <summary>Whether the editor is currently in reference-picking mode.</summary>
    public bool IsReferencePickingMode { get; set; }

    /// <summary>The currently typed function name fragment, if any.</summary>
    public string? FunctionPrefix { get; set; }

    /// <summary>The start index of the current function name fragment.</summary>
    public int FunctionPrefixStart { get; set; } = -1;

    /// <summary>The end index of the current function name fragment.</summary>
    public int FunctionPrefixEnd { get; set; } = -1;

    /// <summary>The available autocomplete suggestions for the current function fragment.</summary>
    public IReadOnlyList<SpreadsheetFormulaFunctionMetadata> Suggestions { get; set; } = [];

    /// <summary>The currently selected suggestion index.</summary>
    public int SelectedSuggestionIndex { get; set; }

    /// <summary>The active function call hint under the caret, if any.</summary>
    public SpreadsheetFormulaFunctionHint? ActiveFunctionHint { get; set; }
}
