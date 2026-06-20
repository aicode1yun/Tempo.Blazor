using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using SkiaSharp;
using Svg.Skia;
using Tempo.Blazor.Abstractions.Wireframe.Export;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Server-side export service for wireframes.
/// Receives a clean SVG string from the client and produces PNG (Skia) or PDF (QuestPDF).
/// </summary>
public sealed class WireframeExportService
{
    /// <summary>Rasterise SVG → PNG via SkiaSharp.</summary>
    public Task<byte[]> ExportPngAsync(WireframeExportRequest request, CancellationToken cancellationToken = default)
    {
        var svg = EnsureSvgNamespace(request.Svg);
        var svgBytes = Encoding.UTF8.GetBytes(svg);

        double baseWidth = ExtractSvgWidth(svg);
        double baseHeight = ExtractSvgHeight(svg);
        int scale = Math.Clamp(request.Options.Scale, 1, 4);
        int width = (int)Math.Ceiling(baseWidth * scale);
        int height = (int)Math.Ceiling(baseHeight * scale);

        using var stream = new MemoryStream(svgBytes);
        using var skSvg = new SKSvg();
        skSvg.Load(stream);

        var info = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(info);
        using var canvas = surface.Canvas;

        // Background
        if (request.Options.IncludeBackground)
        {
            var bg = ParseColor(request.Options.BackgroundColor) ?? SKColors.White;
            canvas.Clear(bg);
        }
        else
        {
            canvas.Clear(SKColors.Transparent);
        }

        if (skSvg.Picture is not null)
        {
            if (scale > 1)
                canvas.Scale((float)scale);
            canvas.DrawPicture(skSvg.Picture);
        }
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Task.FromResult(data.ToArray());
    }

    /// <summary>Embed SVG → single-page PDF via QuestPDF.</summary>
    public Task<byte[]> ExportPdfAsync(WireframeExportRequest request, CancellationToken cancellationToken = default)
    {
        var svg = EnsureSvgNamespace(request.Svg);
        double width = ExtractSvgWidth(svg);
        double height = ExtractSvgHeight(svg);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(new PageSize((float)width, (float)height));
                page.Margin(0);
                page.PageColor(request.Options.IncludeBackground
                    ? (ParseQuestColor(request.Options.BackgroundColor) ?? Colors.White)
                    : Colors.White);
                page.Content().Svg(svg);
            });
        });

        return Task.FromResult(pdf.GeneratePdf());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string EnsureSvgNamespace(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg)) return """<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600"></svg>""";
        if (!svg.Contains("xmlns="))
            svg = svg.Replace("<svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"");
        return svg;
    }

    private static double ExtractSvgWidth(string svg)
    {
        var start = svg.IndexOf("width=\"", StringComparison.Ordinal);
        if (start < 0) return 800;
        start += 7;
        var end = svg.IndexOf('"', start);
        if (end < 0) return 800;
        return double.TryParse(svg[start..end], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 800;
    }

    private static double ExtractSvgHeight(string svg)
    {
        var start = svg.IndexOf("height=\"", StringComparison.Ordinal);
        if (start < 0) return 600;
        start += 8;
        var end = svg.IndexOf('"', start);
        if (end < 0) return 600;
        return double.TryParse(svg[start..end], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) ? h : 600;
    }

    private static SKColor? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        hex = hex.Trim();
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length == 6)
        {
            if (byte.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
                && byte.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
                && byte.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return new SKColor(r, g, b);
        }
        return null;
    }

    private static string? ParseQuestColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        hex = hex.Trim();
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length == 6) return $"#{hex}";
        return null;
    }
}
