namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>
/// Undo/redo history for a single diagram editor instance.
///
/// <para>
/// Not a DI service. <c>TmDiagramEditor</c> creates one instance and cascades it
/// to child components via <c>CascadingValue</c>. Each editor on the page gets its
/// own isolated stack.
/// </para>
/// </summary>
public sealed class DiagramCommandStack : IDiagramCommandStack
{
    private readonly int _maxDepth;
    private readonly LinkedList<IDiagramCommand> _undoStack = new();
    private readonly Stack<IDiagramCommand> _redoStack = new();
    private DiagramCommandTransaction? _currentTransaction;

    /// <inheritdoc/>
    public event Action? OnStackChanged;

    /// <param name="maxDepth">Maximum undo steps retained (default 50).</param>
    public DiagramCommandStack(int maxDepth = 50)
    {
        _maxDepth = maxDepth;
    }

    /// <inheritdoc/>
    public bool CanUndo => _undoStack.Count > 0;

    /// <inheritdoc/>
    public bool CanRedo => _redoStack.Count > 0;

    /// <inheritdoc/>
    public string? NextUndoName => _undoStack.Last?.Value.Name;

    /// <inheritdoc/>
    public string? NextRedoName => _redoStack.Count > 0 ? _redoStack.Peek().Name : null;

    /// <summary>Whether a transaction is currently open.</summary>
    public bool IsInTransaction => _currentTransaction is not null;

    /// <summary>Begins a new transactional scope. Nested transactions are not allowed.</summary>
    public void BeginTransaction(string name)
    {
        if (_currentTransaction is not null)
            throw new InvalidOperationException("A transaction is already in progress. Nested transactions are not supported.");

        _currentTransaction = new DiagramCommandTransaction(name);
    }

    /// <summary>Commits the current transaction as a single undo/redo step.</summary>
    public void CommitTransaction()
    {
        if (_currentTransaction is null)
            throw new InvalidOperationException("No transaction is in progress.");

        var tx = _currentTransaction;
        _currentTransaction = null;

        if (tx.Count == 0)
            return;

        _undoStack.AddLast(tx);
        if (_undoStack.Count > _maxDepth)
            _undoStack.RemoveFirst();

        _redoStack.Clear();
        OnStackChanged?.Invoke();
    }

    /// <summary>Rolls back the current transaction without recording it.</summary>
    public void RollbackTransaction()
    {
        if (_currentTransaction is null)
            throw new InvalidOperationException("No transaction is in progress.");

        var tx = _currentTransaction;
        _currentTransaction = null;

        if (tx.Count > 0)
            tx.Undo();
    }

    /// <summary>Returns an <see cref="IDisposable" /> wrapper that begins and commits/rolls back a transaction.</summary>
    public IDisposable TransactionScope(string name)
    {
        BeginTransaction(name);
        return new TransactionScopeDisposable(this);
    }

    /// <inheritdoc/>
    public void Push(IDiagramCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_currentTransaction is not null)
        {
            // Attempt coalescing for move commands inside a transaction
            if (command is MoveNodesCommand newMove
                && _currentTransaction.Count > 0
                && _currentTransaction.GetCommand(_currentTransaction.Count - 1) is MoveNodesCommand prevMove
                && prevMove.TryCoalesce(newMove))
            {
                OnStackChanged?.Invoke();
                return;
            }

            command.Execute();
            _currentTransaction.Add(command);
            OnStackChanged?.Invoke();
            return;
        }

        // Attempt coalescing for move commands
        if (command is MoveNodesCommand newMove2
            && _undoStack.Last?.Value is MoveNodesCommand prevMove2
            && prevMove2.TryCoalesce(newMove2))
        {
            OnStackChanged?.Invoke();
            return;
        }

        command.Execute();

        _undoStack.AddLast(command);
        if (_undoStack.Count > _maxDepth)
            _undoStack.RemoveFirst();

        _redoStack.Clear();
        OnStackChanged?.Invoke();
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        cmd.Undo();
        _redoStack.Push(cmd);
        OnStackChanged?.Invoke();
    }

    /// <inheritdoc/>
    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.AddLast(cmd);
        OnStackChanged?.Invoke();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (_currentTransaction is not null)
        {
            RollbackTransaction();
        }
        _undoStack.Clear();
        _redoStack.Clear();
        OnStackChanged?.Invoke();
    }

    private sealed class TransactionScopeDisposable : IDisposable
    {
        private readonly DiagramCommandStack _stack;
        private bool _disposed;

        public TransactionScopeDisposable(DiagramCommandStack stack)
        {
            _stack = stack;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_stack.IsInTransaction)
            {
                try
                {
                    _stack.CommitTransaction();
                }
                catch
                {
                    _stack.RollbackTransaction();
                    throw;
                }
            }
        }
    }
}
