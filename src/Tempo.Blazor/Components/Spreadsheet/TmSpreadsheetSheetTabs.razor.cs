using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>Tab bar for switching between workbook sheets with add, delete, and rename support.</summary>
public partial class TmSpreadsheetSheetTabs
{
    private ElementReference _renameInput;
    private int _editingIndex = -1;
    private string? _editName;
    private bool _shouldFocusRenameInput;

    private bool _contextMenuVisible;
    private double _contextMenuX;
    private double _contextMenuY;
    private int _contextMenuSheetIndex = -1;

    /// <summary>All sheets in the workbook.</summary>
    [Parameter] public List<SpreadsheetSheet> Sheets { get; set; } = new();

    /// <summary>Zero-based index of the active sheet.</summary>
    [Parameter] public int ActiveIndex { get; set; }

    /// <summary>Called when the active sheet should change.</summary>
    [Parameter] public EventCallback<int> OnActiveSheetChanged { get; set; }

    /// <summary>Called when a new sheet should be added.</summary>
    [Parameter] public EventCallback OnAddSheetRequested { get; set; }

    /// <summary>Called when a sheet should be deleted.</summary>
    [Parameter] public EventCallback<int> OnDeleteSheetRequested { get; set; }

    /// <summary>Called when a sheet should be renamed.</summary>
    [Parameter] public EventCallback<(int Index, string NewName)> OnRenameSheetRequested { get; set; }

    private void OnTabClick(int index)
    {
        CloseContextMenu();
        if (_editingIndex == index) return;
        _editingIndex = -1;
        if (index != ActiveIndex)
            OnActiveSheetChanged.InvokeAsync(index);
    }

    private void OnCloseClick(MouseEventArgs e, int index)
    {
        CloseContextMenu();
        OnDeleteSheetRequested.InvokeAsync(index);
    }

    private void OnAddSheet()
    {
        CloseContextMenu();
        OnAddSheetRequested.InvokeAsync();
    }

    private void OnTabContextMenu(MouseEventArgs e, int index)
    {
        _contextMenuX = e.ClientX;
        _contextMenuY = e.ClientY;
        _contextMenuVisible = true;
        _contextMenuSheetIndex = index;
    }

    private void CloseContextMenu()
    {
        _contextMenuVisible = false;
        _contextMenuSheetIndex = -1;
    }

    private void ContextMenuRename()
    {
        if (_contextMenuSheetIndex < 0 || _contextMenuSheetIndex >= Sheets.Count) return;
        _editingIndex = _contextMenuSheetIndex;
        _editName = Sheets[_editingIndex].Name;
        _shouldFocusRenameInput = true;
        CloseContextMenu();
        StateHasChanged();
    }

    private void ContextMenuDelete()
    {
        if (_contextMenuSheetIndex < 0) return;
        var index = _contextMenuSheetIndex;
        CloseContextMenu();
        OnDeleteSheetRequested.InvokeAsync(index);
    }

    private void CommitRename(int index)
    {
        if (_editingIndex != index) return;
        if (!string.IsNullOrWhiteSpace(_editName))
        {
            OnRenameSheetRequested.InvokeAsync((index, _editName.Trim()));
        }
        _editingIndex = -1;
        _editName = null;
    }

    private void OnRenameKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Key == "Enter")
        {
            CommitRename(index);
        }
        else if (e.Key == "Escape")
        {
            _editingIndex = -1;
            _editName = null;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_shouldFocusRenameInput)
        {
            _shouldFocusRenameInput = false;
            try { await _renameInput.FocusAsync(); } catch { /* ElementReference may not be bound yet */ }
        }
    }
}
