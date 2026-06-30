using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>One rendered wireframe page: its identity and dimensions plus the standalone SVG string.</summary>
/// <param name="PageId">The page's stable id (<c>WireframePage.Id</c>).</param>
/// <param name="Name">The page's display name.</param>
/// <param name="Width">Canvas width in pixels.</param>
/// <param name="Height">Canvas height in pixels.</param>
/// <param name="Svg">The rendered SVG markup for the page.</param>
public sealed record WireframePageRender(string PageId, string Name, double Width, double Height, string Svg);

/// <summary>
/// Renders wireframe pages to standalone SVG strings fully headless — no browser, no JS interop —
/// by reusing the live component visuals through <c>WireframePageSvg.BuildFragment</c>.
/// Suitable for server-side preview generation, MCP, and backfill, where no DOM is available.
/// </summary>
public interface IWireframeSvgRenderer
{
    /// <summary>
    /// Renders a single <paramref name="page"/> to an SVG string. Component definitions are resolved
    /// in <paramref name="scope"/> (pass the app scope to resolve app-scoped custom components).
    /// Always returns a valid <c>&lt;svg&gt;</c> — an empty page yields a sized placeholder, never null.
    /// </summary>
    Task<string> RenderPageAsync(
        WireframePage page,
        WireframeComponentScope? scope = null,
        WireframePageSvgOptions? options = null);

    /// <summary>
    /// Renders every page of <paramref name="document"/> in document order, returning one
    /// <c>WireframePageRender</c> per page (page metadata + its SVG).
    /// </summary>
    Task<IReadOnlyList<WireframePageRender>> RenderDocumentAsync(
        WireframeDocument document,
        WireframeComponentScope? scope = null,
        WireframePageSvgOptions? options = null);
}
