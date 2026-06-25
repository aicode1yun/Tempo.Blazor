using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Brings the selected elements to the front by assigning them a ZIndex higher than all other elements.
/// </summary>
public sealed class BringToFrontCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<string> _ids;
    private readonly Dictionary<string, int> _originalZIndices = [];

    /// <summary>
    /// Creates a command that brings <paramref name="ids"/> to the front.
    /// </summary>
    public BringToFrontCommand(WireframeDocument doc, IEnumerable<string> ids)
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
    public string Name => _ids.Count == 1 ? "Bring to front" : $"Bring {_ids.Count} to front";

    /// <inheritdoc />
    public void Execute()
    {
        if (_doc.Elements.Count == 0) return;
        var maxZ = _doc.Elements.Max(e => e.ZIndex);
        foreach (var id in _ids)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == id);
            if (el is not null)
                el.ZIndex = ++maxZ;
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
