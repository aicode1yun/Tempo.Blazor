using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Updates visual style properties of a connector.</summary>
public sealed class UpdateConnectorStyleCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _connectorId;
    private readonly string _beforeStroke;
    private readonly double _beforeStrokeWidth;
    private readonly string? _beforeStrokeDasharray;
    private readonly string _beforeStartArrow;
    private readonly string _beforeEndArrow;
    private readonly string _afterStroke;
    private readonly double _afterStrokeWidth;
    private readonly string? _afterStrokeDasharray;
    private readonly string _afterStartArrow;
    private readonly string _afterEndArrow;

    public UpdateConnectorStyleCommand(
        WireframeDocument doc,
        string connectorId,
        string beforeStroke, double beforeStrokeWidth, string? beforeStrokeDasharray,
        string beforeStartArrow, string beforeEndArrow,
        string afterStroke, double afterStrokeWidth, string? afterStrokeDasharray,
        string afterStartArrow, string afterEndArrow)
    {
        _doc = doc;
        _connectorId = connectorId;
        _beforeStroke = beforeStroke;
        _beforeStrokeWidth = beforeStrokeWidth;
        _beforeStrokeDasharray = beforeStrokeDasharray;
        _beforeStartArrow = beforeStartArrow;
        _beforeEndArrow = beforeEndArrow;
        _afterStroke = afterStroke;
        _afterStrokeWidth = afterStrokeWidth;
        _afterStrokeDasharray = afterStrokeDasharray;
        _afterStartArrow = afterStartArrow;
        _afterEndArrow = afterEndArrow;
    }

    public string Name => "Change connector style";

    public void Execute()
    {
        var c = _doc.Connectors.FirstOrDefault(x => x.Id == _connectorId);
        if (c is null) return;
        c.Stroke = _afterStroke;
        c.StrokeWidth = _afterStrokeWidth;
        c.StrokeDasharray = _afterStrokeDasharray;
        c.StartArrow = _afterStartArrow;
        c.EndArrow = _afterEndArrow;
    }

    public void Undo()
    {
        var c = _doc.Connectors.FirstOrDefault(x => x.Id == _connectorId);
        if (c is null) return;
        c.Stroke = _beforeStroke;
        c.StrokeWidth = _beforeStrokeWidth;
        c.StrokeDasharray = _beforeStrokeDasharray;
        c.StartArrow = _beforeStartArrow;
        c.EndArrow = _beforeEndArrow;
    }
}
