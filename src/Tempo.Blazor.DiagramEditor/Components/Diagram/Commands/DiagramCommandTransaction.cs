namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>
/// Groups multiple diagram commands into a single undo/redo unit.
/// </summary>
public sealed class DiagramCommandTransaction : IDiagramCommand
{
    private readonly List<IDiagramCommand> _commands = [];

    /// <summary>Human-readable name shown in undo/redo tooltips.</summary>
    public string Name { get; }

    /// <summary>Creates a new transaction with the given display name.</summary>
    public DiagramCommandTransaction(string name)
    {
        Name = name;
    }

    /// <summary>Number of commands captured in this transaction.</summary>
    public int Count => _commands.Count;

    /// <summary>Gets the command at the specified index.</summary>
    public IDiagramCommand GetCommand(int index) => _commands[index];

    /// <summary>Adds a command to this transaction without executing it again (assumes already executed).</summary>
    public void Add(IDiagramCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
    }

    /// <inheritdoc/>
    public void Execute()
    {
        foreach (var command in _commands)
        {
            command.Execute();
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}
