using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Stencils;

/// <summary>Creates and updates diagram edges from edge stencil definitions.</summary>
public static class DiagramEdgeStencilFactory
{
    /// <summary>Creates a diagram edge and applies the stencil edge defaults.</summary>
    public static DiagramEdge CreateEdge(
        DiagramStencil stencil,
        string? sourceNodeId = null,
        string? sourcePortId = null,
        string? targetNodeId = null,
        string? targetPortId = null,
        DiagramPoint? sourcePoint = null,
        DiagramPoint? targetPoint = null)
    {
        ArgumentNullException.ThrowIfNull(stencil);

        var edge = new DiagramEdge
        {
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId,
            SourcePoint = sourcePoint,
            TargetPoint = targetPoint
        };

        ApplyDefaults(edge, stencil);
        return edge;
    }

    /// <summary>Applies edge stencil defaults to an existing diagram edge.</summary>
    public static void ApplyDefaults(DiagramEdge edge, DiagramStencil stencil)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(stencil);

        if (stencil.Kind != DiagramStencilKind.Edge || stencil.EdgeDefaults is null)
            return;

        var defaults = stencil.EdgeDefaults;
        edge.Routing = defaults.Routing;
        edge.ConnectorType = defaults.ConnectorType;
        edge.Shape = defaults.Shape;
        edge.StartArrow = defaults.StartArrow;
        edge.EndArrow = defaults.EndArrow;
        edge.StartArrowSize = defaults.StartArrowSize;
        edge.EndArrowSize = defaults.EndArrowSize;
        edge.StartArrowFill = defaults.StartArrowFill;
        edge.EndArrowFill = defaults.EndArrowFill;
        edge.Rounded = defaults.Rounded;
        edge.CubicBezier = defaults.CubicBezier;
        edge.ArcSize = defaults.ArcSize;

        if (defaults.Style is not null)
        {
            edge.Style = CopyStyle(defaults.Style);
        }
    }

    private static DiagramStyle CopyStyle(DiagramStyle style) => new()
    {
        Fill = style.Fill,
        Stroke = style.Stroke,
        StrokeWidth = style.StrokeWidth,
        StrokeDashPattern = style.StrokeDashPattern,
        Color = style.Color,
        FontFamily = style.FontFamily,
        FontSize = style.FontSize,
        Opacity = style.Opacity,
        Radius = style.Radius
    };
}
