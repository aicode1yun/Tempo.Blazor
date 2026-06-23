using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Page;

public partial class TmNotionChildPageBlock : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IChildPageBlockContent? Content           { get; set; }
    [Parameter] public bool                    ReadOnly          { get; set; }
    [Parameter] public bool                    IsFocused         { get; set; }
    [Parameter] public EventCallback           OnNavigate        { get; set; }
    [Parameter] public EventCallback<string>   OnRenameCommitted { get; set; }
    [Parameter] public EventCallback           OnFocused         { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private bool             _isRenaming;
    private string           _renameBuffer = string.Empty;
    private bool             _focusPending;
    private ElementReference _inputRef;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _icon  => string.IsNullOrEmpty(Content?.IconEmoji) ? string.Empty : Content.IconEmoji;
    private string _title => string.IsNullOrEmpty(Content?.Title)     ? Loc["TmNotionEditor_Untitled"] : Content.Title;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusPending)
        {
            _focusPending = false;
            try { await _inputRef.FocusAsync(); } catch { }
        }
    }

    // ── View mode ─────────────────────────────────────────────────────────────

    private async Task HandleClickAsync()
    {
        await OnFocused.InvokeAsync();
        await OnNavigate.InvokeAsync();
    }

    private async Task HandleDblClickAsync()
    {
        if (ReadOnly) return;
        _renameBuffer = Content?.Title ?? string.Empty;
        _isRenaming   = true;
        _focusPending = true;
        await OnFocused.InvokeAsync();
        StateHasChanged();
    }

    private async Task HandleViewKeyDownAsync(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Enter":
                await HandleClickAsync();
                break;
            case "F2" when !ReadOnly:
                await HandleDblClickAsync();
                break;
        }
    }

    // ── Rename mode ───────────────────────────────────────────────────────────

    private async Task HandleRenameBlurAsync()
    {
        await Task.Delay(80);
        if (_isRenaming)
            await CommitRenameAsync();
    }

    private async Task HandleRenameKeyDownAsync(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Enter":
                await CommitRenameAsync();
                break;
            case "Escape":
                CancelRename();
                break;
        }
    }

    private async Task CommitRenameAsync()
    {
        if (!_isRenaming) return;
        _isRenaming = false;
        var newTitle = _renameBuffer.Trim();
        if (newTitle != (Content?.Title ?? string.Empty))
            await OnRenameCommitted.InvokeAsync(newTitle);
        StateHasChanged();
    }

    private void CancelRename()
    {
        _isRenaming = false;
        StateHasChanged();
    }
}
