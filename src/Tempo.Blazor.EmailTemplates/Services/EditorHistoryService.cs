using Tempo.Blazor.EmailTemplates.Abstractions.Model;

namespace Tempo.Blazor.EmailTemplates.Services;

/// <summary>
/// Maintains undo/redo history of document snapshots. Each pushed state is a deep, independent copy,
/// the depth is bounded, and rapid edits sharing a coalesce key within a short window merge into a
/// single undo step (so typing is one undo, not one per keystroke).
/// </summary>
public sealed class EditorHistoryService
{
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(500);

    private readonly TimeProvider _time;
    private readonly int _maxDepth;
    private readonly LinkedList<EmailTemplateDocument> _undo = new();
    private readonly Stack<EmailTemplateDocument> _redo = new();

    private EmailTemplateDocument _current = new();
    private string? _lastCoalesceKey;
    private DateTimeOffset _lastPushedAt;

    /// <summary>Initializes the service with an optional clock and history depth limit.</summary>
    public EditorHistoryService(TimeProvider? timeProvider = null, int maxDepth = 50)
    {
        _time = timeProvider ?? TimeProvider.System;
        _maxDepth = Math.Max(1, maxDepth);
    }

    /// <summary>Gets whether an undo is available.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Gets whether a redo is available.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Resets the history to a single baseline state.</summary>
    public void Initialize(EmailTemplateDocument document)
    {
        _undo.Clear();
        _redo.Clear();
        _current = document.DeepClone();
        _lastCoalesceKey = null;
    }

    /// <summary>
    /// Records a new document state. When <paramref name="coalesceKey"/> matches the previous push and
    /// falls within the coalesce window, it replaces the current state instead of adding a new step.
    /// </summary>
    public void Push(EmailTemplateDocument document, string? coalesceKey = null)
    {
        var now = _time.GetUtcNow();
        var coalesce = coalesceKey is not null
            && coalesceKey == _lastCoalesceKey
            && (now - _lastPushedAt) <= CoalesceWindow;

        if (!coalesce)
        {
            _undo.AddLast(_current);
            if (_undo.Count > _maxDepth) _undo.RemoveFirst();
            _redo.Clear();
        }

        _current = document.DeepClone();
        _lastCoalesceKey = coalesceKey;
        _lastPushedAt = now;
    }

    /// <summary>Moves one step back and returns an independent copy of that state, or <see langword="null"/>.</summary>
    public EmailTemplateDocument? Undo()
    {
        if (_undo.Count == 0) return null;
        _redo.Push(_current);
        _current = _undo.Last!.Value;
        _undo.RemoveLast();
        _lastCoalesceKey = null;
        return _current.DeepClone();
    }

    /// <summary>Moves one step forward and returns an independent copy of that state, or <see langword="null"/>.</summary>
    public EmailTemplateDocument? Redo()
    {
        if (_redo.Count == 0) return null;
        _undo.AddLast(_current);
        _current = _redo.Pop();
        _lastCoalesceKey = null;
        return _current.DeepClone();
    }
}
