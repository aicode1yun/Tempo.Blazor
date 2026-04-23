using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Media;

public partial class TmNotionVideoBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IVideoBlockContent? Content  { get; set; }
    [Parameter] public bool                ReadOnly { get; set; }

    [Parameter] public EventCallback<(string? FileId, string? Url)> OnMediaSet       { get; set; }
    [Parameter] public EventCallback<int>     OnWidthChanged    { get; set; }
    [Parameter] public EventCallback<string?> OnCaptionSaved    { get; set; }
    [Parameter] public EventCallback          OnDeleteRequested { get; set; }
    [Parameter] public EventCallback          OnFocused         { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                          _wrapRef;
    private ElementReference                          _captionRef;
    private DotNetObjectReference<TmNotionVideoBlock>? _dotNetRef;
    private bool                                      _resizeInitialized;
    private bool                                      _captionInitialized;
    private bool                                      _captionDirty;
    private IVideoBlockContent?                       _lastContent;
    private bool                                      _dialogOpen;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _widthStyle =>
        Content?.Width.HasValue == true
            ? $"width:{Content.Width}px;max-width:100%"
            : string.Empty;

    private bool _isEmbedProvider =>
        Content?.Provider is VideoProvider.YouTube
            or VideoProvider.Vimeo
            or VideoProvider.Loom;

    private string _embedUrl => Content == null ? string.Empty : BuildEmbedUrl(Content.Provider, Content.Url);

    private static string BuildEmbedUrl(VideoProvider provider, string url) => provider switch
    {
        VideoProvider.YouTube => BuildYouTubeEmbed(url),
        VideoProvider.Vimeo   => BuildVimeoEmbed(url),
        VideoProvider.Loom    => BuildLoomEmbed(url),
        _                     => url
    };

    private static string BuildYouTubeEmbed(string url)
    {
        // https://youtube.com/watch?v=ID or https://youtu.be/ID
        var id = string.Empty;
        if (url.Contains("youtu.be/"))
            id = url.Split("youtu.be/")[^1].Split('?')[0];
        else if (url.Contains("v="))
            id = url.Split("v=")[^1].Split('&')[0];
        return string.IsNullOrEmpty(id)
            ? url
            : $"https://www.youtube-nocookie.com/embed/{id}";
    }

    private static string BuildVimeoEmbed(string url)
    {
        // https://vimeo.com/ID
        var parts = url.TrimEnd('/').Split('/');
        var id    = parts[^1];
        return long.TryParse(id, out _)
            ? $"https://player.vimeo.com/video/{id}"
            : url;
    }

    private static string BuildLoomEmbed(string url)
    {
        // https://www.loom.com/share/ID → https://www.loom.com/embed/ID
        return url.Replace("/share/", "/embed/");
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent        = Content;
        _resizeInitialized  = false;
        _captionInitialized = false;
        _captionDirty       = false;
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

    // ── JS callback ───────────────────────────────────────────────────────────

    [JSInvokable]
    public async Task OnResize(int width, int height) => await OnWidthChanged.InvokeAsync(width);

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

    // ── Upload dialog ─────────────────────────────────────────────────────────

    private Task OpenDialogAsync()  { _dialogOpen = true;  return Task.CompletedTask; }
    private Task CloseDialogAsync() { _dialogOpen = false; return Task.CompletedTask; }

    private async Task HandleMediaSetAsync((string? FileId, string? Url) media)
    {
        _dialogOpen = false;
        await OnMediaSet.InvokeAsync(media);
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
