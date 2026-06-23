using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Formula;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Pastes the internal clipboard into a target range with paste-special semantics: selectable content
/// (values / formulas / formats / all / all-except-borders), an optional arithmetic operation against
/// the existing target values, skip-blanks, and transpose. Relative formula references are shifted by
/// the paste offset (except when transposing, where formulas are flattened to their computed value to
/// avoid producing wrong references). <see cref="Undo"/> restores the overwritten cells exactly.
/// </summary>
public sealed class PasteSpecialCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly string _targetCellRef;
    private readonly SpreadsheetPasteSpecialOptions _options;
    private readonly CultureInfo _culture;

    private readonly Dictionary<string, SpreadsheetCell?> _previous = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _affected = [];

    /// <summary>Creates the paste-special command targeting the given top-left cell reference.</summary>
    public PasteSpecialCommand(
        SpreadsheetSheet sheet,
        string targetCellRef,
        SpreadsheetPasteSpecialOptions options,
        CultureInfo culture)
    {
        _sheet = sheet;
        _targetCellRef = targetCellRef;
        _options = options;
        _culture = culture;
    }

    /// <summary>The cell references changed by the latest paste (for renderer invalidation).</summary>
    public IReadOnlyList<string> AffectedCellRefs => _affected;

    public void Execute()
    {
        _affected.Clear();
        _previous.Clear();

        var source = SpreadsheetClipboard.Cells;
        if (source is null || source.Count == 0)
            return;

        var sourceCoords = source.Keys
            .Select(r => (Ref: r, Pos: SpreadsheetRange.Parse(r)))
            .ToList();
        var minRow = sourceCoords.Min(c => c.Pos.StartRow);
        var minCol = sourceCoords.Min(c => c.Pos.StartCol);

        var target = SpreadsheetRange.Parse(_targetCellRef);

        foreach (var (sourceRef, pos) in sourceCoords)
        {
            var relRow = pos.StartRow - minRow;
            var relCol = pos.StartCol - minCol;
            if (_options.Transpose)
                (relRow, relCol) = (relCol, relRow);

            var destRow = target.StartRow + relRow;
            var destCol = target.StartCol + relCol;
            var destRef = $"{SpreadsheetRange.ColumnIndexToLetters(destCol)}{destRow + 1}";

            var srcCell = source[sourceRef];
            if (_options.SkipBlanks && IsBlank(srcCell))
                continue;

            if (!_previous.ContainsKey(destRef))
                _previous[destRef] = _sheet.Cells.TryGetValue(destRef, out var existing) ? existing.Clone() : null;

            ApplyToCell(destRef, sourceRef, srcCell);
            _affected.Add(destRef);
        }

        Recalculate(_affected);
    }

    private void ApplyToCell(string destRef, string sourceRef, SpreadsheetCell? srcCell)
    {
        var cell = _sheet.GetOrCreateCell(destRef);

        var writeValues = _options.Content is SpreadsheetPasteContent.All
            or SpreadsheetPasteContent.AllExceptBorders
            or SpreadsheetPasteContent.Values
            or SpreadsheetPasteContent.ValuesAndFormats
            or SpreadsheetPasteContent.Formulas;

        var writeFormats = _options.Content is SpreadsheetPasteContent.All
            or SpreadsheetPasteContent.AllExceptBorders
            or SpreadsheetPasteContent.Formats
            or SpreadsheetPasteContent.ValuesAndFormats;

        var useFormula = _options.Content is SpreadsheetPasteContent.All
            or SpreadsheetPasteContent.AllExceptBorders
            or SpreadsheetPasteContent.Formulas;

        if (writeValues)
        {
            if (_options.Operation != SpreadsheetPasteOperation.None)
            {
                ApplyOperation(cell, srcCell);
            }
            else if (useFormula && !_options.Transpose && !string.IsNullOrEmpty(srcCell?.Formula))
            {
                cell.Formula = AdjustFormula(srcCell!.Formula!, destRef, sourceRef);
                cell.Value = null;
            }
            else
            {
                cell.Formula = null;
                cell.Value = srcCell?.Value;
                cell.DataType = srcCell?.DataType ?? SpreadsheetDataType.Text;
            }
            cell.DisplayValue = null;
        }

        if (writeFormats && srcCell is not null)
        {
            var style = srcCell.Style.Clone();
            if (_options.Content == SpreadsheetPasteContent.AllExceptBorders)
            {
                style.BorderTop = new SpreadsheetBorder();
                style.BorderRight = new SpreadsheetBorder();
                style.BorderBottom = new SpreadsheetBorder();
                style.BorderLeft = new SpreadsheetBorder();
            }
            cell.Style = style;
        }
    }

    private void ApplyOperation(SpreadsheetCell cell, SpreadsheetCell? srcCell)
    {
        var hasSrc = SpreadsheetCellValueComparer.TryGetNumber(srcCell, _culture, out var srcVal);
        var hasDest = SpreadsheetCellValueComparer.TryGetNumber(cell, _culture, out var destVal);

        if (!hasSrc)
        {
            // Non-numeric source: fall back to a plain value replace.
            cell.Formula = null;
            cell.Value = srcCell?.Value;
            cell.DataType = srcCell?.DataType ?? SpreadsheetDataType.Text;
            return;
        }

        if (!hasDest)
            destVal = 0;

        var result = _options.Operation switch
        {
            SpreadsheetPasteOperation.Add => destVal + srcVal,
            SpreadsheetPasteOperation.Subtract => destVal - srcVal,
            SpreadsheetPasteOperation.Multiply => destVal * srcVal,
            SpreadsheetPasteOperation.Divide => srcVal != 0 ? destVal / srcVal : double.NaN,
            _ => srcVal
        };

        cell.Formula = null;
        cell.Value = result;
        cell.DataType = SpreadsheetDataType.Number;
    }

    private static string AdjustFormula(string formula, string destRef, string sourceRef)
    {
        var dest = SpreadsheetRange.Parse(destRef);
        var src = SpreadsheetRange.Parse(sourceRef);
        var dRow = dest.StartRow - src.StartRow;
        var dCol = dest.StartCol - src.StartCol;
        return FormulaReferenceAdjuster.AdjustFormula(formula, dRow, dCol);
    }

    public void Undo()
    {
        foreach (var (cellRef, cell) in _previous)
        {
            if (cell is null)
                _sheet.Cells.Remove(cellRef);
            else
                _sheet.Cells[cellRef] = cell.Clone();
        }

        Recalculate(_previous.Keys);
    }

    private void Recalculate(IEnumerable<string> cellRefs)
    {
        var refs = cellRefs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var cellRef in refs)
        {
            _sheet.UpdateDependencies(cellRef);
            if (_sheet.Cells.TryGetValue(cellRef, out var cell) && !string.IsNullOrEmpty(cell.Formula))
                _sheet.EvaluateFormula(cellRef);
        }

        foreach (var cellRef in refs)
            _sheet.RecalculateDependents(cellRef);
    }

    private static bool IsBlank(SpreadsheetCell? cell)
        => cell is null || (cell.Value is null && string.IsNullOrEmpty(cell.Formula));
}
