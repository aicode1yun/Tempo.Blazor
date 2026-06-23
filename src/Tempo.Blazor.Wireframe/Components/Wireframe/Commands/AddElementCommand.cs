using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Adds a single element to the document.</summary>
public sealed class AddElementCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly WireframeElement _element;

    public AddElementCommand(WireframeDocument doc, WireframeElement element)
    {
        _doc = doc;
        _element = element;
    }

    public string Name => $"Add {_element.Type}";

    public void Execute() => _doc.Elements.Add(_element);

    public void Undo() => _doc.Elements.RemoveAll(e => e.Id == _element.Id);
}
