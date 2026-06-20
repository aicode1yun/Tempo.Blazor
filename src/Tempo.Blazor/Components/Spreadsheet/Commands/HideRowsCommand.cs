using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Hides or unhides a set of rows and supports undo.</summary>
public sealed class HideRowsCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly List<int> _rowIndices;
    private readonly bool _hidden;
    private readonly Dictionary<int, bool> _previousState = new();

    public HideRowsCommand(SpreadsheetSheet sheet, IEnumerable<int> rowIndices, bool hidden = true)
    {
        _sheet = sheet;
        _rowIndices = rowIndices.ToList();
        _hidden = hidden;
    }

    public void Execute()
    {
        _previousState.Clear();
        foreach (var idx in _rowIndices)
        {
            if (!_sheet.Rows.TryGetValue(idx, out var row))
            {
                row = new SpreadsheetRow { Index = idx };
                _sheet.Rows[idx] = row;
            }
            _previousState[idx] = row.IsHidden;
            row.IsHidden = _hidden;
        }
    }

    public void Undo()
    {
        foreach (var (idx, wasHidden) in _previousState)
        {
            if (_sheet.Rows.TryGetValue(idx, out var row))
                row.IsHidden = wasHidden;
        }
    }
}
