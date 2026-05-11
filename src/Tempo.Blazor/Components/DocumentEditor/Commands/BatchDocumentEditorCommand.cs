namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Groups several document editor commands into one undo and redo step.</summary>
public sealed class BatchDocumentEditorCommand : IDocumentEditorCommand
{
    private readonly List<IDocumentEditorCommand> _commands;

    /// <summary>Creates a batch with an initial command set.</summary>
    public BatchDocumentEditorCommand(string description, IEnumerable<IDocumentEditorCommand> commands)
    {
        Description = description;
        _commands = [.. commands];
    }

    /// <summary>Creates an empty batch that can be appended to.</summary>
    public BatchDocumentEditorCommand(string description)
    {
        Description = description;
        _commands = [];
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <summary>Number of commands in the batch.</summary>
    public int Count => _commands.Count;

    /// <summary>Adds a command that has already been executed by the command stack.</summary>
    public void Add(IDocumentEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync()
    {
        foreach (var command in _commands)
        {
            await command.ExecuteAsync();
        }
    }

    /// <inheritdoc />
    public async Task UndoAsync()
    {
        for (var i = _commands.Count - 1; i >= 0; i--)
        {
            await _commands[i].UndoAsync();
        }
    }
}
