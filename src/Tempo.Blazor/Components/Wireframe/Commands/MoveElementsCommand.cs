using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Moves one or more elements to new positions.
///
/// <para>
/// Supports coalescing: consecutive move commands targeting the same element ids within
/// a 100 ms window are merged. The "before" snapshot is kept from the first move;
/// only the "after" positions are updated. This keeps undo snapping back to where the
/// drag began rather than to an intermediate frame.
/// </para>
/// </summary>
public sealed class MoveElementsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;

    // id → position before this command
    private readonly Dictionary<string, (double X, double Y)> _before;

    // id → position after this command (mutable for coalescing)
    private Dictionary<string, (double X, double Y)> _after;

    private DateTime _pushedAt;

    /// <param name="doc">Target document.</param>
    /// <param name="before">Positions before the move (snapshot at drag-start).</param>
    /// <param name="after">Positions after the move (snapshot at drag-end).</param>
    public MoveElementsCommand(
        WireframeDocument doc,
        Dictionary<string, (double X, double Y)> before,
        Dictionary<string, (double X, double Y)> after)
    {
        _doc    = doc;
        _before = before;
        _after  = after;
        _pushedAt = DateTime.UtcNow;
    }

    public string Name => _after.Count == 1
        ? "Move element"
        : $"Move {_after.Count} elements";

    // Ids involved in this command
    private IReadOnlySet<string> Ids => _after.Keys.ToHashSet();

    public void Execute()
    {
        foreach (var el in _doc.Elements)
        {
            if (_after.TryGetValue(el.Id, out var pos))
            { el.X = pos.X; el.Y = pos.Y; }
        }
    }

    public void Undo()
    {
        foreach (var el in _doc.Elements)
        {
            if (_before.TryGetValue(el.Id, out var pos))
            { el.X = pos.X; el.Y = pos.Y; }
        }
    }

    /// <summary>
    /// Attempts to merge <paramref name="next"/> into this command.
    /// Returns <c>true</c> if merged (the caller must NOT push <paramref name="next"/> separately).
    /// </summary>
    internal bool TryCoalesce(MoveElementsCommand next)
    {
        // Only merge if same set of elements and within 100 ms
        if ((next._pushedAt - _pushedAt).TotalMilliseconds > 100) return false;
        if (!next.Ids.SetEquals(Ids)) return false;

        // Apply the new "after" positions directly to the document
        foreach (var el in _doc.Elements)
        {
            if (next._after.TryGetValue(el.Id, out var pos))
            { el.X = pos.X; el.Y = pos.Y; }
        }

        // Update our "after" so the next Undo/Redo uses the latest positions
        _after = next._after;
        _pushedAt = next._pushedAt;
        return true;
    }
}
