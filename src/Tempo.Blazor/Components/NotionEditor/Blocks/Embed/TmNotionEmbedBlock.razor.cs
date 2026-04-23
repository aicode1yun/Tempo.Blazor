using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Embed;

public partial class TmNotionEmbedBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IEmbedBlockContent? Content  { get; set; }
    [Parameter] public bool                ReadOnly { get; set; }

    [Parameter] public EventCallback<EmbedBlockContent> OnUrlSet        { get; set; }
    [Parameter] public EventCallback<(int W, int H)>    OnResized       { get; set; }
    [Parameter] public EventCallback<string?>           OnCaptionSaved  { get; set; }
    [Parameter] public EventCallback                    OnFocused       { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private string              _urlInput           = string.Empty;
    private EmbedProvider       _detectedProvider   = EmbedProvider.Generic;
    private bool                _loading;
    private string?             _error;
    private bool                _replacing;

    private ElementReference    _wrapRef;
    private ElementReference    _captionRef;
    private DotNetObjectReference<TmNotionEmbedBlock>? _dotNetRef;
    private bool                _resizeInitialized;
    private bool                _captionInitialized;
    private bool                _captionDirty;
    private IEmbedBlockContent? _lastContent;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _embedUrl => Content is null
        ? string.Empty
        : EmbedProviderDetector.GetEmbedUrl(Content.Url, Content.Provider);

    private string _sizeStyle
    {
        get
        {
            if (Content is null) return string.Empty;
            var parts = new List<string>();
            if (Content.Width.HasValue)  parts.Add($"width:{Content.Width}px;max-width:100%");
            if (Content.Height.HasValue) parts.Add($"height:{Content.Height}px");
            return string.Join(";", parts);
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent        = Content;
        _resizeInitialized  = false;
        _captionInitialized = false;
        _captionDirty       = false;

        if (Content is not null && !_replacing)
        {
            _urlInput         = Content.Url;
            _detectedProvider = Content.Provider;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ReadOnly || string.IsNullOrEmpty(Content?.Url)) return;

        if (!_resizeInitialized)
        {
            _resizeInitialized = true;
            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);
            try { await JS.InvokeVoidAsync("tmNotionEditor.initResizeHandle", _wrapRef, _dotNetRef); }
            catch { }
        }

        if (!_captionInitialized)
        {
            _captionInitialized = true;
            if (!string.IsNullOrEmpty(Content?.Caption))
            {
                try { await JS.InvokeVoidAsync("tmNotionEditor.setHtml", _captionRef, Content.Caption); }
                catch { }
            }
        }
    }

    // ── URL input ─────────────────────────────────────────────────────────────

    private void HandleUrlInput(ChangeEventArgs e)
    {
        _urlInput = e.Value?.ToString() ?? string.Empty;
        _error    = null;

        if (!string.IsNullOrWhiteSpace(_urlInput))
            _detectedProvider = EmbedProviderDetector.Detect(_urlInput.Trim());
        else
            _detectedProvider = EmbedProvider.Generic;
    }

    private async Task HandleUrlKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_urlInput) && !_loading)
            await ConfirmEmbedAsync();
        else if (e.Key == "Escape")
        {
            _replacing = false;
            StateHasChanged();
        }
    }

    private async Task ConfirmEmbedAsync()
    {
        var url = _urlInput.Trim();
        if (string.IsNullOrEmpty(url)) return;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            _error = Loc["TmNotionEmbedBlock_ErrorInvalidUrl"];
            return;
        }

        _loading = true;
        StateHasChanged();

        try
        {
            var provider = EmbedProviderDetector.Detect(url);
            var embed = new EmbedBlockContent
            {
                Url      = url,
                Provider = provider,
                Height   = Content?.Height ?? 400,
                Width    = Content?.Width,
                Caption  = Content?.Caption
            };
            _replacing = false;
            await OnUrlSet.InvokeAsync(embed);
        }
        catch
        {
            _error = Loc["TmNotionEmbedBlock_ErrorGeneric"];
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private Task StartReplaceAsync()
    {
        _replacing          = true;
        _urlInput           = Content?.Url ?? string.Empty;
        _detectedProvider   = Content?.Provider ?? EmbedProvider.Generic;
        _error              = null;
        _resizeInitialized  = false;
        _captionInitialized = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ── JS callbacks ──────────────────────────────────────────────────────────

    [JSInvokable]
    public async Task OnResize(int width, int height) =>
        await OnResized.InvokeAsync((width, height));

    // ── Caption ───────────────────────────────────────────────────────────────

    private async Task OnCaptionBlurAsync()
    {
        if (!_captionDirty || ReadOnly) return;
        _captionDirty = false;
        try
        {
            var html = await JS.InvokeAsync<string>("tmNotionEditor.getHtml", _captionRef);
            await OnCaptionSaved.InvokeAsync(string.IsNullOrWhiteSpace(html) ? null : html);
        }
        catch { }
    }

    // ── Focus ─────────────────────────────────────────────────────────────────

    private async Task HandleFocusAsync() => await OnFocused.InvokeAsync();

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_resizeInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyResizeHandle", _wrapRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
