using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Media;

public partial class TmNotionImageBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IImageBlockContent? Content  { get; set; }
    [Parameter] public bool                ReadOnly { get; set; }

    /// <summary>Raised when the user sets a new image (URL or uploaded file).</summary>
    [Parameter] public EventCallback<(string? FileId, string? Url)> OnMediaSet         { get; set; }

    /// <summary>Raised when resize drag ends. Arg = new pixel width.</summary>
    [Parameter] public EventCallback<int>     OnWidthChanged      { get; set; }

    /// <summary>Raised on caption blur when text changed. Null = empty.</summary>
    [Parameter] public EventCallback<string?> OnCaptionSaved      { get; set; }

    /// <summary>Raised when alignment changes.</summary>
    [Parameter] public EventCallback<MediaAlignment> OnAlignmentChanged { get; set; }

    /// <summary>Raised when the block requests deletion.</summary>
    [Parameter] public EventCallback OnDeleteRequested { get; set; }

    /// <summary>Raised when the component receives focus.</summary>
    [Parameter] public EventCallback OnFocused { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                          _imgWrapRef;
    private ElementReference                          _captionRef;
    private DotNetObjectReference<TmNotionImageBlock>? _dotNetRef;
    private bool                                      _resizeInitialized;
    private bool                                      _captionDirty;
    private bool                                      _captionInitialized;
    private IImageBlockContent?                       _lastContent;
    private bool                                      _dialogOpen;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _alignClass => Content?.Alignment switch
    {
        MediaAlignment.Left      => "tm-notion-image-block--left",
        MediaAlignment.FullWidth => "tm-notion-image-block--full",
        _                        => "tm-notion-image-block--center"
    };

    private string _widthStyle =>
        Content?.Width.HasValue == true && Content.Alignment != MediaAlignment.FullWidth
            ? $"width:{Content.Width}px;max-width:100%"
            : string.Empty;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent         = Content;
        _resizeInitialized   = false;
        _captionInitialized  = false;
        _captionDirty        = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ReadOnly || string.IsNullOrEmpty(Content?.Url)) return;

        if (!_resizeInitialized && Content?.Alignment != MediaAlignment.FullWidth)
        {
            _resizeInitialized = true;
            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);
            try { await JS.InvokeVoidAsync("tmNotionEditor.initResizeHandle", _imgWrapRef, _dotNetRef); }
            catch { }
        }

        if (!_captionInitialized)
        {
            _captionInitialized = true;
            var caption = Content?.Caption ?? string.Empty;
            if (!string.IsNullOrEmpty(caption))
            {
                try { await JS.InvokeVoidAsync("tmNotionEditor.setHtml", _captionRef, caption); }
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

    private Task OpenDialogAsync()
    {
        _dialogOpen = true;
        return Task.CompletedTask;
    }

    private Task CloseDialogAsync()
    {
        _dialogOpen = false;
        return Task.CompletedTask;
    }

    private async Task HandleMediaSetAsync((string? FileId, string? Url) media)
    {
        _dialogOpen = false;
        await OnMediaSet.InvokeAsync(media);
    }

    // ── Alignment ─────────────────────────────────────────────────────────────

    private async Task HandleAlignmentAsync(MediaAlignment alignment) =>
        await OnAlignmentChanged.InvokeAsync(alignment);

    // ── Focus ─────────────────────────────────────────────────────────────────

    private async Task HandleFocusAsync() => await OnFocused.InvokeAsync();

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_resizeInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyResizeHandle", _imgWrapRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
