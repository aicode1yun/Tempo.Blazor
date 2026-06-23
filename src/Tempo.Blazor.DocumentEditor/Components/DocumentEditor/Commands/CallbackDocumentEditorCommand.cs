namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Undoable command backed by asynchronous callbacks.</summary>
public sealed class CallbackDocumentEditorCommand : IDocumentEditorCommand
{
    private readonly Func<Task> _execute;
    private readonly Func<Task> _undo;
    private bool _skipNextExecute;

    /// <summary>Creates a callback command.</summary>
    public CallbackDocumentEditorCommand(
        string description,
        Func<Task> execute,
        Func<Task> undo,
        bool skipInitialExecute = false)
    {
        Description = string.IsNullOrWhiteSpace(description) ? "Update document" : description;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _skipNextExecute = skipInitialExecute;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task ExecuteAsync()
    {
        if (_skipNextExecute)
        {
            _skipNextExecute = false;
            return Task.CompletedTask;
        }

        return _execute();
    }

    /// <inheritdoc />
    public Task UndoAsync() => _undo();
}
