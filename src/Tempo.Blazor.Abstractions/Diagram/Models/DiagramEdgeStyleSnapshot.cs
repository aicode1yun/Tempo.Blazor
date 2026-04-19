namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Immutable snapshot of edge style properties for undo/redo.</summary>
public sealed class DiagramEdgeStyleSnapshot
{
    public string StartArrow { get; set; } = "none";
    public string EndArrow { get; set; } = "classic";
    public string Shape { get; set; } = "connector";
    public double? StartArrowSize { get; set; }
    public double? EndArrowSize { get; set; }
    public bool? StartArrowFill { get; set; }
    public bool? EndArrowFill { get; set; }
    public bool Rounded { get; set; }
    public double? ArcSize { get; set; }
    public string? JumpStyle { get; set; }
    public double? JumpSize { get; set; }
    public double? SourceSpacing { get; set; }
    public double? TargetSpacing { get; set; }
    public double? SourceEdgeT { get; set; }
    public double? TargetEdgeT { get; set; }
    public double LabelPositionT { get; set; } = 0.5;
    public string? SourceCardinality { get; set; }
    public string? TargetCardinality { get; set; }
    public string? LayerId { get; set; }
    public DiagramStyle Style { get; set; } = new();

    /// <summary>Creates a snapshot from the current state of an edge.</summary>
    public static DiagramEdgeStyleSnapshot FromEdge(DiagramEdge edge) => new()
    {
        StartArrow = edge.StartArrow,
        EndArrow = edge.EndArrow,
        Shape = edge.Shape,
        StartArrowSize = edge.StartArrowSize,
        EndArrowSize = edge.EndArrowSize,
        StartArrowFill = edge.StartArrowFill,
        EndArrowFill = edge.EndArrowFill,
        Rounded = edge.Rounded,
        ArcSize = edge.ArcSize,
        JumpStyle = edge.JumpStyle,
        JumpSize = edge.JumpSize,
        SourceSpacing = edge.SourceSpacing,
        TargetSpacing = edge.TargetSpacing,
        SourceEdgeT = edge.SourceEdgeT,
        TargetEdgeT = edge.TargetEdgeT,
        LabelPositionT = edge.LabelPositionT,
        SourceCardinality = edge.SourceCardinality,
        TargetCardinality = edge.TargetCardinality,
        LayerId = edge.LayerId,
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
            Radius = edge.Style.Radius,
        }
    };

    /// <summary>Applies this snapshot to an edge.</summary>
    public void ApplyTo(DiagramEdge edge)
    {
        edge.StartArrow = StartArrow;
        edge.EndArrow = EndArrow;
        edge.Shape = Shape;
        edge.StartArrowSize = StartArrowSize;
        edge.EndArrowSize = EndArrowSize;
        edge.StartArrowFill = StartArrowFill;
        edge.EndArrowFill = EndArrowFill;
        edge.Rounded = Rounded;
        edge.ArcSize = ArcSize;
        edge.JumpStyle = JumpStyle;
        edge.JumpSize = JumpSize;
        edge.SourceSpacing = SourceSpacing;
        edge.TargetSpacing = TargetSpacing;
        edge.SourceEdgeT = SourceEdgeT;
        edge.TargetEdgeT = TargetEdgeT;
        edge.LabelPositionT = LabelPositionT;
        edge.SourceCardinality = SourceCardinality;
        edge.TargetCardinality = TargetCardinality;
        edge.LayerId = LayerId;
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
            Radius = Style.Radius,
        };
    }
}
