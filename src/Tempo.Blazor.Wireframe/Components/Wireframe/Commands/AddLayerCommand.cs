using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Undoable command that adds a new layer to the active page.</summary>
public sealed class AddLayerCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly WireframeLayer _layer;

    public string Name => "Add layer";

    public AddLayerCommand(WireframeDocument doc, WireframeLayer layer)
    {
        _doc = doc;
        _layer = layer;
    }

    public void Execute()
    {
        var page = _doc.ActivePage;
        if (page is null) return;
        _layer.Order = page.Layers.Count > 0 ? page.Layers.Max(l => l.Order) + 1 : 0;
        page.Layers.Add(_layer);
        page.ActiveLayerId = _layer.Id;
    }

    public void Undo()
    {
        var page = _doc.ActivePage;
        if (page is null) return;
        page.Layers.RemoveAll(l => l.Id == _layer.Id);
        if (page.ActiveLayerId == _layer.Id)
        {
            page.ActiveLayerId = page.Layers.OrderBy(l => l.Order).FirstOrDefault()?.Id;
        }
    }
}
