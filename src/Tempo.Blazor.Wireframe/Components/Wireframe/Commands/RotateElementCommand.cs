using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Changes the rotation angle of a single element.
/// </summary>
public sealed class RotateElementCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _elementId;
    private readonly double _before;
    private readonly double _after;

    /// <summary>
    /// Creates a rotation command.
    /// </summary>
    public RotateElementCommand(WireframeDocument doc, string elementId, double before, double after)
    {
        _doc = doc;
        _elementId = elementId;
        _before = before;
        _after = after;
    }

    /// <inheritdoc />
    public string Name => "Rotate element";

    /// <inheritdoc />
    public void Execute() => Apply(_after);

    /// <inheritdoc />
    public void Undo() => Apply(_before);

    private void Apply(double rotation)
    {
        var el = _doc.Elements.FirstOrDefault(e => e.Id == _elementId);
        if (el is not null)
            el.Rotation = rotation;
    }
}
