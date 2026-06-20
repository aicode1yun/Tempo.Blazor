using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Adds a connector between two elements.</summary>
public sealed class AddConnectorCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly WireframeConnector _connector;

    public AddConnectorCommand(WireframeDocument doc, WireframeConnector connector)
    {
        _doc = doc;
        _connector = connector;
    }

    public string Name => "Add connector";

    public void Execute() => _doc.Connectors.Add(_connector);

    public void Undo() => _doc.Connectors.RemoveAll(c => c.Id == _connector.Id);
}
