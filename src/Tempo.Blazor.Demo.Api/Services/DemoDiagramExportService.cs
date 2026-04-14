using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using SkiaSharp;
using Svg.Skia;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Server-side implementation of <see cref="IDiagramExportService"/>.
/// Supports SVG (pure vector), PNG (via Skia rasterisation) and PDF (via QuestPDF).</summary>
public sealed class DemoDiagramExportService : IDiagramExportService
{
    private readonly DemoDiagramStencilRegistry _stencilRegistry = new();

    public Task<byte[]> ExportPngAsync(DiagramDocument document, DiagramExportOptions options, CancellationToken cancellationToken = default)
    {
        var svg = DiagramExportSvgBuilder.Build(document, options, _stencilRegistry);
        var svgBytes = Encoding.UTF8.GetBytes(svg);

        double width = ExtractSvgWidth(svg);
        double height = ExtractSvgHeight(svg);

        using var stream = new MemoryStream(svgBytes);
        using var skSvg = new SKSvg();
        skSvg.Load(stream);

        var info = new SKImageInfo((int)Math.Ceiling(width), (int)Math.Ceiling(height));
        using var surface = SKSurface.Create(info);
        using var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        if (skSvg.Picture is not null)
        {
            canvas.DrawPicture(skSvg.Picture);
        }
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Task.FromResult(data.ToArray());
    }

    public Task<byte[]> ExportPdfAsync(DiagramDocument document, DiagramExportOptions options, CancellationToken cancellationToken = default)
    {
        var svg = DiagramExportSvgBuilder.Build(document, options, _stencilRegistry);

        double width = ExtractSvgWidth(svg);
        double height = ExtractSvgHeight(svg);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(new PageSize((float)width, (float)height));
                page.Margin(0);
                page.PageColor(Colors.White);
                page.Content().Svg(svg);
            });
        });

        return Task.FromResult(pdf.GeneratePdf());
    }

    public Task<string> ExportSvgAsync(DiagramDocument document, DiagramExportOptions options, CancellationToken cancellationToken = default)
    {
        var svg = DiagramExportSvgBuilder.Build(document, options, _stencilRegistry);
        return Task.FromResult(svg);
    }

    private static double ExtractSvgWidth(string svg)
    {
        var start = svg.IndexOf("width=\"", StringComparison.Ordinal);
        if (start < 0) return 800;
        start += 7;
        var end = svg.IndexOf('"', start);
        if (end < 0) return 800;
        return double.TryParse(svg[start..end], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 800;
    }

    private static double ExtractSvgHeight(string svg)
    {
        var start = svg.IndexOf("height=\"", StringComparison.Ordinal);
        if (start < 0) return 600;
        start += 8;
        var end = svg.IndexOf('"', start);
        if (end < 0) return 600;
        return double.TryParse(svg[start..end], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : 600;
    }
}
