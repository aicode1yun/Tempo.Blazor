using System.Text.Json;
using System.Text.Json.Serialization;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Serialization;

/// <summary>
/// Custom JSON converter for <see cref="DiagramDocument"/> that supports
/// reading both the legacy v1.0 format (flat Nodes/Edges/Layers/Width/Height)
/// and the modern v2.0 multi-page format.
/// </summary>
public sealed class DiagramDocumentJsonConverter : JsonConverter<DiagramDocument>
{
    public override DiagramDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var document = new DiagramDocument
        {
            Id = GetStringProperty(root, "id") ?? Guid.NewGuid().ToString(),
            Version = GetStringProperty(root, "version") ?? "1.0",
            Title = GetStringProperty(root, "title") ?? "Untitled diagram",
            CreatedAt = GetDateTimeProperty(root, "createdAt") ?? DateTime.UtcNow,
            ModifiedAt = GetDateTimeProperty(root, "modifiedAt") ?? DateTime.UtcNow,
        };

        // v2.0 multi-page format
        if (root.TryGetProperty("pages", out var pagesElement) && pagesElement.ValueKind == JsonValueKind.Array)
        {
            document.Pages = JsonSerializer.Deserialize<List<DiagramPage>>(pagesElement.GetRawText(), options) ?? [];
            document.ActivePageIndex = GetIntProperty(root, "activePageIndex") ?? 0;
        }
        // v1.0 legacy flat format -> migrate to single page
        else
        {
            var page = new DiagramPage
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Page 1",
                Width = GetDoubleProperty(root, "width") ?? 3000,
                Height = GetDoubleProperty(root, "height") ?? 2000,
                Nodes = DeserializeList<DiagramNode>(root, "nodes", options),
                Edges = DeserializeList<DiagramEdge>(root, "edges", options),
                Layers = DeserializeList<DiagramLayer>(root, "layers", options),
            };
            document.Pages = [page];
            document.ActivePageIndex = 0;
            document.Version = "2.0"; // bump after migration
        }

        if (root.TryGetProperty("lastUsedEdgeStyle", out var lastStyleEl) && lastStyleEl.ValueKind == JsonValueKind.Object)
        {
            document.LastUsedEdgeStyle = JsonSerializer.Deserialize<DiagramEdgeStyleSnapshot>(lastStyleEl.GetRawText(), options);
        }

        document.EnsurePages();
        return document;
    }

    public override void Write(Utf8JsonWriter writer, DiagramDocument value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteStringProperty("version", value.Version);
        writer.WriteStringProperty("id", value.Id);
        writer.WriteStringProperty("title", value.Title);
        writer.WriteNumberProperty("activePageIndex", value.ActivePageIndex);

        writer.WritePropertyName("pages");
        JsonSerializer.Serialize(writer, value.Pages, options);

        writer.WriteStringProperty("createdAt", value.CreatedAt);
        writer.WriteStringProperty("modifiedAt", value.ModifiedAt);

        if (value.LastUsedEdgeStyle is not null)
        {
            writer.WritePropertyName("lastUsedEdgeStyle");
            JsonSerializer.Serialize(writer, value.LastUsedEdgeStyle, options);
        }

        writer.WriteEndObject();
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
            return property.GetString();
        return null;
    }

    private static int? GetIntProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number)
            return property.GetInt32();
        return null;
    }

    private static double? GetDoubleProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number)
            return property.GetDouble();
        return null;
    }

    private static DateTime? GetDateTimeProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            var text = property.GetString();
            if (!string.IsNullOrEmpty(text) && DateTime.TryParse(text, out var dt))
                return dt;
        }
        return null;
    }

    private static List<T> DeserializeList<T>(JsonElement element, string propertyName, JsonSerializerOptions options)
    {
        if (element.TryGetProperty(propertyName, out var property))
            return JsonSerializer.Deserialize<List<T>>(property.GetRawText(), options) ?? [];
        return [];
    }
}

file static class JsonWriterExtensions
{
    public static void WriteStringProperty(this Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
            writer.WriteString(name, value);
    }

    public static void WriteStringProperty(this Utf8JsonWriter writer, string name, DateTime value)
    {
        writer.WriteString(name, value);
    }

    public static void WriteNumberProperty(this Utf8JsonWriter writer, string name, int value)
    {
        writer.WriteNumber(name, value);
    }
}
