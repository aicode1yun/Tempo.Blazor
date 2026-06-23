using System.Text.Json;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Applies the copied style from <see cref="WireframeClipboard"/> to selected elements (undoable).</summary>
public sealed class PasteStyleCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<string> _ids;
    private readonly WireframeComponentRegistry _registry;
    private readonly Dictionary<string, Dictionary<string, JsonElement>> _before = [];

    public PasteStyleCommand(WireframeDocument doc, IEnumerable<string> ids, WireframeComponentRegistry registry)
    {
        _doc = doc;
        _ids = ids.ToList();
        _registry = registry;
    }

    public string Name => "Paste style";

    public void Execute()
    {
        if (WireframeClipboard.StyleProps is null || WireframeClipboard.StyleProps.Count == 0) return;

        foreach (var id in _ids)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == id);
            if (el is null || el.IsLocked || !string.IsNullOrEmpty(el.LockedBy)) continue;

            // Snapshot before
            _before[id] = WireframeClipboard.CloneProps(el.Props);

            // Get schema for target element to know which props are valid
            var def = _registry.GetDef(el.Type);
            var validPropNames = def?.Props.Select(p => p.Name).ToHashSet() ?? [];

            // Apply only props that exist in target schema
            foreach (var kv in WireframeClipboard.StyleProps)
            {
                if (validPropNames.Contains(kv.Key))
                {
                    el.Props[kv.Key] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(kv.Value));
                }
            }
        }
    }

    public void Undo()
    {
        foreach (var (id, oldProps) in _before)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == id);
            if (el is null) continue;
            el.Props = oldProps;
        }
    }
}
