using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Edits an existing named range and recalculates any formulas that reference it.
/// Supports undo.
/// </summary>
public sealed class EditNamedRangeCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetWorkbook _workbook;
    private readonly SpreadsheetNamedRange _range;
    private readonly string _newName;
    private readonly string _newRefersTo;
    private readonly NamedRangeScope _newScope;
    private readonly int? _newSheetIndex;
    private readonly string? _newComment;

    private string _previousName = string.Empty;
    private string _previousRefersTo = string.Empty;
    private NamedRangeScope _previousScope;
    private int? _previousSheetIndex;
    private string? _previousComment;

    public EditNamedRangeCommand(
        SpreadsheetWorkbook workbook,
        SpreadsheetNamedRange range,
        string newName,
        string newRefersTo,
        NamedRangeScope newScope,
        int? newSheetIndex,
        string? newComment)
    {
        _workbook = workbook;
        _range = range;
        _newName = newName;
        _newRefersTo = newRefersTo;
        _newScope = newScope;
        _newSheetIndex = newSheetIndex;
        _newComment = newComment;
    }

    public void Execute()
    {
        CaptureState();
        Apply(_newName, _newRefersTo, _newScope, _newSheetIndex, _newComment);

        _workbook.RecalculateNamedRangeDependents(_newName);
        if (!string.Equals(_previousName, _newName, StringComparison.OrdinalIgnoreCase))
            _workbook.RecalculateNamedRangeDependents(_previousName);
    }

    public void Undo()
    {
        Apply(_previousName, _previousRefersTo, _previousScope, _previousSheetIndex, _previousComment);

        _workbook.RecalculateNamedRangeDependents(_previousName);
        if (!string.Equals(_previousName, _newName, StringComparison.OrdinalIgnoreCase))
            _workbook.RecalculateNamedRangeDependents(_newName);
    }

    private void CaptureState()
    {
        _previousName = _range.Name;
        _previousRefersTo = _range.RefersTo;
        _previousScope = _range.Scope;
        _previousSheetIndex = _range.SheetIndex;
        _previousComment = _range.Comment;
    }

    private void Apply(string name, string refersTo, NamedRangeScope scope, int? sheetIndex, string? comment)
    {
        _range.Name = name;
        _range.RefersTo = refersTo;
        _range.Scope = scope;
        _range.SheetIndex = sheetIndex;
        _range.Comment = comment;
    }
}
