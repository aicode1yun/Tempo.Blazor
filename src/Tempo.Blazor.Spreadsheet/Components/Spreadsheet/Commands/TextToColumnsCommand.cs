using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Format;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Splits the text of a single source column into several columns using
/// <see cref="SpreadsheetTextToColumns"/>, writing the produced fields starting at the source column
/// and overwriting cells to its right. Each produced column gets a target format (general → value
/// parser, text → literal, skip → dropped). The whole region is snapshotted so <see cref="Undo"/>
/// restores the original single column exactly.
/// </summary>
public sealed class TextToColumnsCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly int _sourceCol;
    private readonly int _startRow;
    private readonly int _endRow;
    private readonly SpreadsheetSeparatorOptions _options;
    private readonly IReadOnlyList<SpreadsheetColumnFormat> _formats;
    private readonly CultureInfo _culture;

    private Dictionary<string, SpreadsheetCell?>? _snapshot;
    private readonly List<string> _affected = [];

    /// <summary>Creates a text-to-columns command for a vertical slice of one column.</summary>
    public TextToColumnsCommand(
        SpreadsheetSheet sheet,
        int sourceCol,
        int startRow,
        int endRow,
        SpreadsheetSeparatorOptions options,
        IReadOnlyList<SpreadsheetColumnFormat> formats,
        CultureInfo culture)
    {
        _sheet = sheet;
        _sourceCol = sourceCol;
        _startRow = startRow;
        _endRow = endRow;
        _options = options;
        _formats = formats;
        _culture = culture;
    }

    /// <summary>The cell references touched by the last <see cref="Execute"/> (for renderer invalidation).</summary>
    public IReadOnlyList<string> AffectedCellRefs => _affected;

    /// <summary>The number of output columns produced by the last <see cref="Execute"/>.</summary>
    public int ColumnsProduced { get; private set; }

    public void Execute()
    {
        // 1. Read and split each source row.
        var splitRows = new List<IReadOnlyList<string>>();
        for (var row = _startRow; row <= _endRow; row++)
        {
            var text = SourceText(_sheet.GetCell(row, _sourceCol));
            splitRows.Add(SpreadsheetTextToColumns.Split([text], _options)[0]);
        }

        // 2. Apply per-column format, dropping skipped fields. The kept fields are placed consecutively.
        var placedRows = splitRows.Select(KeepNonSkipped).ToList();
        ColumnsProduced = placedRows.Count == 0 ? 0 : placedRows.Max(r => r.Count);

        // 3. Snapshot the whole destination region (source column + every column written).
        var lastCol = _sourceCol + Math.Max(ColumnsProduced, 1) - 1;
        _snapshot = SnapshotRegion(lastCol);
        _affected.Clear();
        _affected.AddRange(_snapshot.Keys);

        // 4. Clear the region, then write the produced cells.
        foreach (var cellRef in _snapshot.Keys)
            _sheet.Cells.Remove(cellRef);

        for (var r = 0; r < placedRows.Count; r++)
        {
            var row = _startRow + r;
            var fields = placedRows[r];
            for (var j = 0; j < fields.Count; j++)
                WriteCell(row, _sourceCol + j, fields[j].Value, fields[j].Format);
        }

        Recalculate(_snapshot.Keys);
    }

    public void Undo()
    {
        if (_snapshot is null)
            return;

        foreach (var (cellRef, cell) in _snapshot)
        {
            if (cell is null)
                _sheet.Cells.Remove(cellRef);
            else
                _sheet.Cells[cellRef] = cell.Clone();
        }

        Recalculate(_snapshot.Keys);
    }

    private List<(string Value, SpreadsheetColumnFormat Format)> KeepNonSkipped(IReadOnlyList<string> tokens)
    {
        var kept = new List<(string, SpreadsheetColumnFormat)>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var format = i < _formats.Count ? _formats[i] : SpreadsheetColumnFormat.General;
            if (format == SpreadsheetColumnFormat.Skip)
                continue;
            kept.Add((tokens[i], format));
        }
        return kept;
    }

    private void WriteCell(int row, int col, string text, SpreadsheetColumnFormat format)
    {
        var cellRef = CellRef(row, col);
        var cell = _sheet.GetOrCreateCell(cellRef);

        if (format == SpreadsheetColumnFormat.Text)
        {
            cell.Value = text;
            cell.Formula = null;
            cell.DataType = SpreadsheetDataType.Text;
            cell.DisplayValue = null;
            return;
        }

        var parsed = SpreadsheetValueParser.Parse(text, _culture);
        if (parsed.Formula is not null)
        {
            cell.Formula = parsed.Formula;
            cell.Value = null;
        }
        else
        {
            cell.Value = parsed.Value;
            cell.Formula = null;
            cell.DataType = parsed.Type;
            if (parsed.ImpliedNumberFormat is not null && IsGeneral(cell.Style.NumberFormat))
                cell.Style.NumberFormat = parsed.ImpliedNumberFormat;
        }

        cell.DisplayValue = null;
    }

    private Dictionary<string, SpreadsheetCell?> SnapshotRegion(int lastCol)
    {
        var snapshot = new Dictionary<string, SpreadsheetCell?>(StringComparer.OrdinalIgnoreCase);
        for (var row = _startRow; row <= _endRow; row++)
            for (var col = _sourceCol; col <= lastCol; col++)
            {
                var cellRef = CellRef(row, col);
                snapshot[cellRef] = _sheet.Cells.TryGetValue(cellRef, out var cell) ? cell.Clone() : null;
            }

        return snapshot;
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

    private static string SourceText(SpreadsheetCell? cell)
    {
        if (cell?.Value is string s)
            return s;
        return cell?.Value?.ToString() ?? string.Empty;
    }

    private static string CellRef(int row, int col)
        => $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";

    private static bool IsGeneral(string? format)
        => string.IsNullOrEmpty(format) || format.Equals("General", StringComparison.OrdinalIgnoreCase);
}
