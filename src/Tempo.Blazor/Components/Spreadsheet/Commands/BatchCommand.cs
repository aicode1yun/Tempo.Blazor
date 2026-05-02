namespace Tempo.Blazor.Components.Spreadsheet.Commands;

/// <summary>
/// Groups multiple commands into a single undoable unit.
/// </summary>
public sealed class BatchCommand : ISpreadsheetCommand
{
    private readonly List<ISpreadsheetCommand> _commands = [];

    public void Add(ISpreadsheetCommand command)
    {
        _commands.Add(command);
    }

    public void Execute()
    {
        foreach (var command in _commands)
        {
            command.Execute();
        }
    }

    public void Undo()
    {
        for (var i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}
