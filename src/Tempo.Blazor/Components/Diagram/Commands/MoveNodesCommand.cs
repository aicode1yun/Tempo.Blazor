using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>
/// Moves one or more nodes to new positions.
///
/// <para>
/// Supports coalescing: consecutive move commands targeting the same node ids within
/// a 100 ms window are merged. The "before" snapshot is kept from the first move;
/// only the "after" positions are updated.
/// </para>
/// </summary>
public sealed class MoveNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly Dictionary<string, (double X, double Y)> _before;
    private Dictionary<string, (double X, double Y)> _after;
    private DateTime _pushedAt;

    public MoveNodesCommand(
        DiagramDocument doc,
        Dictionary<string, (double X, double Y)> before,
        Dictionary<string, (double X, double Y)> after)
    {
        _doc = doc;
        _before = before;
        _after = after;
        _pushedAt = DateTime.UtcNow;
    }

    public string Name => _after.Count == 1
        ? "Move node"
        : $"Move {_after.Count} nodes";

    private IReadOnlySet<string> Ids => _after.Keys.ToHashSet();

    public void Execute()
    {
        foreach (var node in _doc.Nodes)
        {
            if (_after.TryGetValue(node.Id, out var pos))
            { node.X = pos.X; node.Y = pos.Y; }
        }
    }

    public void Undo()
    {
        foreach (var node in _doc.Nodes)
        {
            if (_before.TryGetValue(node.Id, out var pos))
            { node.X = pos.X; node.Y = pos.Y; }
        }
    }

    /// <summary>
    /// Attempts to merge <paramref name="next"/> into this command.
    /// Returns <c>true</c> if merged.
    /// </summary>
    internal bool TryCoalesce(MoveNodesCommand next)
    {
        if ((next._pushedAt - _pushedAt).TotalMilliseconds > 100) return false;
        if (!next.Ids.SetEquals(Ids)) return false;

        foreach (var node in _doc.Nodes)
        {
            if (next._after.TryGetValue(node.Id, out var pos))
            { node.X = pos.X; node.Y = pos.Y; }
        }

        _after = next._after;
        _pushedAt = next._pushedAt;
        return true;
    }
}
