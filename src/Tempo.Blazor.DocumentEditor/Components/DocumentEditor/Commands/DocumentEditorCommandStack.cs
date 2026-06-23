namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Async undo and redo stack for a single document editor instance.</summary>
public sealed class DocumentEditorCommandStack
{
    private readonly int _maxDepth;
    private readonly LinkedList<IDocumentEditorCommand> _undoStack = new();
    private readonly Stack<IDocumentEditorCommand> _redoStack = new();
    private BatchDocumentEditorCommand? _currentBatch;

    /// <summary>Raised whenever stack state changes.</summary>
    public event Action? OnStackChanged;

    /// <summary>Creates a command stack with the requested maximum undo depth.</summary>
    public DocumentEditorCommandStack(int maxDepth = 100)
    {
        _maxDepth = Math.Max(1, maxDepth);
    }

    /// <summary>Whether there is a command to undo.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Whether there is a command to redo.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Description of the next command to undo.</summary>
    public string? NextUndoDescription => _undoStack.Last?.Value.Description;

    /// <summary>Description of the next command to redo.</summary>
    public string? NextRedoDescription => _redoStack.Count == 0 ? null : _redoStack.Peek().Description;

    /// <summary>Whether a batch is currently collecting commands.</summary>
    public bool IsInBatch => _currentBatch is not null;

    /// <summary>Executes a command and records it as an undo step.</summary>
    public async Task PushAsync(IDocumentEditorCommand command)
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
        {
            _undoStack.RemoveFirst();
        }

        _redoStack.Clear();
        OnStackChanged?.Invoke();
    }

    /// <summary>Begins collecting commands into one undoable batch.</summary>
    public void BeginBatch(string description)
    {
        if (_currentBatch is not null)
        {
            throw new InvalidOperationException("A document editor command batch is already open.");
        }

        _currentBatch = new BatchDocumentEditorCommand(description);
    }

    /// <summary>Commits the open batch as one undo step.</summary>
    public void CommitBatch()
    {
        if (_currentBatch is null)
        {
            throw new InvalidOperationException("No document editor command batch is open.");
        }

        var batch = _currentBatch;
        _currentBatch = null;
        if (batch.Count == 0)
        {
            return;
        }

        _undoStack.AddLast(batch);
        if (_undoStack.Count > _maxDepth)
        {
            _undoStack.RemoveFirst();
        }

        _redoStack.Clear();
        OnStackChanged?.Invoke();
    }

    /// <summary>Rolls back and discards the open batch.</summary>
    public async Task RollbackBatchAsync()
    {
        if (_currentBatch is null)
        {
            throw new InvalidOperationException("No document editor command batch is open.");
        }

        var batch = _currentBatch;
        _currentBatch = null;
        if (batch.Count > 0)
        {
            await batch.UndoAsync();
        }

        OnStackChanged?.Invoke();
    }

    /// <summary>Undoes the last command when available.</summary>
    public async Task UndoAsync()
    {
        if (!CanUndo)
        {
            return;
        }

        var command = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        await command.UndoAsync();
        _redoStack.Push(command);
        OnStackChanged?.Invoke();
    }

    /// <summary>Redoes the last undone command when available.</summary>
    public async Task RedoAsync()
    {
        if (!CanRedo)
        {
            return;
        }

        var command = _redoStack.Pop();
        await command.ExecuteAsync();
        _undoStack.AddLast(command);
        OnStackChanged?.Invoke();
    }

    /// <summary>Clears all undo and redo state.</summary>
    public async Task ClearAsync()
    {
        if (_currentBatch is not null)
        {
            await RollbackBatchAsync();
        }

        _undoStack.Clear();
        _redoStack.Clear();
        OnStackChanged?.Invoke();
    }
}
