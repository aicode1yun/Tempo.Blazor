using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Changes the <see cref="WireframeElement.ZIndex"/> of a single element.
/// Supports undo/redo through the command stack.
/// </summary>
public sealed class ZIndexCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _elementId;
    private readonly int _before;
    private readonly int _after;

    /// <summary>
    /// Creates a new ZIndex change command.
    /// </summary>
    /// <param name="doc">The document containing the element.</param>
    /// <param name="elementId">Id of the element to modify.</param>
    /// <param name="before">Original ZIndex value.</param>
    /// <param name="after">New ZIndex value.</param>
    /// <param name="name">Human-readable command name for the undo stack.</param>
    public ZIndexCommand(WireframeDocument doc, string elementId, int before, int after, string name)
    {
        _doc = doc;
        _elementId = elementId;
        _before = before;
        _after = after;
        Name = name;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public void Execute() => Apply(_after);

    /// <inheritdoc />
    public void Undo() => Apply(_before);

    private void Apply(int z)
    {
        var el = _doc.Elements.FirstOrDefault(e => e.Id == _elementId);
        if (el is not null)
            el.ZIndex = z;
    }
}
