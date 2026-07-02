namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Maps a stencil component to its runtime implementation.</summary>
public sealed class StencilTarget
{
    public string? Framework { get; init; }

    public string? Library { get; init; }

    public string? Version { get; init; }
}
