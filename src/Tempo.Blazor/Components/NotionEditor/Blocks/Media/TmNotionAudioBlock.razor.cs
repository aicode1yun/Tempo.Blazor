using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Media;

public partial class TmNotionAudioBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IAudioBlockContent? Content  { get; set; }
    [Parameter] public bool                ReadOnly { get; set; }

    [Parameter] public EventCallback<(string? FileId, string? Url)> OnMediaSet       { get; set; }
    [Parameter] public EventCallback<string?> OnCaptionSaved    { get; set; }
    [Parameter] public EventCallback          OnDeleteRequested { get; set; }
    [Parameter] public EventCallback          OnFocused         { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                          _captionRef;
    private bool                                      _captionInitialized;
    private bool                                      _captionDirty;
    private IAudioBlockContent?                       _lastContent;
    private bool                                      _dialogOpen;

    // ── Computed ─────────────────────────────────────────────────────────────

    private bool _isEmbedProvider =>
        Content?.Provider is AudioProvider.SoundCloud or AudioProvider.Spotify;

    private string _embedUrl => Content == null ? string.Empty : BuildEmbedUrl(Content.Provider, Content.Url);

    private static string BuildEmbedUrl(AudioProvider provider, string url) => provider switch
    {
        AudioProvider.SoundCloud => BuildSoundCloudEmbed(url),
        AudioProvider.Spotify    => BuildSpotifyEmbed(url),
        _                        => url
    };

    private static string BuildSoundCloudEmbed(string url)
    {
        var encoded = Uri.EscapeDataString(url);
        return $"https://w.soundcloud.com/player/?url={encoded}&auto_play=false&hide_related=true&show_comments=false&show_user=true&show_reposts=false&visual=true";
    }

    private static string BuildSpotifyEmbed(string url)
    {
        // https://open.spotify.com/track/ID → https://open.spotify.com/embed/track/ID
        return url.Replace("open.spotify.com/", "open.spotify.com/embed/");
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent        = Content;
        _captionInitialized = false;
        _captionDirty       = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ReadOnly || string.IsNullOrEmpty(Content?.Url)) return;

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

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
