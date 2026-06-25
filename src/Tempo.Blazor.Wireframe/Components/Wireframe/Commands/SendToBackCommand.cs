using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Sends the selected elements to the back by assigning them a ZIndex lower than all other elements.
/// </summary>
public sealed class SendToBackCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<string> _ids;
    private readonly Dictionary<string, int> _originalZIndices = [];

    /// <summary>
    /// Creates a command that sends <paramref name="ids"/> to the back.
    /// </summary>
    public SendToBackCommand(WireframeDocument doc, IEnumerable<string> ids)
    {
        _doc = doc;
        _ids = ids.ToList();

        foreach (var id in _ids)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == id);
            if (el is not null)
                _originalZIndices[id] = el.ZIndex;
        }
    }

    /// <inheritdoc />
    public string Name => _ids.Count == 1 ? "Send to back" : $"Send {_ids.Count} to back";

    /// <inheritdoc />
    public void Execute()
    {
        if (_doc.Elements.Count == 0) return;
        var minZ = _doc.Elements.Min(e => e.ZIndex);
        // Decrease by count so all selected elements get unique and lower Z values
        var offset = _ids.Count;
        foreach (var id in _ids)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == id);
            if (el is not null)
                el.ZIndex = minZ - offset--;
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        foreach (var kvp in _originalZIndices)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == kvp.Key);
            if (el is not null)
                el.ZIndex = kvp.Value;
        }
    }
}
