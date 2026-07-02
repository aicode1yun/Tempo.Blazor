using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

public sealed class RenderNodeConverter : JsonConverter<RenderNode>
{
    public override RenderNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Render node must be a JSON object.");

        RenderNodeKind? kind = null;
        var attributes = new Dictionary<string, object?>(StringComparer.Ordinal);
        string? when = null;
        string? text = null;
        string? value = null;
        string? prop = null;
        string? @as = null;
        IReadOnlyList<RenderNode> children = [];
        IReadOnlyDictionary<string, object?> props = new Dictionary<string, object?>();
        RenderNode? node = null;

        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name)
            {
                case "kind":
                    kind = property.Value.Deserialize<RenderNodeKind>(options);
                    break;
                case "when":
                    when = property.Value.GetString();
                    break;
                case "text":
                    text = property.Value.GetString();
                    break;
                case "value":
                    value = property.Value.GetString();
                    break;
                case "children":
                    children = property.Value.Deserialize<List<RenderNode>>(options) ?? [];
                    break;
                case "props":
                    props = property.Value.Deserialize<Dictionary<string, object?>>(options)
                            ?? new Dictionary<string, object?>();
                    break;
                case "prop":
                    prop = property.Value.GetString();
                    break;
                case "as":
                    @as = property.Value.GetString();
                    break;
                case "node":
                    node = property.Value.Deserialize<RenderNode>(options);
                    break;
                default:
                    attributes[property.Name] = property.Value.Deserialize<object?>(options);
                    break;
            }
        }

        if (kind is null)
            throw new JsonException("Render node is missing required property 'kind'.");

        return new RenderNode
        {
            Kind = kind.Value,
            Attributes = attributes,
            When = when,
            Text = text,
            Value = value,
            Children = children,
            Props = props,
            Prop = prop,
            As = @as,
            Node = node
        };
    }

    public override void Write(Utf8JsonWriter writer, RenderNode value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("kind");
        JsonSerializer.Serialize(writer, value.Kind, options);

        foreach (var attribute in value.Attributes)
        {
            if (IsReservedProperty(attribute.Key))
                throw new JsonException($"Render node attribute '{attribute.Key}' conflicts with a reserved property.");

            writer.WritePropertyName(attribute.Key);
            JsonSerializer.Serialize(writer, attribute.Value, options);
        }

        WriteString(writer, "when", value.When);
        WriteString(writer, "text", value.Text);
        WriteString(writer, "value", value.Value);
        WriteDictionary(writer, "props", value.Props, options);
        WriteList(writer, "children", value.Children, options);
        WriteString(writer, "prop", value.Prop);
        WriteString(writer, "as", value.As);

        if (value.Node is not null)
        {
            writer.WritePropertyName("node");
            JsonSerializer.Serialize(writer, value.Node, options);
        }

        writer.WriteEndObject();
    }

    private static void WriteString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
            writer.WriteString(propertyName, value);
    }

    private static void WriteList<T>(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<T> values,
        JsonSerializerOptions options)
    {
        if (values.Count == 0)
            return;

        writer.WritePropertyName(propertyName);
        JsonSerializer.Serialize(writer, values, options);
    }

    private static void WriteDictionary(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyDictionary<string, object?> values,
        JsonSerializerOptions options)
    {
        if (values.Count == 0)
            return;

        writer.WritePropertyName(propertyName);
        JsonSerializer.Serialize(writer, values, options);
    }

    private static bool IsReservedProperty(string propertyName)
    {
        return propertyName is "kind" or "when" or "text" or "value" or "children" or "props" or "prop" or "as" or "node";
    }
}
