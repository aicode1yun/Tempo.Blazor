using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Resizes (and optionally repositions) a single element.</summary>
public sealed class ResizeElementCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _id;

    private readonly double _beforeX, _beforeY, _beforeW, _beforeH;
    private readonly double _afterX,  _afterY,  _afterW,  _afterH;

    public ResizeElementCommand(
        WireframeDocument doc,
        string id,
        double beforeX, double beforeY, double beforeW, double beforeH,
        double afterX,  double afterY,  double afterW,  double afterH)
    {
        _doc = doc; _id = id;
        _beforeX = beforeX; _beforeY = beforeY; _beforeW = beforeW; _beforeH = beforeH;
        _afterX  = afterX;  _afterY  = afterY;  _afterW  = afterW;  _afterH  = afterH;
    }

    public string Name => "Resize element";

    public void Execute() => Apply(_afterX, _afterY, _afterW, _afterH);
    public void Undo()    => Apply(_beforeX, _beforeY, _beforeW, _beforeH);

    private void Apply(double x, double y, double w, double h)
    {
        var el = _doc.Elements.FirstOrDefault(e => e.Id == _id);
        if (el is null) return;
        el.X = x; el.Y = y; el.W = w; el.H = h;
    }
}
