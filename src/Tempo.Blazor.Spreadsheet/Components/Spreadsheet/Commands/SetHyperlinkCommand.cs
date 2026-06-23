using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Sets a structured hyperlink on a cell. If the cell is empty and a display text is provided,
/// the display text is written into the cell value. Supports undo.
/// </summary>
public sealed class SetHyperlinkCommand : ISpreadsheetCommand
{
    private readonly SpreadsheetSheet _sheet;
    private readonly string _cellRef;
    private readonly SpreadsheetHyperlink _hyperlink;

    private SpreadsheetHyperlink? _oldHyperlink;
    private object? _oldValue;
    private string? _oldFormula;

    public SetHyperlinkCommand(SpreadsheetSheet sheet, string cellRef, SpreadsheetHyperlink hyperlink)
    {
        _sheet = sheet;
        _cellRef = cellRef;
        _hyperlink = hyperlink;
    }

    public void Execute()
    {
        var cell = _sheet.GetOrCreateCell(_cellRef);
        _oldHyperlink = cell.Hyperlink?.Clone();
        _oldValue = cell.Value;
        _oldFormula = cell.Formula;

        cell.Hyperlink = _hyperlink.Clone();
        cell.Formula = null;

        // If the cell currently has no meaningful content and the hyperlink carries display
        // text, write the display text into the cell so the link is visible.
        if ((cell.Value is null || string.IsNullOrWhiteSpace(cell.Value.ToString()))
            && !string.IsNullOrWhiteSpace(_hyperlink.Display))
        {
            cell.Value = _hyperlink.Display;
            cell.DataType = SpreadsheetDataType.Text;
        }

        cell.DisplayValue = null;
    }

    public void Undo()
    {
        var cell = _sheet.GetOrCreateCell(_cellRef);
        cell.Hyperlink = _oldHyperlink?.Clone();
        cell.Value = _oldValue;
        cell.Formula = _oldFormula;
        cell.DisplayValue = null;
    }
}
