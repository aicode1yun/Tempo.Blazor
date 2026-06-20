namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>
/// Categorises a stencil's background into a native SVG primitive that the
/// unified-SVG canvas (<see cref="TmDiagramCanvas"/>) can render as an
/// <c>&lt;rect&gt;</c>, <c>&lt;ellipse&gt;</c>, <c>&lt;polygon&gt;</c> or
/// <c>&lt;path&gt;</c> directly inside the node <c>&lt;g&gt;</c>.
/// <para>
/// Stencils whose background cannot be cleanly expressed as a single SVG
/// primitive (e.g. <c>cylinder</c>, <c>cloud</c>, <c>actor</c>,
/// <c>sticky-note</c>, <c>cube</c>, complex component/package outlines …)
/// map to <see cref="Custom"/>. For those the canvas keeps the existing
/// HTML/CSS fallback (inside the node's <c>&lt;foreignObject&gt;</c>) and
/// does not emit a native SVG primitive.
/// </para>
/// </summary>
public enum DiagramShapeKind
{
    /// <summary>
    /// Stencil cannot be rendered as a single native SVG primitive — the canvas
    /// keeps the existing HTML/CSS shape (or the stencil's <c>ShapeSvg</c> raw
    /// markup) inside the node's <c>&lt;foreignObject&gt;</c>.
    /// </summary>
    Custom = 0,

    /// <summary>Plain rectangle, mapped to <c>&lt;rect&gt;</c>.</summary>
    Rectangle = 1,

    /// <summary>Rounded rectangle (<c>rx/ry</c> derived from node style or default 8 px).</summary>
    RoundedRectangle = 2,

    /// <summary>Ellipse / circle, mapped to <c>&lt;ellipse&gt;</c>.</summary>
    Ellipse = 3,

    /// <summary>Diamond (rhombus), mapped to <c>&lt;polygon&gt;</c>.</summary>
    Diamond = 4,

    /// <summary>Equilateral up-pointing triangle, mapped to <c>&lt;polygon&gt;</c>.</summary>
    Triangle = 5,

    /// <summary>Regular hexagon (flat-top), mapped to <c>&lt;polygon&gt;</c>.</summary>
    Hexagon = 6,

    /// <summary>Parallelogram slanted right, mapped to <c>&lt;polygon&gt;</c>.</summary>
    Parallelogram = 7,
}
