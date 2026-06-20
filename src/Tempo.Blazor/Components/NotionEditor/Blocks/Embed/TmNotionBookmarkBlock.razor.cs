using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Embed;

public partial class TmNotionBookmarkBlock : ComponentBase
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IBookmarkBlockContent? Content         { get; set; }
    [Parameter] public bool                   ReadOnly        { get; set; }
    [Parameter] public INotionBookmarkProvider? BookmarkProvider { get; set; }

    [Parameter] public EventCallback<BookmarkBlockContent> OnResolved      { get; set; }
    [Parameter] public EventCallback<string?>              OnCaptionSaved  { get; set; }
    [Parameter] public EventCallback                       OnFocused       { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private string              _urlInput           = string.Empty;
    private bool                _resolving;
    private string?             _error;
    private ElementReference    _captionRef;
    private bool                _captionDirty;
    private bool                _captionInitialized;
    private IBookmarkBlockContent? _lastContent;

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
        if (!_captionInitialized && !string.IsNullOrEmpty(Content?.Caption))
        {
            _captionInitialized = true;
            try { await JS.InvokeVoidAsync("tmNotionEditor.setHtml", _captionRef, Content.Caption); }
            catch { }
        }
    }

    // ── URL input ─────────────────────────────────────────────────────────────

    private void HandleUrlInput(ChangeEventArgs e)
    {
        _urlInput = e.Value?.ToString() ?? string.Empty;
        _error    = null;
    }

    private async Task HandleUrlKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_urlInput) && !_resolving)
            await ResolveAsync();
    }

    private async Task ResolveAsync()
    {
        var url = _urlInput.Trim();
        if (string.IsNullOrEmpty(url)) return;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        _resolving = true;
        _error     = null;
        StateHasChanged();

        try
        {
            BookmarkBlockContent resolved;
            if (BookmarkProvider is not null)
            {
                var result = await BookmarkProvider.ResolveBookmarkAsync(url);
                resolved = new BookmarkBlockContent
                {
                    Url          = result.Url,
                    Title        = result.Title,
                    Description  = result.Description,
                    CoverImageUrl = result.CoverImageUrl,
                    FaviconUrl   = result.FaviconUrl,
                    Domain       = result.Domain,
                    Caption      = result.Caption
                };
            }
            else
            {
                // No provider — store URL only; host is used as domain
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    throw new UriFormatException("Invalid URL");

                resolved = new BookmarkBlockContent
                {
                    Url    = url,
                    Domain = uri.Host
                };
            }

            await OnResolved.InvokeAsync(resolved);
        }
        catch (Exception ex)
        {
            _error = ex is UriFormatException
                ? Loc["TmNotionBookmarkBlock_ErrorInvalidUrl"]
                : Loc["TmNotionBookmarkBlock_ErrorResolve"];
        }
        finally
        {
            _resolving = false;
            StateHasChanged();
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

    // ── Focus ─────────────────────────────────────────────────────────────────

    private async Task HandleFocusAsync() => await OnFocused.InvokeAsync();
}
