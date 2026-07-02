using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

internal sealed class StencilJsonValueConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => ReadNumber(ref reader),
            JsonTokenType.StartObject => ReadObject(ref reader, options),
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            _ => throw new JsonException($"Unsupported JSON token '{reader.TokenType}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case string stringValue:
                writer.WriteStringValue(stringValue);
                break;
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case long longValue:
                writer.WriteNumberValue(longValue);
                break;
            case float floatValue:
                writer.WriteNumberValue(floatValue);
                break;
            case double doubleValue:
                writer.WriteNumberValue(doubleValue);
                break;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                break;
            case IDictionary<string, object?> dictionary:
                WriteDictionary(writer, dictionary, options);
                break;
            case IEnumerable<object?> values:
                WriteArray(writer, values, options);
                break;
            case IDictionary dictionary:
                WriteDictionary(writer, dictionary, options);
                break;
            case IEnumerable values:
                WriteArray(writer, values, options);
                break;
            default:
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
                break;
        }
    }

    private static object ReadNumber(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt32(out var intValue))
            return intValue;

        if (reader.TryGetInt64(out var longValue))
            return longValue;

        return reader.GetDouble();
    }

    private static Dictionary<string, object?> ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var value = new Dictionary<string, object?>(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return value;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a property name.");

            var propertyName = reader.GetString() ?? throw new JsonException("JSON property name cannot be null.");
            reader.Read();
            value[propertyName] = JsonSerializer.Deserialize<object?>(ref reader, options);
        }

        throw new JsonException("Unexpected end of JSON object.");
    }

    private static List<object?> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var value = new List<object?>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return value;

            value.Add(JsonSerializer.Deserialize<object?>(ref reader, options));
        }

        throw new JsonException("Unexpected end of JSON array.");
    }

    private static void WriteDictionary(Utf8JsonWriter writer, IDictionary<string, object?> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var item in value)
        {
            writer.WritePropertyName(item.Key);
            JsonSerializer.Serialize(writer, item.Value, options);
        }

        writer.WriteEndObject();
    }

    private static void WriteDictionary(Utf8JsonWriter writer, IDictionary value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (DictionaryEntry item in value)
        {
            if (item.Key is not string key)
                throw new JsonException("Stencil JSON dictionaries require string keys.");

            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, item.Value, options);
        }

        writer.WriteEndObject();
    }

    private static void WriteArray(Utf8JsonWriter writer, IEnumerable<object?> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            JsonSerializer.Serialize(writer, item, options);
        writer.WriteEndArray();
    }

    private static void WriteArray(Utf8JsonWriter writer, IEnumerable value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            JsonSerializer.Serialize(writer, item, options);
        writer.WriteEndArray();
    }
}
