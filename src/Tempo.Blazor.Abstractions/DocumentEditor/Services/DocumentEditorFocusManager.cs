namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Tracks logical focus targets for document editor surface, toolbar, and floating UI.</summary>
public sealed class DocumentEditorFocusManager
{
    private readonly Dictionary<string, DocumentEditorFocusTarget> _targets = new(StringComparer.Ordinal);
    private readonly Stack<string> _restoreStack = new();

    /// <summary>Registers or replaces a logical focus target.</summary>
    public void Register(DocumentEditorFocusTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(target.Id))
        {
            throw new ArgumentException("Focus target id cannot be empty.", nameof(target));
        }

        _targets[target.Id] = target;
    }

    /// <summary>Gets all registered focus targets.</summary>
    public IReadOnlyList<DocumentEditorFocusTarget> Targets => _targets.Values
        .OrderBy(target => target.Id, StringComparer.Ordinal)
        .ToArray();

    /// <summary>Marks a target as the current restore destination.</summary>
    public void PushRestoreTarget(string? targetId)
    {
        if (!string.IsNullOrWhiteSpace(targetId) && _targets.ContainsKey(targetId))
        {
            _restoreStack.Push(targetId);
        }
    }

    /// <summary>Returns and removes the next valid restore target.</summary>
    public DocumentEditorFocusTarget? PopRestoreTarget()
    {
        while (_restoreStack.Count > 0)
        {
            var id = _restoreStack.Pop();
            if (_targets.TryGetValue(id, out var target))
            {
                return target;
            }
        }

        return null;
    }

    /// <summary>Returns whether the target should trap keyboard focus.</summary>
    public bool ShouldTrapFocus(string? targetId) =>
        !string.IsNullOrWhiteSpace(targetId)
        && _targets.TryGetValue(targetId, out var target)
        && target.TrapsFocus;
}

/// <summary>Logical document editor focus target.</summary>
public sealed record DocumentEditorFocusTarget
{
    /// <summary>Stable target id.</summary>
    public required string Id { get; init; }

    /// <summary>Target kind.</summary>
    public DocumentEditorFocusTargetKind Kind { get; init; }

    /// <summary>Optional selector used by the Blazor or JS focus bridge.</summary>
    public string? Selector { get; init; }

    /// <summary>Whether focus should be trapped while the target is active.</summary>
    public bool TrapsFocus { get; init; }
}

/// <summary>Document editor focus target kind.</summary>
public enum DocumentEditorFocusTargetKind
{
    /// <summary>Editable document surface.</summary>
    Surface,

    /// <summary>Ribbon or toolbar.</summary>
    Toolbar,

    /// <summary>Floating layer.</summary>
    FloatingLayer,

    /// <summary>Modal dialog.</summary>
    Modal
}
