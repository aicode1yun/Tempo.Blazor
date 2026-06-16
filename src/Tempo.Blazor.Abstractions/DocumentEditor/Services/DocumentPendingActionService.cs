namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Tracks in-flight async operations (save, export, upload, sync) that the user should know are pending.</summary>
public sealed class DocumentPendingActionService
{
    private readonly Dictionary<string, string> _actions = [];

    /// <summary>Whether any actions are currently pending.</summary>
    public bool HasAny => _actions.Count > 0;

    /// <summary>Number of currently pending actions.</summary>
    public int Count => _actions.Count;

    /// <summary>Message of the first registered pending action, or <c>null</c> when none are pending.</summary>
    public string? FirstMessage => _actions.Count > 0 ? _actions.Values.First() : null;

    /// <summary>All pending action messages in registration order.</summary>
    public IReadOnlyList<string> Messages => [.. _actions.Values];

    /// <summary>Returns the message for a pending action, or <c>null</c> when the action is not registered.</summary>
    public string? GetMessage(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _actions.TryGetValue(id, out var message) ? message : null;
    }

    /// <summary>
    /// Registers or updates a pending action.
    /// If an action with <paramref name="id"/> already exists its message is replaced.
    /// </summary>
    public void Add(string id, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(message);
        _actions[id] = message;
    }

    /// <summary>Removes the pending action with the given <paramref name="id"/>. No-op if not found.</summary>
    public void Remove(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        _actions.Remove(id);
    }

    /// <summary>Removes all pending actions.</summary>
    public void Clear() => _actions.Clear();
}
