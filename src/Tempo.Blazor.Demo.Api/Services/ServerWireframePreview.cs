using System.Globalization;
using System.Text;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Renders a small, deterministic server-side SVG thumbnail of a wireframe document so that edits
/// made through MCP (which has no browser to render the real preview) still produce a visible,
/// changing preview for embedded blocks. Includes a <c>data-elements</c> count for assertions.
/// </summary>
public static class ServerWireframePreview
{
    public static string Render(WireframeDocument document)
    {
        var page = document.ActivePage;
        var pw = page?.Width is > 0 ? page.Width : 1280;
        var ph = page?.Height is > 0 ? page.Height : 800;
        var elements = page?.Elements ?? [];

        const double w = 160, h = 120;
        var scaleX = w / pw;
        var scaleY = h / ph;

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(w)} {F(h)}\" width=\"160\" height=\"120\" data-elements=\"{elements.Count}\">");
        sb.Append("<rect width=\"160\" height=\"120\" rx=\"8\" fill=\"#eef2ff\" stroke=\"#6366f1\" stroke-width=\"2\"/>");

        foreach (var el in elements)
        {
            var x = el.X * scaleX;
            var y = el.Y * scaleY;
            var ew = Math.Max(2, el.W * scaleX);
            var eh = Math.Max(2, el.H * scaleY);
            sb.Append(CultureInfo.InvariantCulture,
                $"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(ew)}\" height=\"{F(eh)}\" rx=\"1.5\" fill=\"#6366f1\" fill-opacity=\"0.5\"/>");
        }

        var title = System.Security.SecurityElement.Escape(document.Title);
        sb.Append(CultureInfo.InvariantCulture,
            $"<text x=\"80\" y=\"114\" font-size=\"9\" text-anchor=\"middle\" fill=\"#4f46e5\">{title} · {elements.Count}</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
