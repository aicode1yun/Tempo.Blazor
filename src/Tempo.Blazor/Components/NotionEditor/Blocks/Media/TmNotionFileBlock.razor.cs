using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Media;

public partial class TmNotionFileBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IFileBlockContent? Content  { get; set; }
    [Parameter] public bool               ReadOnly { get; set; }

    [Parameter] public EventCallback<(string? FileId, string? Url)> OnMediaSet       { get; set; }
    [Parameter] public EventCallback<string?> OnCaptionSaved    { get; set; }
    [Parameter] public EventCallback          OnDeleteRequested { get; set; }
    [Parameter] public EventCallback          OnFocused         { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference   _captionRef;
    private bool               _captionInitialized;
    private bool               _captionDirty;
    private IFileBlockContent? _lastContent;
    private bool               _dialogOpen;

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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
