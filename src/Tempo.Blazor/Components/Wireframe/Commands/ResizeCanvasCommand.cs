using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Changes the canvas (document) width and height.</summary>
public sealed class ResizeCanvasCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly double _oldW, _oldH, _newW, _newH;

    public ResizeCanvasCommand(WireframeDocument doc, double oldW, double oldH, double newW, double newH)
    {
        _doc = doc;
        _oldW = oldW; _oldH = oldH;
        _newW = newW; _newH = newH;
    }

    public string Name => "Resize Canvas";

    public void Execute() { _doc.Width = _newW; _doc.Height = _newH; }
    public void Undo()    { _doc.Width = _oldW; _doc.Height = _oldH; }
}
