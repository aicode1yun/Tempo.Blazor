using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Displays the active cell reference and provides an input for editing cell values and formulas.
/// Synchronizes bidirectionally with the active cell in the spreadsheet grid.
/// </summary>
public partial class TmSpreadsheetFormulaBar
{
    private ElementReference _inputRef;
    private string? _editValue;
    private bool _shouldFocusAfterRender;

    /// <summary>The A1 reference of the currently active cell.</summary>
    [Parameter] public string? ActiveCellRef { get; set; }

    /// <summary>The current display value or formula of the active cell.</summary>
    [Parameter] public string? DisplayValue { get; set; }

    /// <summary>Whether the formula bar is in editing mode.</summary>
    [Parameter] public bool IsEditing { get; set; }

    /// <summary>Called when the user starts editing in the formula bar.</summary>
    [Parameter] public EventCallback OnEditStarted { get; set; }

    /// <summary>Called when the user commits a value from the formula bar.</summary>
    [Parameter] public EventCallback<string?> OnValueCommitted { get; set; }

    /// <summary>Called when the user cancels editing in the formula bar.</summary>
    [Parameter] public EventCallback OnEditCancelled { get; set; }

    /// <summary>Called when the formula bar value changes during editing.</summary>
    [Parameter] public EventCallback<string?> OnValueChanged { get; set; }

    /// <summary>Called when the user presses Tab while editing in the formula bar.</summary>
    [Parameter] public EventCallback OnTabPressed { get; set; }

    protected override void OnParametersSet()
    {
        if (!IsEditing)
        {
            _editValue = DisplayValue;
        }
    }

    private void StartEdit()
    {
        if (IsEditing) return;
        IsEditing = true;
        _editValue = DisplayValue;
        _shouldFocusAfterRender = true;
        OnEditStarted.InvokeAsync();
    }

    private void OnInput(ChangeEventArgs e)
    {
        _editValue = e.Value?.ToString();
        OnValueChanged.InvokeAsync(_editValue);
    }

    private void HandleKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Enter":
                Commit();
                break;
            case "Escape":
                Cancel();
                break;
            case "Tab":
                Commit();
                OnTabPressed.InvokeAsync();
                break;
        }
    }

    private void Commit()
    {
        if (!IsEditing) return;
        OnValueCommitted.InvokeAsync(_editValue);
    }

    private void Cancel()
    {
        if (!IsEditing) return;
        _editValue = DisplayValue;
        OnEditCancelled.InvokeAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_shouldFocusAfterRender)
        {
            _shouldFocusAfterRender = false;
            try { await _inputRef.FocusAsync(); } catch { /* ElementReference may not be bound yet */ }
        }
    }
}
