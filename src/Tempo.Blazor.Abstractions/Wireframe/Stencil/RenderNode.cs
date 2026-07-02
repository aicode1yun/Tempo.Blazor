namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>JSON render tree node used by stencil components.</summary>
public sealed class RenderNode
{
    public required RenderNodeKind Kind { get; init; }

    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();

    public string? When { get; init; }

    public string? Text { get; init; }

    public string? Value { get; init; }

    public IReadOnlyList<RenderNode> Children { get; init; } = [];

    public IReadOnlyDictionary<string, object?> Props { get; init; } = new Dictionary<string, object?>();

    public string? Prop { get; init; }

    public string? As { get; init; }

    public RenderNode? Node { get; init; }
}

public enum RenderNodeKind
{
    Group,
    Rect,
    Text,
    Line,
    Path,
    Icon,
    Spinner,
    Image,
    Svg,
    Component,
    Stack,
    Row,
    Grid,
    Repeat,
    Part
}
