namespace Tempo.Blazor.Components.Wireframe;

/// <summary>Thrown when a wireframe JSON string cannot be deserialized.</summary>
public sealed class WireframeDeserializationException : Exception
{
    /// <inheritdoc/>
    public WireframeDeserializationException(string message) : base(message) { }

    /// <inheritdoc/>
    public WireframeDeserializationException(string message, Exception inner) : base(message, inner) { }
}
