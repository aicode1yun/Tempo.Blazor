namespace Tempo.Blazor.Components.Diagram.Serialization;

/// <summary>Thrown when a diagram JSON string cannot be deserialized.</summary>
public sealed class DiagramDeserializationException : Exception
{
    /// <inheritdoc/>
    public DiagramDeserializationException(string message) : base(message) { }

    /// <inheritdoc/>
    public DiagramDeserializationException(string message, Exception inner) : base(message, inner) { }
}
