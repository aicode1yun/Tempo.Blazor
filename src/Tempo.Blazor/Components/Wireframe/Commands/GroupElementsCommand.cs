using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Groups the selected elements under a new group container (undoable).</summary>
public sealed class GroupElementsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<string> _elementIds;
    private readonly WireframeElement _container;
    private readonly IReadOnlyList<string?> _previousGroupIds;

    public GroupElementsCommand(WireframeDocument doc, IEnumerable<string> elementIds)
    {
        _doc = doc;
        _elementIds = elementIds.ToList();
        _previousGroupIds = _elementIds.Select(id => _doc.Elements.FirstOrDefault(e => e.Id == id)?.GroupId).ToList();

        var elements = _elementIds
            .Select(id => _doc.Elements.FirstOrDefault(e => e.Id == id))
            .Where(e => e is not null)
            .ToList();

        var minX = elements.Min(e => e!.X);
        var minY = elements.Min(e => e!.Y);
        var maxX = elements.Max(e => e!.X + e!.W);
        var maxY = elements.Max(e => e!.Y + e!.H);
        const double pad = 12;

        var minZ = elements.Min(e => e!.ZIndex);

        _container = new WireframeElement
        {
            Type = "__group__",
            X = minX - pad,
            Y = minY - pad,
            W = maxX - minX + pad * 2,
            H = maxY - minY + pad * 2,
            ZIndex = minZ - 1,
            Props = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["label"] = System.Text.Json.JsonSerializer.SerializeToElement("Group")
            }
        };
    }

    public string Name => _elementIds.Count == 1 ? "Group element" : $"Group {_elementIds.Count} elements";

    public void Execute()
    {
        _doc.Elements.Add(_container);
        foreach (var id in _elementIds)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == id);
            if (el is null) continue;
            el.GroupId = _container.Id;
        }
    }

    public void Undo()
    {
        _doc.Elements.Remove(_container);
        for (int i = 0; i < _elementIds.Count; i++)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == _elementIds[i]);
            if (el is null || i >= _previousGroupIds.Count) continue;
            el.GroupId = _previousGroupIds[i];
        }
    }
}
