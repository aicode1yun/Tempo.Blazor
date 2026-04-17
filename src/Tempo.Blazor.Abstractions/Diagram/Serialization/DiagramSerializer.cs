using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Serialization;

/// <summary>
/// Serializes and deserializes <see cref="DiagramDocument"/> to/from JSON.
/// </summary>
public static class DiagramSerializer
{
    /// <summary>
    /// Serializes <paramref name="document"/> to an indented JSON string.
    /// Also updates <see cref="DiagramDocument.ModifiedAt"/> to UTC now.
    /// </summary>
    public static string Serialize(DiagramDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.EnsurePages();
        document.ModifiedAt = DateTime.UtcNow;
        return JsonSerializer.Serialize(document, DiagramJsonOptions.Default);
    }

    /// <summary>
    /// Deserializes a JSON string to a <see cref="DiagramDocument"/>.
    /// </summary>
    /// <returns>The deserialized document.</returns>
    /// <exception cref="DiagramDeserializationException">
    /// Thrown when the JSON is malformed or the root object is missing.
    /// </exception>
    public static DiagramDocument Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        DiagramDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<DiagramDocument>(json, DiagramJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new DiagramDeserializationException("Invalid diagram JSON.", ex);
        }

        if (document is null)
            throw new DiagramDeserializationException("Diagram JSON deserialized to null.");

        document.EnsurePages();
        return document;
    }

    /// <summary>
    /// Tries to deserialize without throwing.
    /// </summary>
    /// <returns><c>true</c> on success; <c>false</c> and <c>null</c> document on failure.</returns>
    public static bool TryDeserialize(string json, out DiagramDocument? document)
    {
        try
        {
            document = Deserialize(json);
            return true;
        }
        catch (DiagramDeserializationException)
        {
            document = null;
            return false;
        }
    }
}
