using Tempo.Blazor.Components.Diagram.Commands;

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

    /// <inheritdoc/>
    public void Push(IDiagramCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Attempt coalescing for move commands
        if (command is MoveNodesCommand newMove
            && _undoStack.Last?.Value is MoveNodesCommand prevMove
            && prevMove.TryCoalesce(newMove))
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
        _undoStack.Clear();
        _redoStack.Clear();
        OnStackChanged?.Invoke();
    }
}
