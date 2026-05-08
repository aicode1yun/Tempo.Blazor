using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Displays a normalized signing document page and optional overlays.</summary>
public partial class TmDocumentPageViewer
{
    /// <summary>Optional id assigned to the rendered page element.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Document page to render. When null, an empty state is shown.</summary>
    [Parameter] public SigningDocumentPage? Page { get; set; }

    /// <summary>Whether to show the loading skeleton instead of the page.</summary>
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>Error message shown instead of the page.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Optional alt text for the page image. Defaults to page label or localized page number.</summary>
    [Parameter] public string? Alt { get; set; }

    /// <summary>Whether overlay content should receive pointer events. Defaults to true.</summary>
    [Parameter] public bool IsOverlayInteractive { get; set; } = true;

    /// <summary>Whether to prevent the browser context menu on the page element. Defaults to true.</summary>
    [Parameter] public bool PreventDefaultContextMenu { get; set; } = true;

    /// <summary>Optional template for replacing the rendered page image.</summary>
    [Parameter] public RenderFragment<SigningDocumentPage>? PageTemplate { get; set; }

    /// <summary>Overlay content rendered over the document page.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Overlay template rendered over the document page with page context.</summary>
    [Parameter] public RenderFragment<SigningDocumentPage>? OverlayTemplate { get; set; }

    /// <summary>Callback invoked when the page is clicked.</summary>
    [Parameter] public EventCallback<TmDocumentPageViewerPointerEventArgs> OnPageClick { get; set; }

    /// <summary>Callback invoked when the page context menu is requested.</summary>
    [Parameter] public EventCallback<TmDocumentPageViewerPointerEventArgs> OnPageContextMenu { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string RootClass => string.Join(" ", new[] { "tm-document-page-viewer", Class }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private string OverlayClass => IsOverlayInteractive
        ? "tm-document-page-viewer__overlay"
        : "tm-document-page-viewer__overlay tm-document-page-viewer__overlay--readonly";

    private string PageElementId => !string.IsNullOrWhiteSpace(Id)
        ? Id
        : $"tm-document-page-{Page?.PageIndex ?? 0}";

    private string PageStyle
    {
        get
        {
            if (Page is null || Page.Width <= 0 || Page.Height <= 0)
            {
                return string.Empty;
            }

            return string.Create(CultureInfo.InvariantCulture, $"aspect-ratio: {Page.Width} / {Page.Height};");
        }
    }

    private string ImageAlt => !string.IsNullOrWhiteSpace(Alt)
        ? Alt
        : Page?.Label ?? Loc["TmDocumentPageViewer_PageAlt", (Page?.PageIndex ?? 0) + 1];

    private string PageAriaLabel => Page?.Label ?? Loc["TmDocumentPageViewer_PageAriaLabel", (Page?.PageIndex ?? 0) + 1];

    private Task HandlePageClickAsync(MouseEventArgs args)
    {
        return Page is null || !OnPageClick.HasDelegate
            ? Task.CompletedTask
            : OnPageClick.InvokeAsync(new TmDocumentPageViewerPointerEventArgs(Page, args));
    }

    private Task HandlePageContextMenuAsync(MouseEventArgs args)
    {
        return Page is null || !OnPageContextMenu.HasDelegate
            ? Task.CompletedTask
            : OnPageContextMenu.InvokeAsync(new TmDocumentPageViewerPointerEventArgs(Page, args));
    }
}
