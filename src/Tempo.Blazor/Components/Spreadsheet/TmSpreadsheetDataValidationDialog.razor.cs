using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

public partial class TmSpreadsheetDataValidationDialog
{
    [Parameter] public SpreadsheetDataValidation? Validation { get; set; }
    [Parameter] public EventCallback<SpreadsheetDataValidation> OnSave { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private string _activeTab = "Settings";

    private SpreadsheetValidationType _validationType = SpreadsheetValidationType.Any;
    private SpreadsheetValidationOperator _operator = SpreadsheetValidationOperator.Between;
    private string _formula1 = string.Empty;
    private string _formula2 = string.Empty;
    private bool _allowBlank = true;
    private bool _showDropDown = true;

    private bool _showInputMessage;
    private string _inputTitle = string.Empty;
    private string _inputMessage = string.Empty;

    private bool _showErrorAlert = true;
    private SpreadsheetValidationErrorStyle _errorStyle = SpreadsheetValidationErrorStyle.Stop;
    private string _errorTitle = string.Empty;
    private string _errorMessage = string.Empty;

    private static readonly SpreadsheetValidationType[] _allTypes =
    [
        SpreadsheetValidationType.Any,
        SpreadsheetValidationType.Whole,
        SpreadsheetValidationType.Decimal,
        SpreadsheetValidationType.List,
        SpreadsheetValidationType.Date,
        SpreadsheetValidationType.Time,
        SpreadsheetValidationType.TextLength,
        SpreadsheetValidationType.Custom
    ];

    private static readonly SpreadsheetValidationOperator[] _allOperators =
    [
        SpreadsheetValidationOperator.Between,
        SpreadsheetValidationOperator.NotBetween,
        SpreadsheetValidationOperator.Equal,
        SpreadsheetValidationOperator.NotEqual,
        SpreadsheetValidationOperator.GreaterThan,
        SpreadsheetValidationOperator.LessThan,
        SpreadsheetValidationOperator.GreaterOrEqual,
        SpreadsheetValidationOperator.LessOrEqual
    ];

    protected override void OnParametersSet()
    {
        var v = Validation;
        if (v is null) return;
        _validationType = v.Type;
        _operator = v.Operator;
        _formula1 = v.Formula1 ?? string.Empty;
        _formula2 = v.Formula2 ?? string.Empty;
        _allowBlank = v.AllowBlank;
        _showDropDown = v.ShowDropDown;
        _showInputMessage = v.InputMessage is not null;
        _inputTitle = v.InputMessage?.Title ?? string.Empty;
        _inputMessage = v.InputMessage?.Message ?? string.Empty;
        _showErrorAlert = v.ErrorAlert is not null;
        _errorStyle = v.ErrorAlert?.Style ?? SpreadsheetValidationErrorStyle.Stop;
        _errorTitle = v.ErrorAlert?.Title ?? string.Empty;
        _errorMessage = v.ErrorAlert?.Message ?? string.Empty;
    }

    private void OnTypeChanged(ChangeEventArgs e)
        => _validationType = Enum.TryParse<SpreadsheetValidationType>(e.Value?.ToString(), out var t) ? t : SpreadsheetValidationType.Any;

    private void OnOperatorChanged(ChangeEventArgs e)
        => _operator = Enum.TryParse<SpreadsheetValidationOperator>(e.Value?.ToString(), out var op) ? op : SpreadsheetValidationOperator.Between;

    private void OnErrorStyleChanged(ChangeEventArgs e)
        => _errorStyle = Enum.TryParse<SpreadsheetValidationErrorStyle>(e.Value?.ToString(), out var s) ? s : SpreadsheetValidationErrorStyle.Stop;

    private async Task OnApply()
    {
        var result = new SpreadsheetDataValidation
        {
            Range = Validation?.Range ?? new SpreadsheetRange(0, 0, 0, 0),
            Type = _validationType,
            Operator = _operator,
            Formula1 = string.IsNullOrWhiteSpace(_formula1) ? null : _formula1.Trim(),
            Formula2 = string.IsNullOrWhiteSpace(_formula2) ? null : _formula2.Trim(),
            AllowBlank = _allowBlank,
            ShowDropDown = _showDropDown,
            InputMessage = _showInputMessage && (!string.IsNullOrEmpty(_inputTitle) || !string.IsNullOrEmpty(_inputMessage))
                ? new SpreadsheetInputMessage { Title = _inputTitle, Message = _inputMessage }
                : null,
            ErrorAlert = _showErrorAlert
                ? new SpreadsheetValidationErrorAlert { Style = _errorStyle, Title = _errorTitle, Message = _errorMessage }
                : null
        };

        await OnSave.InvokeAsync(result);
    }

    private async Task OnCancel() => await OnClose.InvokeAsync();

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            await OnClose.InvokeAsync();
    }
}
