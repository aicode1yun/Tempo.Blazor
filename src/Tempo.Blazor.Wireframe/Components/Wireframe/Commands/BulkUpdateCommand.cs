using System.Text.Json;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Applies the same prop changes to multiple selected elements at once
/// (e.g. changing a shared property via the Properties Panel multi-select mode).
/// </summary>
public sealed class BulkUpdateCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;

    // id → {key → value before}
    private readonly Dictionary<string, Dictionary<string, JsonElement?>> _before;

    // shared changes applied to all elements
    private readonly Dictionary<string, JsonElement?> _changes;

    private readonly IReadOnlyList<string> _ids;

    /// <param name="doc">Target document.</param>
    /// <param name="ids">Ids of all elements to update.</param>
    /// <param name="changes">Prop key → new value (null removes prop).</param>
    public BulkUpdateCommand(
        WireframeDocument doc,
        IEnumerable<string> ids,
        Dictionary<string, JsonElement?> changes)
    {
        _doc     = doc;
        _changes = changes;
        _ids     = ids.ToList();

        // Snapshot all before-values for undo
        _before = [];
        foreach (var id in _ids)
        {
            var el = doc.Elements.FirstOrDefault(e => e.Id == id);
            var snap = new Dictionary<string, JsonElement?>();
            foreach (var key in changes.Keys)
            {
                snap[key] = el is not null && el.Props.TryGetValue(key, out var v) ? v : null;
            }
            _before[id] = snap;
        }
    }

    public string Name => $"Update {_ids.Count} elements";

    public void Execute()
    {
        foreach (var id in _ids)
            Apply(id, _changes);
    }

    public void Undo()
    {
        foreach (var id in _ids)
        {
            if (_before.TryGetValue(id, out var snap))
                Apply(id, snap);
        }
    }

    private void Apply(string id, Dictionary<string, JsonElement?> values)
    {
        var el = _doc.Elements.FirstOrDefault(e => e.Id == id);
        if (el is null) return;
        foreach (var (key, value) in values)
        {
            if (value is null)
                el.Props.Remove(key);
            else
                el.Props[key] = value.Value;
        }
    }
}
