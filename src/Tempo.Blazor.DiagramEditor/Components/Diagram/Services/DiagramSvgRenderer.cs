using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>
/// Headless <see cref="IDiagramSvgRenderer"/> backed by the shared <see cref="DiagramSvgBuilder"/>
/// and the application's <see cref="DiagramStencilRegistry"/>. Registered as a singleton by
/// <c>AddTempoBlazorDiagramEditor()</c>.
/// </summary>
public sealed class DiagramSvgRenderer : IDiagramSvgRenderer
{
    private readonly DiagramStencilRegistry _stencils;

    public DiagramSvgRenderer(DiagramStencilRegistry stencils)
    {
        ArgumentNullException.ThrowIfNull(stencils);
        _stencils = stencils;
    }

    /// <inheritdoc />
    public string RenderSvg(DiagramDocument document, DiagramSvgRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new DiagramSvgRenderOptions();

        document.EnsurePages();
        var page = ResolvePage(document, options.PageIndex);
        var palette = DiagramSvgPalette.ForTheme(options.Theme);

        return DiagramSvgBuilder.Build(page, options.ToExportOptions(), palette, _stencils.GetStencil);
    }

    private static DiagramPage ResolvePage(DiagramDocument document, int? pageIndex)
    {
        if (pageIndex.HasValue && pageIndex.Value >= 0 && pageIndex.Value < document.Pages.Count)
            return document.Pages[pageIndex.Value];
        return document.ActivePage;
    }
}
