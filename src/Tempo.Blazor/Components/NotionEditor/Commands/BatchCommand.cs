using Tempo.Blazor.NotionEditor.Commands;

namespace Tempo.Blazor.Components.NotionEditor.Commands;

/// <summary>
/// Groups multiple <see cref="INotionCommand"/> instances into a single undo / redo step.
///
/// Commands are executed in insertion order by <see cref="ExecuteAsync"/> and reversed
/// (last-to-first) by <see cref="UndoAsync"/>.
/// This is the type used internally by <see cref="NotionCommandStack"/> when a batch scope
/// is open; it can also be built externally and pushed as a single atomic command.
/// </summary>
public sealed class BatchCommand : INotionCommand
{
    private readonly List<INotionCommand> _commands;

    public BatchCommand(string description, IEnumerable<INotionCommand> commands)
    {
        Description = description;
        _commands   = [.. commands];
    }

    public BatchCommand(string description)
    {
        Description = description;
        _commands   = [];
    }

    public string Description { get; }

    /// <summary>Number of commands in this batch.</summary>
    public int Count => _commands.Count;

    /// <summary>Appends a command. The command must NOT yet have been executed.</summary>
    public void Add(INotionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
    }

    public async Task ExecuteAsync()
    {
        foreach (var cmd in _commands)
            await cmd.ExecuteAsync();
    }

    public async Task UndoAsync()
    {
        for (var i = _commands.Count - 1; i >= 0; i--)
            await _commands[i].UndoAsync();
    }
}
