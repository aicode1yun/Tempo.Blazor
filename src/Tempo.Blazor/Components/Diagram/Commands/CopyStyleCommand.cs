using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Copies the style and optionally size of a single node to the static clipboard.</summary>
public sealed class CopyStyleCommand
{
    private readonly DiagramNode _node;
    private readonly bool _includeSize;

    public CopyStyleCommand(DiagramNode node, bool includeSize = false)
    {
        _node = node;
        _includeSize = includeSize;
    }

    public void Execute()
    {
        DiagramClipboard.Style = CloneStyle(_node.Style);
        if (_includeSize)
        {
            DiagramClipboard.Width = _node.W;
            DiagramClipboard.Height = _node.H;
        }
    }

    private static DiagramStyle CloneStyle(DiagramStyle source) => new()
    {
        Fill = source.Fill,
        Stroke = source.Stroke,
        StrokeWidth = source.StrokeWidth,
        StrokeDasharray = source.StrokeDasharray,
        StrokeDashPattern = source.StrokeDashPattern,
        Color = source.Color,
        FontFamily = source.FontFamily,
        FontSize = source.FontSize,
        Opacity = source.Opacity,
        Radius = source.Radius,
        TextAlign = source.TextAlign,
        VerticalAlign = source.VerticalAlign,
        IsBold = source.IsBold,
        IsItalic = source.IsItalic,
        IsUnderline = source.IsUnderline,
        HasShadow = source.HasShadow,
    };
}
