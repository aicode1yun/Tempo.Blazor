using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Lists;

/// <summary>
/// Self-contained numbered list item. Renders its sequential number (computed by the parent
/// by counting preceding sibling NumberedList blocks at the same indent level).
/// Enter on an empty item fires OnTypeConvert("paragraph").
/// Enter on a non-empty item fires OnEnterSplit to create a new sibling.
/// Tab/Shift+Tab fires OnIndentChange so the parent can clamp (0–3) and persist.
/// </summary>
public partial class TmNotionNumberedListBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IListBlockContent? Content     { get; set; }
    [Parameter] public int                Number      { get; set; } = 1;
    [Parameter] public bool               ReadOnly    { get; set; }
    [Parameter] public bool               IsFocused   { get; set; }
    [Parameter] public string?            Placeholder { get; set; }

    /// <summary>Fired on blur when HTML has changed. Arg = new HTML.</summary>
    [Parameter] public EventCallback<string>                    OnContentSaved    { get; set; }

    /// <summary>Fired when Enter is pressed on a non-empty item. Arg = HTML after the split point.</summary>
    [Parameter] public EventCallback<string>                    OnEnterSplit      { get; set; }

    /// <summary>Fired when Backspace is pressed on an empty item.</summary>
    [Parameter] public EventCallback                            OnDeleteRequested { get; set; }

    /// <summary>Fired when Tab / Shift+Tab. Bool = shiftKey (true = outdent).</summary>
    [Parameter] public EventCallback<bool>                      OnIndentChange    { get; set; }

    /// <summary>
    /// Fired when a markdown or conversion shortcut is detected.
    /// Special value "paragraph" means the user pressed Enter on an empty item.
    /// </summary>
    [Parameter] public EventCallback<string>                    OnTypeConvert     { get; set; }

    /// <summary>Fired when the editable receives focus.</summary>
    [Parameter] public EventCallback                            OnFocused         { get; set; }

    /// <summary>Fired when '/' is typed. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnSlashMenu       { get; set; }

    /// <summary>Fired when '@' is typed. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnMentionMenu     { get; set; }

    /// <summary>Fired when '[[' is typed. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnPageLinkMenu    { get; set; }

    /// <summary>Fired when '{{' token syntax is typed. Args = (top, left) caret coords.</summary>
    [Parameter] public EventCallback<(double Top, double Left)> OnTokenMenu       { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                                   _editableRef;
    private DotNetObjectReference<TmNotionNumberedListBlock>?  _dotNetRef;
    private bool                                               _kbInitialized;
    private bool                                               _dirty;
    private string?                                            _lastHtml;
    private IListBlockContent?                                 _lastContent;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _numberLabel => (Content?.IndentLevel ?? 0) switch
    {
        1 => $"{(char)('a' + Math.Min(Number - 1, 25))}.",
        2 => $"{ToRoman(Number)}.",
        _ => $"{Number}."
    };

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
        if (IsEmptyHtml(beforeHtml))
        {
            await OnTypeConvert.InvokeAsync("paragraph");
            return;
        }

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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsEmptyHtml(string html) =>
        string.IsNullOrWhiteSpace(html) || html.Trim() is "" or "<br>" or "<br/>" or "&nbsp;";

    private static string ToRoman(int n)
    {
        if (n < 1) return "i";
        var result = new System.Text.StringBuilder();
        int[] vals  = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];
        string[] syms = ["m", "cm", "d", "cd", "c", "xc", "l", "xl", "x", "ix", "v", "iv", "i"];
        for (var i = 0; i < vals.Length; i++)
        {
            while (n >= vals[i]) { result.Append(syms[i]); n -= vals[i]; }
        }
        return result.ToString();
    }
}
