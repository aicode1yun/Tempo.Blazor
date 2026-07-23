using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>
/// Renders a <see cref="DiagramDocument"/> to a static SVG string on the server, without a
/// browser or JavaScript. Intended for headless export and documentation embedding (the same
/// output the demo export service rasterises to PNG/PDF).
/// </summary>
/// <remarks>
/// The render is deterministic: identical documents and options produce byte-for-byte identical
/// SVG (invariant-culture coordinate formatting, nodes ordered by z-index). Registered as a
/// singleton by <c>AddTempoBlazorDiagramEditor()</c>.
/// </remarks>
public interface IDiagramSvgRenderer
{
    /// <summary>Renders a single diagram page (selected via <see cref="DiagramSvgRenderOptions.PageIndex"/>,
    /// defaulting to the active page) to an SVG string.</summary>
    string RenderSvg(DiagramDocument document, DiagramSvgRenderOptions? options = null);
}

/// <summary>Colour theme for <see cref="IDiagramSvgRenderer"/> output.</summary>
public enum DiagramSvgTheme
{
    /// <summary>Light background with dark strokes/text (default).</summary>
    Light,

    /// <summary>Dark background with light strokes/text.</summary>
    Dark
}

/// <summary>Options controlling headless diagram SVG rendering. All fields are additive and
/// optional; the defaults reproduce the light-theme auto-fit export.</summary>
public sealed class DiagramSvgRenderOptions
{
    /// <summary>Colour theme. Only the <em>default</em> node/edge/text colours switch by theme;
    /// explicit per-node/edge colours (stencil layout or <see cref="DiagramStyle"/>) always win.</summary>
    public DiagramSvgTheme Theme { get; init; } = DiagramSvgTheme.Light;

    /// <summary>Zero-based page index to render. When null, the document's active page is used.</summary>
    public int? PageIndex { get; init; }

    /// <summary>Explicit output width. When both width and height are null the renderer auto-fits
    /// to the node bounds with <see cref="Padding"/>.</summary>
    public double? Width { get; init; }

    /// <summary>Explicit output height. See <see cref="Width"/>.</summary>
    public double? Height { get; init; }

    /// <summary>Padding around auto-fitted content in pixels.</summary>
    public double Padding { get; init; } = 20;

    /// <summary>When true, a dotted grid pattern is drawn behind the diagram.</summary>
    public bool IncludeGrid { get; init; }

    /// <summary>Overrides the theme background colour (any CSS colour). Null uses the theme default.</summary>
    public string? BackgroundColor { get; init; }

    /// <summary>Maps the render options onto the shared <see cref="DiagramExportOptions"/> the builder consumes.</summary>
    public DiagramExportOptions ToExportOptions() => new()
    {
        Width = Width,
        Height = Height,
        Padding = Padding,
        IncludeGrid = IncludeGrid,
        BackgroundColor = BackgroundColor,
        PageIndex = PageIndex
    };
}

/// <summary>Resolved colour palette applied to a diagram SVG render. Only fills in the
/// <em>defaults</em>; explicit stencil/style colours override these per element.</summary>
public sealed class DiagramSvgPalette
{
    /// <summary>Page background colour (used when no explicit background override is supplied).</summary>
    public string Background { get; init; } = "#ffffff";

    /// <summary>Grid dot colour.</summary>
    public string Grid { get; init; } = "#e5e7eb";

    /// <summary>Fill for solid arrow markers (association, composition).</summary>
    public string MarkerSolid { get; init; } = "#111827";

    /// <summary>Fill for hollow arrow markers (inheritance, aggregation).</summary>
    public string MarkerHollowFill { get; init; } = "white";

    /// <summary>Stroke for hollow arrow markers.</summary>
    public string MarkerHollowStroke { get; init; } = "#111827";

    /// <summary>Default node fill when neither stencil layout nor node style specifies one.</summary>
    public string DefaultNodeFill { get; init; } = "#ffffff";

    /// <summary>Default node stroke.</summary>
    public string DefaultNodeStroke { get; init; } = "#111827";

    /// <summary>Section divider line colour.</summary>
    public string Divider { get; init; } = "#e5e7eb";

    /// <summary>Default text colour.</summary>
    public string DefaultText { get; init; } = "#111827";

    /// <summary>Light theme palette (default). Values reproduce the original demo export output.</summary>
    public static DiagramSvgPalette Light { get; } = new();

    /// <summary>Dark theme palette.</summary>
    public static DiagramSvgPalette Dark { get; } = new()
    {
        Background = "#1e1e2e",
        Grid = "#3f3f46",
        MarkerSolid = "#e5e7eb",
        MarkerHollowFill = "#1e1e2e",
        MarkerHollowStroke = "#e5e7eb",
        DefaultNodeFill = "#27272a",
        DefaultNodeStroke = "#e5e7eb",
        Divider = "#3f3f46",
        DefaultText = "#e5e7eb"
    };

    /// <summary>Returns the palette for a theme.</summary>
    public static DiagramSvgPalette ForTheme(DiagramSvgTheme theme)
        => theme == DiagramSvgTheme.Dark ? Dark : Light;
}
