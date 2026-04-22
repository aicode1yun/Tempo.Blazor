using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Text;

/// <summary>
/// Self-contained contenteditable heading (H1 / H2 / H3). Owns its JS keyboard-handler lifecycle.
/// Renders the correct semantic element based on <see cref="IHeadingBlockContent.Level"/>.
/// Supports optional toggle (collapse/expand) controlled by <see cref="IHeadingBlockContent.IsToggleable"/>.
/// [JSInvokable] method names must match strings called in notion-editor.js — hence EventCallback
/// parameter names differ to avoid C# naming conflicts.
/// </summary>
public partial class TmNotionHeadingBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IHeadingBlockContent? Content   { get; set; }
    [Parameter] public bool                  ReadOnly  { get; set; }
    [Parameter] public bool                  IsFocused { get; set; }

    /// <summary>Fired on blur when HTML content changed. Arg = new HTML.</summary>
    [Parameter] public EventCallback<string>                    OnContentSaved    { get; set; }

    /// <summary>Fired when Enter is pressed; creates a new Paragraph, not another heading.
    /// Arg = HTML fragment after the caret (may be empty).</summary>
    [Parameter] public EventCallback<string>                    OnEnterSplit      { get; set; }

    /// <summary>Fired when Backspace is pressed on an empty block.</summary>
    [Parameter] public EventCallback                            OnDeleteRequested { get; set; }

    /// <summary>Fired when Tab / Shift+Tab. Bool = shiftKey.</summary>
    [Parameter] public EventCallback<bool>                      OnIndentChange    { get; set; }

    /// <summary>Fired when a markdown shortcut is recognised (e.g. "heading1", "heading2").</summary>
    [Parameter] public EventCallback<string>                    OnTypeConvert     { get; set; }

    /// <summary>Fired when the element receives focus.</summary>
    [Parameter] public EventCallback                            OnFocused         { get; set; }

    /// <summary>Fired when '/' slash command is triggered. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnSlashMenu       { get; set; }

    /// <summary>Fired when '@' mention is triggered. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnMentionMenu     { get; set; }

    /// <summary>Fired when '[[' page-link is triggered. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnPageLinkMenu    { get; set; }

    /// <summary>Fired when the toggle arrow is clicked. Bool = new isOpen state.</summary>
    [Parameter] public EventCallback<bool>                      OnToggleChanged   { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                              _editableRef;
    private DotNetObjectReference<TmNotionHeadingBlock>?  _dotNetRef;
    private bool                                          _kbInitialized;
    private bool                                          _dirty;
    private string?                                       _lastHtml;
    private IHeadingBlockContent?                         _lastContent;
    private bool                                          _isToggleOpen = true;

    // ── Computed CSS ─────────────────────────────────────────────────────────

    private string _alignClass => Content?.Alignment switch
    {
        TextAlignment.Center => "tm-notion-align-center",
        TextAlignment.Right  => "tm-notion-align-right",
        _                    => string.Empty
    };

    private string _bgClass =>
        string.IsNullOrEmpty(Content?.BackgroundColor)
            ? string.Empty
            : $"tm-notion-bg-{Content.BackgroundColor}";

    private string _closedClass => _isToggleOpen ? string.Empty : "tm-notion-heading--closed";

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent   = Content;
        _dirty         = false;
        _kbInitialized = false;
        _lastHtml      = null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ReadOnly) return;

        var html = Content?.Html ?? string.Empty;

        if (!_kbInitialized)
        {
            _kbInitialized = true;
            _lastHtml      = html;
            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                await JS.InvokeVoidAsync("tmNotionEditor.initKeyboardHandler", _editableRef, _dotNetRef);
                await JS.InvokeVoidAsync("tmNotionEditor.setHtml", _editableRef, html);
                if (IsFocused)
                    await JS.InvokeVoidAsync("tmNotionEditor.focusAtStart", _editableRef);
            }
            catch { }
        }
        else if (html != _lastHtml)
        {
            _lastHtml = html;
            try { await JS.InvokeVoidAsync("tmNotionEditor.setHtml", _editableRef, html); }
            catch { }
        }
    }

    // ── Blur / focus ──────────────────────────────────────────────────────────

    private async Task OnBlurAsync()
    {
        if (!_dirty || ReadOnly) return;
        _dirty = false;
        try
        {
            var html  = await JS.InvokeAsync<string>("tmNotionEditor.getHtml", _editableRef);
            _lastHtml = html;
            await OnContentSaved.InvokeAsync(html);
        }
        catch { }
    }

    private async Task HandleFocusAsync() => await OnFocused.InvokeAsync();

    // ── Toggle ────────────────────────────────────────────────────────────────

    private async Task HandleToggleAsync()
    {
        _isToggleOpen = !_isToggleOpen;
        await OnToggleChanged.InvokeAsync(_isToggleOpen);
    }

    // ── JS keyboard callbacks — names MUST match notion-editor.js ─────────────

    [JSInvokable]
    public async Task OnEnterPressed(string beforeHtml, string afterHtml)
    {
        _lastHtml = beforeHtml;
        _dirty    = false;
        await OnContentSaved.InvokeAsync(beforeHtml);
        await OnEnterSplit.InvokeAsync(afterHtml);
    }

    [JSInvokable]
    public async Task OnBackspaceOnEmpty() => await OnDeleteRequested.InvokeAsync();

    [JSInvokable]
    public async Task OnTabPressed(bool shiftKey) => await OnIndentChange.InvokeAsync(shiftKey);

    [JSInvokable]
    public void OnArrowUp() { }

    [JSInvokable]
    public void OnArrowDown() { }

    [JSInvokable]
    public async Task OnMarkdownShortcut(string shortcut) =>
        await OnTypeConvert.InvokeAsync(shortcut);

    [JSInvokable]
    public async Task OnSlashTriggered(double top, double left) =>
        await OnSlashMenu.InvokeAsync((top, left));

    [JSInvokable]
    public async Task OnMentionTriggered(double top, double left) =>
        await OnMentionMenu.InvokeAsync((top, left));

    [JSInvokable]
    public async Task OnPageLinkTriggered(double top, double left) =>
        await OnPageLinkMenu.InvokeAsync((top, left));

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_kbInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyBlock", _editableRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
