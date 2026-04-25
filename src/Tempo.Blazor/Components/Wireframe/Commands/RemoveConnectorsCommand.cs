using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Removes one or more connectors from the document.</summary>
public sealed class RemoveConnectorsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<WireframeConnector> _removed;

    public RemoveConnectorsCommand(WireframeDocument doc, IEnumerable<string> ids)
    {
        _doc = doc;
        var idSet = ids.ToHashSet();
        _removed = doc.Connectors.Where(c => idSet.Contains(c.Id)).ToList();
    }

    public string Name => _removed.Count == 1
        ? "Delete connector"
        : $"Delete {_removed.Count} connectors";

    public void Execute()
    {
        var idSet = _removed.Select(c => c.Id).ToHashSet();
        _doc.Connectors.RemoveAll(c => idSet.Contains(c.Id));
    }

    public void Undo()
    {
        foreach (var c in _removed)
        {
            if (!_doc.Connectors.Any(x => x.Id == c.Id))
                _doc.Connectors.Add(c);
        }
        _doc.Connectors.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
    }
}
