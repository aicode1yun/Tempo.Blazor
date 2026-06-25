using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Text;

/// <summary>
/// Self-contained code block with language selector, textarea editor, copy button, and optional caption.
/// Uses a dedicated JS keyboard handler (initCodeKeyboardHandler) that handles Tab/Shift+Tab
/// indentation and Backspace-on-empty — Tab does NOT move focus like other text blocks.
/// </summary>
public partial class TmNotionCodeBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public ICodeBlockContent? Content   { get; set; }
    [Parameter] public bool               ReadOnly  { get; set; }
    [Parameter] public bool               IsFocused { get; set; }

    /// <summary>Fired on blur when the code text has changed. Arg = new code string.</summary>
    [Parameter] public EventCallback<string>  OnCodeSaved         { get; set; }

    /// <summary>Fired when the user selects a new language. Arg = language name (null → plain text).</summary>
    [Parameter] public EventCallback<string?> OnLanguageChanged   { get; set; }

    /// <summary>Fired on caption blur when the caption has changed. Arg = new caption (null if empty).</summary>
    [Parameter] public EventCallback<string?> OnCaptionSaved      { get; set; }

    /// <summary>Fired when Backspace is pressed in an empty code block.</summary>
    [Parameter] public EventCallback          OnDeleteRequested   { get; set; }

    /// <summary>Fired when the textarea receives focus.</summary>
    [Parameter] public EventCallback          OnFocused           { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                           _textareaRef;
    private ElementReference                           _captionRef;
    private DotNetObjectReference<TmNotionCodeBlock>?  _dotNetRef;
    private bool                                       _kbInitialized;
    private bool                                       _codeDirty;
    private bool                                       _captionDirty;
    private ICodeBlockContent?                         _lastContent;
    private string                                     _selectedLanguage = "Plain Text";
    private bool                                       _codeCopied;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _displayLanguage =>
        string.IsNullOrWhiteSpace(_selectedLanguage) ? "Plain Text" : _selectedLanguage;

    private string _wrapClass =>
        Content?.WrapLines == true ? "tm-notion-code-block--wrap" : string.Empty;

    private bool _showCaption =>
        !string.IsNullOrEmpty(Content?.Caption) || !ReadOnly;

    // ── Language list ─────────────────────────────────────────────────────────

    private static readonly string[] _languages =
    [
        "Plain Text",
        "JavaScript", "TypeScript", "JSX", "TSX",
        "Python", "C#", "Java", "Go", "Rust",
        "C", "C++", "PHP", "Ruby", "Swift", "Kotlin", "Dart", "Scala", "R",
        "SQL", "HTML", "CSS", "SCSS", "XML",
        "JSON", "YAML", "TOML", "INI",
        "Markdown", "Bash", "PowerShell", "Shell", "Batch",
        "Docker", "Terraform", "Nginx", "Apache"
    ];

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent      = Content;
        _selectedLanguage = Content?.Language ?? "Plain Text";
        _codeDirty        = false;
        _captionDirty     = false;
        _kbInitialized    = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ReadOnly) return;

        var code = Content?.Code ?? string.Empty;

        if (!_kbInitialized)
        {
            _kbInitialized = true;
            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                await JS.InvokeVoidAsync("tmNotionEditor.initCodeKeyboardHandler", _textareaRef, _dotNetRef);
                await JS.InvokeVoidAsync("tmNotionEditor.setCode", _textareaRef, code);

                if (!string.IsNullOrEmpty(Content?.Caption))
                    await JS.InvokeVoidAsync("tmNotionEditor.setHtml", _captionRef, Content.Caption);
            }
            catch { }
        }
    }

    // ── Code blur / focus ─────────────────────────────────────────────────────

    private async Task OnCodeBlurAsync()
    {
        if (!_codeDirty || ReadOnly) return;
        _codeDirty = false;
        try
        {
            var code = await JS.InvokeAsync<string>("tmNotionEditor.getCode", _textareaRef);
            await OnCodeSaved.InvokeAsync(code);
        }
        catch { }
    }

    private async Task HandleFocusAsync() => await OnFocused.InvokeAsync();

    // ── Caption blur ──────────────────────────────────────────────────────────

    private async Task OnCaptionBlurAsync()
    {
        if (!_captionDirty || ReadOnly) return;
        _captionDirty = false;
        try
        {
            var html = await JS.InvokeAsync<string>("tmNotionEditor.getHtml", _captionRef);
            var text = string.IsNullOrWhiteSpace(html) ? null : html;
            await OnCaptionSaved.InvokeAsync(text);
        }
        catch { }
    }

    // ── Language change ───────────────────────────────────────────────────────

    private async Task HandleLanguageChangedAsync()
    {
        var lang = string.IsNullOrWhiteSpace(_selectedLanguage) ? null : _selectedLanguage;
        await OnLanguageChanged.InvokeAsync(lang);
    }

    // ── Copy ──────────────────────────────────────────────────────────────────

    private async Task CopyCodeAsync()
    {
        try
        {
            var code = await JS.InvokeAsync<string>("tmNotionEditor.getCode", _textareaRef);
            if (string.IsNullOrEmpty(code) && Content?.Code is { Length: > 0 } stored)
                code = stored;
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", code);
            _codeCopied = true;
            StateHasChanged();
            await Task.Delay(2000);
            _codeCopied = false;
            StateHasChanged();
        }
        catch { }
    }

    // ── JS callback — must match notion-editor.js ─────────────────────────────

    [JSInvokable]
    public async Task OnBackspaceOnEmpty() => await OnDeleteRequested.InvokeAsync();

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_kbInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyBlock", _textareaRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
