namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Immutable data available to stencil expression evaluation.</summary>
public sealed record StencilEvalContext(
    IReadOnlyDictionary<string, object?> Props,
    double SizeW,
    double SizeH,
    int RepeatIndex,
    StencilTokenResolver? Tokens)
{
    public static StencilEvalContext Empty { get; } = new(
        new Dictionary<string, object?>(),
        0,
        0,
        0,
        null);

    /// <summary>Returns a copy with a different repeat index; never throws.</summary>
    public StencilEvalContext WithRepeatIndex(int repeatIndex) => this with { RepeatIndex = repeatIndex };
}
