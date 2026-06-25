using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Format;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Replaces matched text within a single cell and supports undo. The cell's searchable text
/// (displayed value or formula, per <see cref="SpreadsheetSearchOptions.SearchIn"/>) is rewritten
/// and re-parsed through <see cref="SpreadsheetValueParser"/>. In <see cref="SpreadsheetSearchIn.Values"/>
/// mode formula cells are skipped so their formulas are never destroyed.
/// </summary>
public sealed class ReplaceCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly string _cellRef;
    private readonly SpreadsheetSearchOptions _options;
    private readonly string _replacement;
    private readonly CultureInfo _culture;
    private readonly bool _allInCell;

    private SetCellValueCommand? _inner;

    /// <summary>Whether the last <see cref="Execute"/> actually changed the cell.</summary>
    public bool DidReplace { get; private set; }

    public ReplaceCommand(
        SpreadsheetSheet sheet,
        string cellRef,
        SpreadsheetSearchOptions options,
        string replacement,
        CultureInfo culture,
        bool allInCell = false)
    {
        _sheet = sheet;
        _cellRef = cellRef;
        _options = options;
        _replacement = replacement ?? string.Empty;
        _culture = culture;
        _allInCell = allInCell;
    }

    public void Execute()
    {
        DidReplace = false;
        if (!_sheet.Cells.TryGetValue(_cellRef, out var cell))
            return;

        // Never overwrite a formula via a values-mode replace.
        if (_options.SearchIn == SpreadsheetSearchIn.Values && !string.IsNullOrEmpty(cell.Formula))
            return;

        var text = SpreadsheetSearchEngine.GetSearchableText(cell, _options.SearchIn, _culture);
        if (!SpreadsheetSearchEngine.TryReplace(text, _options, _replacement, _allInCell, out var replaced))
            return;

        if (string.Equals(text, replaced, StringComparison.Ordinal))
            return;

        var parsed = SpreadsheetValueParser.Parse(replaced, _culture);
        _inner = parsed.Formula is not null
            ? new SetCellValueCommand(_sheet, _cellRef, null, parsed.Formula)
            : new SetCellValueCommand(_sheet, _cellRef, parsed.Value, null,
                dataType: parsed.Type, impliedNumberFormat: parsed.ImpliedNumberFormat);
        _inner.Execute();
        DidReplace = true;
    }

    public void Undo()
    {
        _inner?.Undo();
    }
}
