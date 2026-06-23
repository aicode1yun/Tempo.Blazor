using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Rotates multiple elements to a common angle. Stores original per-element angles for undo.
/// </summary>
public sealed class BulkRotateCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<string> _ids;
    private readonly Dictionary<string, double> _beforeAngles;
    private readonly double _afterAngle;

    /// <summary>
    /// Creates a bulk rotation command.
    /// </summary>
    public BulkRotateCommand(WireframeDocument doc, IEnumerable<string> ids, Dictionary<string, double> beforeAngles, double afterAngle)
    {
        _doc = doc;
        _ids = ids.ToList();
        _beforeAngles = beforeAngles;
        _afterAngle = afterAngle;
    }

    /// <inheritdoc />
    public string Name => $"Rotate {_ids.Count} elements";

    /// <inheritdoc />
    public void Execute()
    {
        foreach (var el in _doc.Elements)
        {
            if (_ids.Contains(el.Id))
                el.Rotation = _afterAngle;
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        foreach (var kvp in _beforeAngles)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == kvp.Key);
            if (el is not null)
                el.Rotation = kvp.Value;
        }
    }
}
