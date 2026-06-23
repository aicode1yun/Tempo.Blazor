using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Special;

public partial class TmNotionExcerptBlock : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Owning page block for persistence context.</summary>
    [Parameter] public IPageBlock Block { get; set; } = default!;

    /// <summary>Saved excerpt content.</summary>
    [Parameter] public IExcerptBlockContent? Content { get; set; }

    /// <summary>Whether the excerpt is rendered without editing affordances.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Raised when edited excerpt HTML should be persisted.</summary>
    [Parameter] public EventCallback<ExcerptBlockContent> OnContentChanged { get; set; }

    /// <summary>Raised when the excerpt receives focus.</summary>
    [Parameter] public EventCallback OnFocused { get; set; }

    private ElementReference _editableRef;
    private bool _dirty;
    private string? _lastHtml;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ReadOnly)
        {
            return;
        }

        var html = Content?.Html ?? string.Empty;
        if (firstRender || (!_dirty && !string.Equals(html, _lastHtml, StringComparison.Ordinal)))
        {
            _lastHtml = html;
            try
            {
                await JS.InvokeVoidAsync("tmNotionEditor.setHtml", _editableRef, html);
            }
            catch
            {
            }
        }
    }

    private void HandleInput()
        => _dirty = true;

    private async Task HandleBlurAsync()
    {
        if (!_dirty || ReadOnly)
        {
            return;
        }

        try
        {
            var html = await JS.InvokeAsync<string>("tmNotionEditor.getHtml", _editableRef);
            html = NotionInlineHtmlSanitizer.SanitizeHtmlFragment(html);
            _lastHtml = html;
            _dirty = false;
            await OnContentChanged.InvokeAsync(new ExcerptBlockContent { Html = html });
        }
        catch
        {
            _dirty = false;
        }
    }

    private Task HandleFocusedAsync(MouseEventArgs _)
        => OnFocused.InvokeAsync();

    private Task HandleFocusedAsync(FocusEventArgs _)
        => OnFocused.InvokeAsync();

    private static string Sanitize(string? html)
        => NotionInlineHtmlSanitizer.SanitizeHtmlFragment(html);
}
