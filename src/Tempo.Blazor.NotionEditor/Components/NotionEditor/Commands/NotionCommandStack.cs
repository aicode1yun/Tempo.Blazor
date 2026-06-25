using Tempo.Blazor.NotionEditor.Commands;

namespace Tempo.Blazor.Components.NotionEditor.Commands;

/// <summary>
/// Async undo / redo history for a single Notion editor instance.
///
/// Not a DI service — <c>TmNotionPage</c> creates one instance per page and
/// cascades it to child components via <c>CascadingValue</c> so each page has
/// its own isolated stack.
///
/// Because provider calls are async the stack exposes async Push / Undo / Redo.
/// The <see cref="OnStackChanged"/> event is raised on every mutation so the UI
/// can refresh without coupling to the stack's internal implementation.
/// </summary>
public sealed class NotionCommandStack
{
    private readonly int _maxDepth;
    private readonly LinkedList<INotionCommand> _undoStack = new();
    private readonly Stack<INotionCommand>      _redoStack = new();
    private BatchCommand?                       _currentBatch;

    /// <summary>Raised on every Push / Undo / Redo / Clear so the toolbar and callers can refresh.</summary>
    public event Action? OnStackChanged;

    /// <param name="maxDepth">Maximum undo steps retained (default 100).</param>
    public NotionCommandStack(int maxDepth = 100)
    {
        _maxDepth = maxDepth;
    }

    // ── State ─────────────────────────────────────────────────────────────────

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Description of the next command that would be undone, or <c>null</c>.</summary>
    public string? NextUndoDescription => _undoStack.Last?.Value.Description;

    /// <summary>Description of the next command that would be redone, or <c>null</c>.</summary>
    public string? NextRedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

    /// <summary>Whether a batch is currently open.</summary>
    public bool IsInBatch => _currentBatch is not null;

    // ── Push ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="command"/> and pushes it onto the undo stack.
    /// When a batch is open the command is appended to it instead of being
    /// pushed directly — it will be committed as part of <see cref="CommitBatchAsync"/>.
    /// </summary>
    public async Task PushAsync(INotionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        await command.ExecuteAsync();

        if (_currentBatch is not null)
        {
            _currentBatch.Add(command);
            OnStackChanged?.Invoke();
            return;
        }

        _undoStack.AddLast(command);
        if (_undoStack.Count > _maxDepth)
            _undoStack.RemoveFirst();

        _redoStack.Clear();
        OnStackChanged?.Invoke();
    }

    // ── Batch (multi-command undo step) ──────────────────────────────────────

    /// <summary>
    /// Begins a new batch scope. All commands pushed while the batch is open
    /// will be grouped into a single undo / redo step.
    /// Nested batches are not allowed.
    /// </summary>
    public void BeginBatch(string description)
    {
        if (_currentBatch is not null)
            throw new InvalidOperationException("A batch is already in progress. Nested batches are not supported.");

        _currentBatch = new BatchCommand(description);
    }

    /// <summary>
    /// Commits the open batch as a single undo step.
    /// If the batch contains no commands it is discarded silently.
    /// </summary>
    public void CommitBatch()
    {
        if (_currentBatch is null)
            throw new InvalidOperationException("No batch is in progress.");

        var batch = _currentBatch;
        _currentBatch = null;

        if (batch.Count == 0)
            return;

        _undoStack.AddLast(batch);
        if (_undoStack.Count > _maxDepth)
            _undoStack.RemoveFirst();

        _redoStack.Clear();
        OnStackChanged?.Invoke();
    }

    /// <summary>
    /// Rolls back the open batch: undoes all commands already executed within it
    /// and discards the batch without recording it.
    /// </summary>
    public async Task RollbackBatchAsync()
    {
        if (_currentBatch is null)
            throw new InvalidOperationException("No batch is in progress.");

        var batch = _currentBatch;
        _currentBatch = null;

        if (batch.Count > 0)
            await batch.UndoAsync();
    }

    /// <summary>
    /// Returns an <see cref="IAsyncDisposable"/> that begins a batch now
    /// and commits it on disposal (or rolls it back if an exception is thrown).
    /// </summary>
    public IAsyncDisposable BatchScope(string description)
    {
        BeginBatch(description);
        return new BatchScopeDisposable(this);
    }

    // ── Undo / Redo ───────────────────────────────────────────────────────────

    /// <summary>Undoes the most recent command. No-op when <see cref="CanUndo"/> is false.</summary>
    public async Task UndoAsync()
    {
        if (!CanUndo) return;
        var cmd = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        await cmd.UndoAsync();
        _redoStack.Push(cmd);
        OnStackChanged?.Invoke();
    }

    /// <summary>Re-applies the most recently undone command. No-op when <see cref="CanRedo"/> is false.</summary>
    public async Task RedoAsync()
    {
        if (!CanRedo) return;
        var cmd = _redoStack.Pop();
        await cmd.ExecuteAsync();
        _undoStack.AddLast(cmd);
        OnStackChanged?.Invoke();
    }

    /// <summary>Clears both stacks (e.g. after navigating to a different page).</summary>
    public async Task ClearAsync()
    {
        if (_currentBatch is not null)
            await RollbackBatchAsync();

        _undoStack.Clear();
        _redoStack.Clear();
        OnStackChanged?.Invoke();
    }

    // ── Disposable scope helper ───────────────────────────────────────────────

    private sealed class BatchScopeDisposable : IAsyncDisposable
    {
        private readonly NotionCommandStack _stack;
        private bool _disposed;

        public BatchScopeDisposable(NotionCommandStack stack) => _stack = stack;

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            if (_stack.IsInBatch)
            {
                try   { _stack.CommitBatch(); }
                catch { await _stack.RollbackBatchAsync(); throw; }
            }
        }
    }
}
