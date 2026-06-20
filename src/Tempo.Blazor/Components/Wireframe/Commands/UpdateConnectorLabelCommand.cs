using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Updates the label of a connector.</summary>
public sealed class UpdateConnectorLabelCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _connectorId;
    private readonly string? _beforeLabel;
    private readonly string? _afterLabel;

    public UpdateConnectorLabelCommand(WireframeDocument doc, string connectorId, string? beforeLabel, string? afterLabel)
    {
        _doc = doc;
        _connectorId = connectorId;
        _beforeLabel = beforeLabel;
        _afterLabel = afterLabel;
    }

    public string Name => "Edit connector label";

    public void Execute()
    {
        var c = _doc.Connectors.FirstOrDefault(x => x.Id == _connectorId);
        if (c is null) return;
        c.Label = _afterLabel;
    }

    public void Undo()
    {
        var c = _doc.Connectors.FirstOrDefault(x => x.Id == _connectorId);
        if (c is null) return;
        c.Label = _beforeLabel;
    }
}
