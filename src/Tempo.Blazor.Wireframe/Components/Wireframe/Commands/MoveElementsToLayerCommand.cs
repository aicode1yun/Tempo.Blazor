using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Undoable command that moves selected elements to a different layer.</summary>
public sealed class MoveElementsToLayerCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string[] _elementIds;
    private readonly string _targetLayerId;
    private readonly Dictionary<string, string?> _previousLayerIds = new();

    public string Name => "Move elements to layer";

    public MoveElementsToLayerCommand(WireframeDocument doc, string[] elementIds, string targetLayerId)
    {
        _doc = doc;
        _elementIds = elementIds;
        _targetLayerId = targetLayerId;
    }

    public void Execute()
    {
        var page = _doc.ActivePage;
        if (page is null) return;

        _previousLayerIds.Clear();
        foreach (var el in page.Elements.Where(e => _elementIds.Contains(e.Id)))
        {
            _previousLayerIds[el.Id] = el.LayerId;
            el.LayerId = _targetLayerId;
        }
    }

    public void Undo()
    {
        var page = _doc.ActivePage;
        if (page is null) return;

        foreach (var el in page.Elements.Where(e => _previousLayerIds.ContainsKey(e.Id)))
        {
            el.LayerId = _previousLayerIds[el.Id];
        }
    }
}
