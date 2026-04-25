using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Undoable command that removes a layer and moves its elements to the default layer.</summary>
public sealed class RemoveLayerCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _layerId;
    private WireframeLayer? _snapshot;
    private List<string> _movedElementIds = [];
    private string? _previousActiveLayerId;

    public string Name => "Remove layer";

    public RemoveLayerCommand(WireframeDocument doc, string layerId)
    {
        _doc = doc;
        _layerId = layerId;
    }

    public void Execute()
    {
        var page = _doc.ActivePage;
        if (page is null) return;

        var layer = page.Layers.FirstOrDefault(l => l.Id == _layerId);
        if (layer is null) return;

        _snapshot = new WireframeLayer
        {
            Id = layer.Id,
            Name = layer.Name,
            Order = layer.Order,
            IsVisible = layer.IsVisible,
            IsLocked = layer.IsLocked,
        };
        _previousActiveLayerId = page.ActiveLayerId;

        // Move elements to the remaining layer with lowest order
        var targetLayer = page.Layers.Where(l => l.Id != _layerId).OrderBy(l => l.Order).FirstOrDefault();
        if (targetLayer is not null)
        {
            _movedElementIds = page.Elements
                .Where(e => e.LayerId == _layerId)
                .Select(e => e.Id)
                .ToList();
            foreach (var el in page.Elements.Where(e => e.LayerId == _layerId))
            {
                el.LayerId = targetLayer.Id;
            }
        }

        page.Layers.RemoveAll(l => l.Id == _layerId);
        if (page.ActiveLayerId == _layerId)
        {
            page.ActiveLayerId = targetLayer?.Id ?? page.Layers.OrderBy(l => l.Order).FirstOrDefault()?.Id;
        }
    }

    public void Undo()
    {
        var page = _doc.ActivePage;
        if (page is null || _snapshot is null) return;

        page.Layers.Add(_snapshot);
        page.Layers.Sort((a, b) => a.Order.CompareTo(b.Order));
        page.ActiveLayerId = _previousActiveLayerId ?? _snapshot.Id;

        foreach (var el in page.Elements.Where(e => _movedElementIds.Contains(e.Id)))
        {
            el.LayerId = _snapshot.Id;
        }
    }
}
