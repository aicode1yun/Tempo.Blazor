using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Aligns selected elements along the specified axis (undoable).</summary>
public sealed class AlignElementsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<string> _ids;
    private readonly WireframeAlignment _alignment;
    private readonly Dictionary<string, (double X, double Y)> _before;

    public AlignElementsCommand(WireframeDocument doc, IEnumerable<string> ids, WireframeAlignment alignment)
    {
        _doc = doc;
        _ids = ids.ToList();
        _alignment = alignment;
        _before = _ids
            .Select(id => _doc.Elements.FirstOrDefault(e => e.Id == id))
            .Where(e => e is not null)
            .ToDictionary(e => e!.Id, e => (e.X, e.Y));
    }

    public string Name => $"Align {_alignment.ToString().ToLowerInvariant()}";

    public void Execute()
    {
        var elements = _ids
            .Select(id => _doc.Elements.FirstOrDefault(e => e.Id == id))
            .Where(e => e is not null)
            .ToList();

        if (elements.Count < 2) return;

        var minX = elements.Min(e => e!.X);
        var maxX = elements.Max(e => e!.X + e.W);
        var minY = elements.Min(e => e!.Y);
        var maxY = elements.Max(e => e!.Y + e.H);
        var centerH = (minX + maxX) / 2;
        var centerV = (minY + maxY) / 2;

        foreach (var el in elements)
        {
            if (el is null || el.IsLocked || !string.IsNullOrEmpty(el.LockedBy)) continue;

            switch (_alignment)
            {
                case WireframeAlignment.Left:
                    el.X = minX;
                    break;
                case WireframeAlignment.CenterH:
                    el.X = centerH - el.W / 2;
                    break;
                case WireframeAlignment.Right:
                    el.X = maxX - el.W;
                    break;
                case WireframeAlignment.Top:
                    el.Y = minY;
                    break;
                case WireframeAlignment.CenterV:
                    el.Y = centerV - el.H / 2;
                    break;
                case WireframeAlignment.Bottom:
                    el.Y = maxY - el.H;
                    break;
            }
        }
    }

    public void Undo()
    {
        foreach (var el in _doc.Elements)
        {
            if (el.IsLocked || !string.IsNullOrEmpty(el.LockedBy)) continue;
            if (_before.TryGetValue(el.Id, out var pos))
            {
                el.X = pos.X;
                el.Y = pos.Y;
            }
        }
    }
}
