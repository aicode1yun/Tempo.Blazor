namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Runtime component implementation and default parameter bindings.</summary>
public sealed class StencilImpl
{
    public required string Component { get; init; }

    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}
