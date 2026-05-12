using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Special;

public partial class TmNotionEquationBlock : ComponentBase
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IEquationBlockContent? Content           { get; set; }
    [Parameter] public bool                   ReadOnly          { get; set; }
    [Parameter] public bool                   IsFocused         { get; set; }

    [Parameter] public EventCallback<string>  OnExpressionSaved { get; set; }
    [Parameter] public EventCallback          OnDeleteRequested { get; set; }
    [Parameter] public EventCallback          OnFocused         { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private bool             _isEditing;
    private string           _editBuffer     = string.Empty;

    private bool             _focusPending;
    private bool             _renderPending;

    private ElementReference _renderRef;
    private ElementReference _inputRef;
    private ElementReference _previewRef;

    private IEquationBlockContent? _lastContent;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _emptyCls =>
        string.IsNullOrWhiteSpace(Content?.Expression)
            ? "tm-notion-equation-block--empty"
            : string.Empty;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent = Content;
        if (!_isEditing)
            _renderPending = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusPending)
        {
            _focusPending = false;
            try
            {
                await JS.InvokeVoidAsync("tmNotionEditor.setCode", _inputRef, _editBuffer);
                await _inputRef.FocusAsync();
                if (!string.IsNullOrWhiteSpace(_editBuffer))
                    await JS.InvokeVoidAsync("tmNotionEditor.renderEquation", _previewRef, _editBuffer);
            }
            catch { }
        }

        if (_renderPending)
        {
            _renderPending = false;
            var expr = Content?.Expression;
            if (!string.IsNullOrWhiteSpace(expr))
            {
                try { await JS.InvokeVoidAsync("tmNotionEditor.renderEquation", _renderRef, expr); }
                catch { }
            }
        }
    }

    // ── View mode interactions ────────────────────────────────────────────────

    private async Task HandleClickAsync()
    {
        if (ReadOnly) return;
        _editBuffer   = Content?.Expression ?? string.Empty;
        _isEditing    = true;
        _focusPending = true;
        await OnFocused.InvokeAsync();
        StateHasChanged();
    }

    private async Task HandleViewKeyDownAsync(KeyboardEventArgs e)
    {
        if (ReadOnly) return;
        if (e.Key is "Enter" or " ")
            await HandleClickAsync();
    }

    // ── Edit mode interactions ────────────────────────────────────────────────

    private async Task HandleInputAsync(ChangeEventArgs e)
    {
        _editBuffer = e.Value?.ToString() ?? string.Empty;
        try { await JS.InvokeVoidAsync("tmNotionEditor.renderEquation", _previewRef, _editBuffer); }
        catch { }
    }

    private async Task HandleBlurAsync()
    {
        // Small delay so button clicks (Done/Cancel) fire before blur commits
        await Task.Delay(150);
        if (_isEditing)
            await CommitAsync();
    }

    private async Task HandleEditKeyDownAsync(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Enter" when !e.ShiftKey:
                await CommitAsync();
                break;
            case "Escape":
                await CancelAsync();
                break;
        }
    }

    // ── Commit / cancel ───────────────────────────────────────────────────────

    private async Task CommitAsync()
    {
        if (!_isEditing) return;
        _isEditing     = false;
        _renderPending = true;
        var expr = _editBuffer.Trim();
        if (expr != (Content?.Expression ?? string.Empty))
            await OnExpressionSaved.InvokeAsync(expr);
        StateHasChanged();
    }

    private Task CancelAsync()
    {
        _isEditing     = false;
        _renderPending = true;
        StateHasChanged();
        return Task.CompletedTask;
    }
}
