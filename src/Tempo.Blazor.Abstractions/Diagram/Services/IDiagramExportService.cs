using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Exports a <see cref="DiagramDocument"/> to various output formats.</summary>
/// <remarks>Concrete implementations are provided by the consuming application (e.g. server-side renderer).</remarks>
public interface IDiagramExportService
{
    /// <summary>Exports the diagram as a PNG image.</summary>
    Task<byte[]> ExportPngAsync(DiagramDocument document, DiagramExportOptions options, CancellationToken cancellationToken = default);

    /// <summary>Exports the diagram as a PDF document.</summary>
    Task<byte[]> ExportPdfAsync(DiagramDocument document, DiagramExportOptions options, CancellationToken cancellationToken = default);

    /// <summary>Exports the diagram as an SVG vector graphic string.</summary>
    Task<string> ExportSvgAsync(DiagramDocument document, DiagramExportOptions options, CancellationToken cancellationToken = default);
}

/// <summary>Options controlling diagram export output.</summary>
public sealed class DiagramExportOptions
{
    /// <summary>Output width in pixels. Null = auto-fit.</summary>
    public double? Width { get; set; }

    /// <summary>Output height in pixels. Null = auto-fit.</summary>
    public double? Height { get; set; }

    /// <summary>Background color. Null = transparent.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Padding around the diagram content in pixels.</summary>
    public double Padding { get; set; } = 20;

    /// <summary>Whether to include the grid in the exported image.</summary>
    public bool IncludeGrid { get; set; }

    /// <summary>Zero-based index of the page to export. Null = active page.</summary>
    public int? PageIndex { get; set; }

    /// <summary>When true and exporting to PDF, all pages are exported as separate PDF pages.</summary>
    public bool ExportAllPages { get; set; }
}
