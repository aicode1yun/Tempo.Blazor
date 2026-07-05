using System.Globalization;
using System.Security;
using System.Text;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>Renders deterministic low-fidelity SVG thumbnails for wireframe documents.</summary>
public static class WireframeThumbnailRenderer
{
    private static readonly string[] DangerousTextFragments = ["javascript:", "vbscript:"];

    public const double DefaultWidth = 160;
    public const double DefaultHeight = 120;

    /// <summary>Renders the active page of <paramref name="document"/> as a thumbnail SVG.</summary>
    public static string Render(
        WireframeDocument document,
        double width = DefaultWidth,
        double height = DefaultHeight)
        => Render(document.ActivePage, document.Title, width, height);

    /// <summary>Renders <paramref name="page"/> as a thumbnail SVG.</summary>
    public static string Render(
        WireframePage? page,
        string? title = null,
        double width = DefaultWidth,
        double height = DefaultHeight)
    {
        var thumbnailWidth = width > 0 ? width : DefaultWidth;
        var thumbnailHeight = height > 0 ? height : DefaultHeight;
        var pageWidth = page?.Width is > 0 ? page.Width : 1280;
        var pageHeight = page?.Height is > 0 ? page.Height : 800;
        var elements = page?.Elements ?? [];
        var scaleX = thumbnailWidth / pageWidth;
        var scaleY = thumbnailHeight / pageHeight;

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(thumbnailWidth)} {F(thumbnailHeight)}\" width=\"{F(thumbnailWidth)}\" height=\"{F(thumbnailHeight)}\" data-elements=\"{elements.Count}\">");
        sb.Append(CultureInfo.InvariantCulture,
            $"<rect width=\"{F(thumbnailWidth)}\" height=\"{F(thumbnailHeight)}\" rx=\"8\" fill=\"#eef2ff\" stroke=\"#6366f1\" stroke-width=\"2\"/>");

        foreach (var element in elements)
        {
            var x = element.X * scaleX;
            var y = element.Y * scaleY;
            var elementWidth = Math.Max(2, element.W * scaleX);
            var elementHeight = Math.Max(2, element.H * scaleY);
            sb.Append(CultureInfo.InvariantCulture,
                $"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(elementWidth)}\" height=\"{F(elementHeight)}\" rx=\"1.5\" fill=\"#6366f1\" fill-opacity=\"0.5\"/>");
        }

        var safeTitle = SanitizeText(title);
        sb.Append(CultureInfo.InvariantCulture,
            $"<text x=\"{F(thumbnailWidth / 2)}\" y=\"{F(Math.Max(0, thumbnailHeight - 6))}\" font-size=\"9\" text-anchor=\"middle\" fill=\"#4f46e5\">{safeTitle} - {elements.Count}</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string SanitizeText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        var inTag = false;
        foreach (var ch in value)
        {
            if (ch == '<')
            {
                inTag = true;
                continue;
            }

            if (ch == '>')
            {
                inTag = false;
                continue;
            }

            if (!inTag && !char.IsControl(ch))
            {
                sb.Append(ch);
            }
        }

        var sanitized = sb.ToString();
        foreach (var fragment in DangerousTextFragments)
        {
            sanitized = sanitized.Replace(fragment, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return SecurityElement.Escape(sanitized) ?? string.Empty;
    }

    private static string F(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
