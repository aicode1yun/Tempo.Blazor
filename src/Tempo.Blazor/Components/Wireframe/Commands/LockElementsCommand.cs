using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Locks one or more elements so they cannot be moved, resized, or deleted.
/// </summary>
public sealed class LockElementsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<string> _ids;

    /// <summary>
    /// Creates a command that locks <paramref name="ids"/>.
    /// </summary>
    public LockElementsCommand(WireframeDocument doc, IEnumerable<string> ids)
    {
        _doc = doc;
        _ids = ids.ToList();
    }

    /// <inheritdoc />
    public string Name => _ids.Count == 1 ? "Lock element" : $"Lock {_ids.Count} elements";

    /// <inheritdoc />
    public void Execute()
    {
        foreach (var el in _doc.Elements)
        {
            if (_ids.Contains(el.Id))
                el.IsLocked = true;
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        foreach (var el in _doc.Elements)
        {
            if (_ids.Contains(el.Id))
                el.IsLocked = false;
        }
    }
}
