namespace Tempo.Blazor.E2E.CanvasEngine;

/// <summary>Named viewport used by canvas document editor visual gates.</summary>
public sealed record CanvasEngineViewport(string Name, int Width, int Height)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}
