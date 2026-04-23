namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Maps <see cref="DiagramStencilLayout.BackgroundShape"/> strings to <see cref="DiagramShapeKind"/>.</summary>
public static class DiagramShapeKindExtensions
{
    /// <summary>
    /// Returns the native SVG primitive kind for this stencil layout, or
    /// <see cref="DiagramShapeKind.Custom"/> when the background requires the
    /// HTML/CSS fallback (custom <c>ShapeSvg</c>, or a complex background like
    /// cylinder/cloud/actor).
    /// </summary>
    public static DiagramShapeKind GetNativeShapeKind(this DiagramStencilLayout? layout)
    {
        if (layout is null) return DiagramShapeKind.Custom;
        // Note: we intentionally ignore ShapeSvg when BackgroundShape is one of
        // the simple primitives below. Every built-in stencil ships a ShapeSvg
        // that mirrors its BackgroundShape (for legacy HTML-based rendering);
        // a native SVG primitive is cleaner, faster, and export-friendly. For
        // genuinely custom geometry the stencil author uses BackgroundShape
        // values we don't map (e.g. "cylinder", "actor", "cube" …), which fall
        // through to Custom and keep the HTML/ShapeSvg fallback.
        return (layout.BackgroundShape ?? "rectangle").ToLowerInvariant() switch
        {
            "rectangle" => DiagramShapeKind.Rectangle,
            "rounded" => DiagramShapeKind.RoundedRectangle,
            "ellipse" => DiagramShapeKind.Ellipse,
            "circle" => DiagramShapeKind.Ellipse,
            "diamond" => DiagramShapeKind.Diamond,
            "triangle" => DiagramShapeKind.Triangle,
            "hexagon" => DiagramShapeKind.Hexagon,
            "parallelogram" => DiagramShapeKind.Parallelogram,
            // Everything else (cylinder, cloud, cube, actor, star, sticky-note,
            // document, component, double-ellipse, pentagon, half-ellipse, note,
            // pool, package, lollipop, weak-entity, trapezoid, …) falls back to
            // the HTML/CSS rendering path until stencil-specific SVG paths land.
            _ => DiagramShapeKind.Custom,
        };
    }
}
