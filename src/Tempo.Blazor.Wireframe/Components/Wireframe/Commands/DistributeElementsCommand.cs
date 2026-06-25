using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Distributes selected elements with equal spacing (undoable).</summary>
public sealed class DistributeElementsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<string> _ids;
    private readonly WireframeDistribution _distribution;
    private readonly Dictionary<string, (double X, double Y)> _before;

    public DistributeElementsCommand(WireframeDocument doc, IEnumerable<string> ids, WireframeDistribution distribution)
    {
        _doc = doc;
        _ids = ids.ToList();
        _distribution = distribution;
        _before = _ids
            .Select(id => _doc.Elements.FirstOrDefault(e => e.Id == id))
            .Where(e => e is not null)
            .ToDictionary(e => e!.Id, e => (e.X, e.Y));
    }

    public string Name => $"Distribute {_distribution.ToString().ToLowerInvariant()}";

    public void Execute()
    {
        var elements = _ids
            .Select(id => _doc.Elements.FirstOrDefault(e => e.Id == id))
            .Where(e => e is not null)
            .ToList();

        if (elements.Count < 3) return;

        // Filter out locked
        var movable = elements
            .Where(e => e is not null && !e.IsLocked && string.IsNullOrEmpty(e.LockedBy))
            .ToList();

        if (movable.Count < 3) return;

        if (_distribution == WireframeDistribution.Horizontal)
        {
            // Sort by center X
            var sorted = movable.OrderBy(e => e!.X + e.W / 2).ToList();
            var firstCenter = sorted[0]!.X + sorted[0]!.W / 2;
            var lastCenter = sorted[^1]!.X + sorted[^1]!.W / 2;
            var step = (lastCenter - firstCenter) / (sorted.Count - 1);

            for (int i = 0; i < sorted.Count; i++)
            {
                var el = sorted[i]!;
                el.X = firstCenter + i * step - el.W / 2;
            }
        }
        else // Vertical
        {
            // Sort by center Y
            var sorted = movable.OrderBy(e => e!.Y + e.H / 2).ToList();
            var firstCenter = sorted[0]!.Y + sorted[0]!.H / 2;
            var lastCenter = sorted[^1]!.Y + sorted[^1]!.H / 2;
            var step = (lastCenter - firstCenter) / (sorted.Count - 1);

            for (int i = 0; i < sorted.Count; i++)
            {
                var el = sorted[i]!;
                el.Y = firstCenter + i * step - el.H / 2;
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
