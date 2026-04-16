using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Applies the copied style from <see cref="DiagramClipboard"/> to the selected nodes (undoable).</summary>
public sealed class PasteStyleCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string[] _nodeIds;
    private readonly List<(string Id, DiagramStyle Old, DiagramStyle New)> _snapshots = [];

    public PasteStyleCommand(DiagramDocument doc, string[] nodeIds)
    {
        _doc = doc;
        _nodeIds = nodeIds;
    }

    public string Name => "Paste style";

    public void Execute()
    {
        if (DiagramClipboard.Style is null) return;
        var newStyle = CloneStyle(DiagramClipboard.Style);
        foreach (var id in _nodeIds)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            _snapshots.Add((id, CloneStyle(node.Style), newStyle));
            node.Style = CloneStyle(newStyle);
        }
    }

    public void Undo()
    {
        foreach (var (id, old, _) in _snapshots)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            node.Style = CloneStyle(old);
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
