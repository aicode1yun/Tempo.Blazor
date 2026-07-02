namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Root document for a reusable wireframe stencil pack.</summary>
public sealed class StencilPack
{
    public required string Format { get; init; }

    public required int FormatVersion { get; init; }

    public required string Id { get; init; }

    public required string Namespace { get; init; }

    public bool IsBuiltIn { get; init; }

    public StencilTarget? Target { get; init; }

    public IReadOnlyDictionary<string, string> Tokens { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Themes { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, string>>();

    public IReadOnlyDictionary<string, string> Icons { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, RenderNode> Parts { get; init; } = new Dictionary<string, RenderNode>();

    public IReadOnlyList<StencilComponent> Components { get; init; } = [];
}
