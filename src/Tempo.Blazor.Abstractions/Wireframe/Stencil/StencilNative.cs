namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Native DOM fallback for stencils that do not need a render tree.</summary>
public sealed class StencilNative
{
    public required string NativeType { get; init; }

    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}
