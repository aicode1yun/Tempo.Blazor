using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Sets the value and/or formula of a single cell and supports undo.
/// </summary>
public sealed class SetCellValueCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly string _cellRef;
    private readonly object? _newValue;
    private readonly string? _newFormula;
    private readonly SpreadsheetCellStyle? _newStyle;

    private object? _oldValue;
    private string? _oldFormula;
    private SpreadsheetCellStyle? _oldStyle;
    private bool _cellExisted;

    public SetCellValueCommand(SpreadsheetSheet sheet, string cellRef, object? newValue, string? newFormula, SpreadsheetCellStyle? newStyle = null)
    {
        _sheet = sheet;
        _cellRef = cellRef;
        _newValue = newValue;
        _newFormula = newFormula;
        _newStyle = newStyle;
    }

    public void Execute()
    {
        _cellExisted = _sheet.Cells.TryGetValue(_cellRef, out var existing);
        if (_cellExisted)
        {
            _oldValue = existing!.Value;
            _oldFormula = existing.Formula;
            _oldStyle = existing.Style.Clone();
        }

        var cell = _sheet.GetOrCreateCell(_cellRef);
        if (_newValue is not null)
        {
            cell.Value = _newValue;
            cell.Formula = null;
        }
        else if (_newFormula is not null)
        {
            cell.Formula = _newFormula;
            cell.Value = null;
        }

        if (_newStyle is not null)
        {
            cell.Style = _newStyle.Clone();
        }

        cell.DisplayValue = null;
        _sheet.RecalculateDependents(_cellRef);
    }

    public void Undo()
    {
        if (!_cellExisted)
        {
            _sheet.Cells.Remove(_cellRef);
            return;
        }

        var cell = _sheet.GetOrCreateCell(_cellRef);
        cell.Value = _oldValue;
        cell.Formula = _oldFormula;
        cell.Style = _oldStyle?.Clone() ?? new SpreadsheetCellStyle();
        cell.DisplayValue = null;
        _sheet.RecalculateDependents(_cellRef);
    }
}
