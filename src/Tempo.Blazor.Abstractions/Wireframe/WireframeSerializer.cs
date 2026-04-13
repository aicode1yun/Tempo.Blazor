using System.Text.Json;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Serializes and deserializes <see cref="WireframeDocument"/> to/from JSON.
///
/// The JSON format is intentionally AI-friendly:
/// <list type="bullet">
///   <item>camelCase keys</item>
///   <item>Indented output</item>
///   <item>Enums as strings</item>
///   <item>Null values omitted</item>
/// </list>
/// </summary>
public static class WireframeSerializer
{
    /// <summary>
    /// Serializes <paramref name="document"/> to an indented JSON string.
    /// Also updates <see cref="WireframeDocument.ModifiedAt"/> to UTC now.
    /// </summary>
    public static string Serialize(WireframeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.ModifiedAt = DateTime.UtcNow;
        return JsonSerializer.Serialize(document, WireframeJsonOptions.Default);
    }

    /// <summary>
    /// Deserializes a JSON string to a <see cref="WireframeDocument"/>.
    /// </summary>
    /// <returns>The deserialized document.</returns>
    /// <exception cref="WireframeDeserializationException">
    /// Thrown when the JSON is malformed or the root object is missing.
    /// </exception>
    public static WireframeDocument Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        WireframeDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<WireframeDocument>(json, WireframeJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new WireframeDeserializationException("Invalid wireframe JSON.", ex);
        }

        if (document is null)
            throw new WireframeDeserializationException("Wireframe JSON deserialized to null.");

        return document;
    }

    /// <summary>
    /// Tries to deserialize without throwing.
    /// </summary>
    /// <returns><c>true</c> on success; <c>false</c> and <c>null</c> document on failure.</returns>
    public static bool TryDeserialize(string json, out WireframeDocument? document)
    {
        try
        {
            document = Deserialize(json);
            return true;
        }
        catch (WireframeDeserializationException)
        {
            document = null;
            return false;
        }
    }
}
