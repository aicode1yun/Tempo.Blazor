using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Batch updates style properties of multiple nodes (undoable).</summary>
public sealed class UpdateNodesStyleCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IReadOnlyList<string> _nodeIds;
    private readonly IReadOnlyList<DiagramStyle> _beforeStyles;
    private readonly DiagramStyle _afterStyle;

    public UpdateNodesStyleCommand(DiagramDocument doc, IEnumerable<string> nodeIds, IEnumerable<DiagramStyle> beforeStyles, DiagramStyle afterStyle)
    {
        _doc = doc;
        _nodeIds = nodeIds.ToList();
        _beforeStyles = beforeStyles.ToList();
        _afterStyle = afterStyle;
    }

    public string Name => "Update nodes style";

    public void Execute()
    {
        foreach (var id in _nodeIds)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            ApplyStyle(node.Style, _afterStyle);
        }
    }

    public void Undo()
    {
        for (int i = 0; i < _nodeIds.Count; i++)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeIds[i]);
            if (node is null || i >= _beforeStyles.Count) continue;
            ApplyStyle(node.Style, _beforeStyles[i]);
        }
    }

    private static void ApplyStyle(DiagramStyle target, DiagramStyle source)
    {
        target.Fill = source.Fill;
        target.Stroke = source.Stroke;
        target.StrokeWidth = source.StrokeWidth;
        target.StrokeDasharray = source.StrokeDasharray;
        target.StrokeDashPattern = source.StrokeDashPattern;
        target.Color = source.Color;
        target.FontFamily = source.FontFamily;
        target.FontSize = source.FontSize;
        target.Opacity = source.Opacity;
        target.Radius = source.Radius;
        target.TextAlign = source.TextAlign;
        target.VerticalAlign = source.VerticalAlign;
        target.IsBold = source.IsBold;
        target.IsItalic = source.IsItalic;
        target.IsUnderline = source.IsUnderline;
        target.HasShadow = source.HasShadow;
    }
}
