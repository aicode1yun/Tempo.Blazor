namespace Tempo.Blazor.Components.Wireframe.Stencil;

using Tempo.Blazor.Components.Wireframe.Models;

/// <summary>Describes one component or primitive that can be placed on a wireframe canvas.</summary>
public sealed class StencilComponent
{
    public required string Type { get; init; }

    public required string DisplayName { get; init; }

    public required string Category { get; init; }

    public string? Icon { get; init; }

    public required StencilSize DefaultSize { get; init; }

    public StencilSize? MinSize { get; init; }

    public StencilSize? MaxSize { get; init; }

    public IReadOnlyDictionary<string, StencilSize> SizePresets { get; init; } = new Dictionary<string, StencilSize>();

    public StencilResize Resize { get; init; } = StencilResize.Scale;

    public StencilSlice? Slice { get; init; }

    public IReadOnlyList<PropDef> Props { get; init; } = [];

    public IReadOnlyList<string> ContentSlots { get; init; } = [];

    public StencilImpl? Impl { get; init; }

    public RenderNode? Render { get; init; }

    public StencilNative? Native { get; init; }
}
