using System.Text.Json;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Updates one or more props on a single element.
/// Supports undo by storing a snapshot of the previous prop values.
/// </summary>
public sealed class UpdatePropsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _id;

    // key → value before change (null = key was absent)
    private readonly Dictionary<string, JsonElement?> _before;

    // key → new value (null = remove key)
    private readonly Dictionary<string, JsonElement?> _after;

    /// <param name="doc">Target document.</param>
    /// <param name="id">Element id.</param>
    /// <param name="changes">Dictionary of prop key → new JsonElement (null removes the prop).</param>
    public UpdatePropsCommand(
        WireframeDocument doc,
        string id,
        Dictionary<string, JsonElement?> changes)
    {
        _doc   = doc;
        _id    = id;
        _after = changes;

        // Snapshot current values for undo
        var el = doc.Elements.FirstOrDefault(e => e.Id == id);
        _before = [];
        foreach (var key in changes.Keys)
        {
            _before[key] = el is not null && el.Props.TryGetValue(key, out var v)
                ? v
                : null;
        }
    }

    public string Name => "Update properties";

    public void Execute() => Apply(_after);
    public void Undo()    => Apply(_before);

    private void Apply(Dictionary<string, JsonElement?> values)
    {
        var el = _doc.Elements.FirstOrDefault(e => e.Id == _id);
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
