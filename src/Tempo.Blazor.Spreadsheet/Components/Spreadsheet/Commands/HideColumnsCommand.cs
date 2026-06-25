using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>Hides or unhides a set of columns and supports undo.</summary>
public sealed class HideColumnsCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly List<int> _colIndices;
    private readonly bool _hidden;
    private readonly Dictionary<int, bool> _previousState = new();

    public HideColumnsCommand(SpreadsheetSheet sheet, IEnumerable<int> colIndices, bool hidden = true)
    {
        _sheet = sheet;
        _colIndices = colIndices.ToList();
        _hidden = hidden;
    }

    public void Execute()
    {
        _previousState.Clear();
        foreach (var idx in _colIndices)
        {
            if (!_sheet.Columns.TryGetValue(idx, out var col))
            {
                col = new SpreadsheetColumn { Index = idx };
                _sheet.Columns[idx] = col;
            }
            _previousState[idx] = col.IsHidden;
            col.IsHidden = _hidden;
        }
    }

    public void Undo()
    {
        foreach (var (idx, wasHidden) in _previousState)
        {
            if (_sheet.Columns.TryGetValue(idx, out var col))
                col.IsHidden = wasHidden;
        }
    }
}
