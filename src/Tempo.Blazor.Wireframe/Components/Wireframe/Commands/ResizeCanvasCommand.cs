using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Changes the canvas width and height of the active page.</summary>
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

    public string Name => "Resize canvas";

    public void Execute()
    {
        if (_doc.ActivePage is { } page)
        {
            page.Width = _newW;
            page.Height = _newH;
        }
    }

    public void Undo()
    {
        if (_doc.ActivePage is { } page)
        {
            page.Width = _oldW;
            page.Height = _oldH;
        }
    }
}
