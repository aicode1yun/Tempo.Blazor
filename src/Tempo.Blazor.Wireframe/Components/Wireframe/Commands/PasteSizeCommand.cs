using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Applies the copied width/height from <see cref="WireframeClipboard"/> to selected elements (undoable).</summary>
public sealed class PasteSizeCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<string> _ids;
    private readonly Dictionary<string, (double W, double H)> _before = [];

    public PasteSizeCommand(WireframeDocument doc, IEnumerable<string> ids)
    {
        _doc = doc;
        _ids = ids.ToList();
    }

    public string Name => "Paste size";

    public void Execute()
    {
        var w = WireframeClipboard.Width;
        var h = WireframeClipboard.Height;
        if (!w.HasValue && !h.HasValue) return;

        foreach (var id in _ids)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == id);
            if (el is null || el.IsLocked || !string.IsNullOrEmpty(el.LockedBy)) continue;

            _before[id] = (el.W, el.H);
            if (w.HasValue) el.W = w.Value;
            if (h.HasValue) el.H = h.Value;
        }
    }

    public void Undo()
    {
        foreach (var (id, size) in _before)
        {
            var el = _doc.Elements.FirstOrDefault(e => e.Id == id);
            if (el is null) continue;
            el.W = size.W;
            el.H = size.H;
        }
    }
}
