namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>
/// Represents a single reversible user action in the wireframe editor.
/// </summary>
public interface IWireframeCommand
{
    /// <summary>Human-readable name for debugging and future history UI.</summary>
    string Name { get; }

    /// <summary>Apply the change to the document.</summary>
    void Execute();

    /// <summary>Reverse the change previously applied by <see cref="Execute"/>.</summary>
    void Undo();
}
