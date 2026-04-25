using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Unlocks one or more elements so they can be edited again.
/// </summary>
public sealed class UnlockElementsCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly IReadOnlyList<string> _ids;

    /// <summary>
    /// Creates a command that unlocks <paramref name="ids"/>.
    /// </summary>
    public UnlockElementsCommand(WireframeDocument doc, IEnumerable<string> ids)
    {
        _doc = doc;
        _ids = ids.ToList();
    }

    /// <inheritdoc />
    public string Name => _ids.Count == 1 ? "Unlock element" : $"Unlock {_ids.Count} elements";

    /// <inheritdoc />
    public void Execute()
    {
        foreach (var el in _doc.Elements)
        {
            if (_ids.Contains(el.Id))
                el.IsLocked = false;
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        foreach (var el in _doc.Elements)
        {
            if (_ids.Contains(el.Id))
                el.IsLocked = true;
        }
    }
}
