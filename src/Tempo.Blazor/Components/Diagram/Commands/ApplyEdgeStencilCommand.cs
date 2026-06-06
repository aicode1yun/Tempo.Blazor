using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Applies an edge stencil template to a diagram edge.</summary>
public sealed class ApplyEdgeStencilCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly DiagramStencil _stencil;
    private EdgeTemplateSnapshot? _before;

    public ApplyEdgeStencilCommand(DiagramDocument doc, string edgeId, DiagramStencil stencil)
    {
        _doc = doc;
        _edgeId = edgeId;
        _stencil = stencil;
    }

    public string Name => _stencil.NameResourceKey;

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;

        _before ??= EdgeTemplateSnapshot.FromEdge(edge);
        DiagramEdgeStencilFactory.ApplyDefaults(edge, _stencil);
        _doc.LastUsedEdgeStyle = DiagramEdgeStyleSnapshot.FromEdge(edge);
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null || _before is null) return;

        _before.ApplyTo(edge);
        _doc.LastUsedEdgeStyle = DiagramEdgeStyleSnapshot.FromEdge(edge);
    }

    private sealed class EdgeTemplateSnapshot
    {
        public string Routing { get; init; } = "straight";
        public string ConnectorType { get; init; } = "association";
        public string Shape { get; init; } = "connector";
        public string StartArrow { get; init; } = "none";
        public string EndArrow { get; init; } = "classic";
        public double? StartArrowSize { get; init; }
        public double? EndArrowSize { get; init; }
        public bool? StartArrowFill { get; init; }
        public bool? EndArrowFill { get; init; }
        public bool Rounded { get; init; }
        public bool CubicBezier { get; init; }
        public double? ArcSize { get; init; }
        public DiagramStyle Style { get; init; } = new();

        public static EdgeTemplateSnapshot FromEdge(DiagramEdge edge) => new()
        {
            Routing = edge.Routing,
            ConnectorType = edge.ConnectorType,
            Shape = edge.Shape,
            StartArrow = edge.StartArrow,
            EndArrow = edge.EndArrow,
            StartArrowSize = edge.StartArrowSize,
            EndArrowSize = edge.EndArrowSize,
            StartArrowFill = edge.StartArrowFill,
            EndArrowFill = edge.EndArrowFill,
            Rounded = edge.Rounded,
            CubicBezier = edge.CubicBezier,
            ArcSize = edge.ArcSize,
            Style = new DiagramStyle
            {
                Fill = edge.Style.Fill,
                Stroke = edge.Style.Stroke,
                StrokeWidth = edge.Style.StrokeWidth,
                StrokeDashPattern = edge.Style.StrokeDashPattern,
                Color = edge.Style.Color,
                FontFamily = edge.Style.FontFamily,
                FontSize = edge.Style.FontSize,
                Opacity = edge.Style.Opacity,
                Radius = edge.Style.Radius
            }
        };

        public void ApplyTo(DiagramEdge edge)
        {
            edge.Routing = Routing;
            edge.ConnectorType = ConnectorType;
            edge.Shape = Shape;
            edge.StartArrow = StartArrow;
            edge.EndArrow = EndArrow;
            edge.StartArrowSize = StartArrowSize;
            edge.EndArrowSize = EndArrowSize;
            edge.StartArrowFill = StartArrowFill;
            edge.EndArrowFill = EndArrowFill;
            edge.Rounded = Rounded;
            edge.CubicBezier = CubicBezier;
            edge.ArcSize = ArcSize;
            edge.Style = new DiagramStyle
            {
                Fill = Style.Fill,
                Stroke = Style.Stroke,
                StrokeWidth = Style.StrokeWidth,
                StrokeDashPattern = Style.StrokeDashPattern,
                Color = Style.Color,
                FontFamily = Style.FontFamily,
                FontSize = Style.FontSize,
                Opacity = Style.Opacity,
                Radius = Style.Radius
            };
        }
    }
}
