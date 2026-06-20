using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Sets the value and/or formula of a single cell and supports undo. Optionally applies a typed
/// data type and an implied number format (the latter only when the cell still uses the
/// <c>General</c> format), as produced by <see cref="Format.SpreadsheetValueParser"/>.
/// </summary>
public sealed class SetCellValueCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly string _cellRef;
    private readonly object? _newValue;
    private readonly string? _newFormula;
    private readonly SpreadsheetCellStyle? _newStyle;
    private readonly SpreadsheetDataType? _newDataType;
    private readonly string? _impliedNumberFormat;

    private object? _oldValue;
    private string? _oldFormula;
    private SpreadsheetCellStyle? _oldStyle;
    private SpreadsheetDataType _oldDataType;
    private bool _cellExisted;

    public SetCellValueCommand(
        SpreadsheetSheet sheet,
        string cellRef,
        object? newValue,
        string? newFormula,
        SpreadsheetCellStyle? newStyle = null,
        SpreadsheetDataType? dataType = null,
        string? impliedNumberFormat = null)
    {
        _sheet = sheet;
        _cellRef = cellRef;
        _newValue = newValue;
        _newFormula = newFormula;
        _newStyle = newStyle;
        _newDataType = dataType;
        _impliedNumberFormat = impliedNumberFormat;
    }

    public void Execute()
    {
        _cellExisted = _sheet.Cells.TryGetValue(_cellRef, out var existing);
        if (_cellExisted)
        {
            _oldValue = existing!.Value;
            _oldFormula = existing.Formula;
            _oldStyle = existing.Style.Clone();
            _oldDataType = existing.DataType;
        }

        var cell = _sheet.GetOrCreateCell(_cellRef);
        if (_newValue is not null)
        {
            cell.Value = _newValue;
            cell.Formula = null;
            cell.DataType = _newDataType ?? SpreadsheetDataType.Text;
            _sheet.UpdateDependencies(_cellRef);
        }
        else if (_newFormula is not null)
        {
            cell.Formula = _newFormula;
            cell.Value = null;
            _sheet.UpdateDependencies(_cellRef);
            _sheet.EvaluateFormula(_cellRef);
        }

        if (_newStyle is not null)
        {
            cell.Style = _newStyle.Clone();
        }

        // Apply the implied number format only when the cell still uses the General format,
        // so an explicit user-chosen format is never overwritten.
        if (_impliedNumberFormat is not null && IsGeneral(cell.Style.NumberFormat))
        {
            cell.Style.NumberFormat = _impliedNumberFormat;
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
        cell.DataType = _oldDataType;
        _sheet.UpdateDependencies(_cellRef);
        cell.Style = _oldStyle?.Clone() ?? new SpreadsheetCellStyle();
        cell.DisplayValue = null;
        _sheet.RecalculateDependents(_cellRef);
    }

    private static bool IsGeneral(string? format)
        => string.IsNullOrEmpty(format) || format.Equals("General", StringComparison.OrdinalIgnoreCase);
}
