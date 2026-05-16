using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Text;

/// <summary>
/// Self-contained contenteditable paragraph. Owns its JS keyboard-handler lifecycle
/// and surfaces high-level EventCallbacks so the parent dispatcher can react.
/// The [JSInvokable] method names must match the strings used in notion-editor.js.
/// EventCallback parameter names are distinct from those method names to avoid C# conflicts.
/// </summary>
public partial class TmNotionTextBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public ITextBlockContent? Content     { get; set; }
    [Parameter] public bool               ReadOnly    { get; set; }
    [Parameter] public bool               IsFocused   { get; set; }
    [Parameter] public string?            Placeholder { get; set; }

    /// <summary>Fired on blur when the HTML has changed. Arg = new HTML.</summary>
    [Parameter] public EventCallback<string>                    OnContentSaved     { get; set; }

    /// <summary>Fired after Enter splits the block. Arg = HTML fragment after the split point.</summary>
    [Parameter] public EventCallback<string>                    OnEnterSplit       { get; set; }

    /// <summary>Fired when Backspace is pressed on an empty block.</summary>
    [Parameter] public EventCallback                            OnDeleteRequested  { get; set; }

    /// <summary>Fired when Tab / Shift+Tab is pressed. Bool = shiftKey (true = outdent).</summary>
    [Parameter] public EventCallback<bool>                      OnIndentChange     { get; set; }

    /// <summary>Fired when a markdown pattern is recognised. Arg = shortcut key (e.g. "heading1").</summary>
    [Parameter] public EventCallback<string>                    OnTypeConvert      { get; set; }

    /// <summary>Fired when the element receives focus.</summary>
    [Parameter] public EventCallback                            OnFocused          { get; set; }

    /// <summary>Fired when '/' is typed in a trigger position. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnSlashMenu        { get; set; }

    /// <summary>Fired when '@' mention syntax is typed. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnMentionMenu      { get; set; }

    /// <summary>Fired when '[[' page-link syntax is typed. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnPageLinkMenu     { get; set; }

    /// <summary>Fired when '{{' token syntax is typed. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnTokenMenu        { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                           _editableRef;
    private DotNetObjectReference<TmNotionTextBlock>?  _dotNetRef;
    private bool                                       _kbInitialized;
    private bool                                       _dirty;
    private string?                                    _lastHtml;
    private ITextBlockContent?                         _lastContent;

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
        else if (!_dirty && html != _lastHtml)
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
        try
        {
            var html = await JS.InvokeAsync<string>("tmNotionEditor.getHtml", _editableRef);
            await OnContentSaved.InvokeAsync(html);
        }
        catch { }
        finally
        {
            _dirty = false;
        }
    }

    private async Task HandleFocusAsync() => await OnFocused.InvokeAsync();

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

    [JSInvokable]
    public async Task OnTokenTriggered(double top, double left) =>
        await OnTokenMenu.InvokeAsync((top, left));

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
