using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Blazor.Reporting.Interop;

/// <summary>Serializes report parameter values for remote report viewer HTTP calls.</summary>
public sealed class ReportParameterValueJsonConverter : JsonConverter<ReportParameterValue>
{
    /// <inheritdoc />
    public override ReportParameterValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return ReportParameterValue.Multiple(ReadArray(ref reader));
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return ReportParameterValue.Scalar(ReadSingleValue(ref reader));
        }

        var values = new List<object?>();
        object? scalarValue = null;
        var hasValues = false;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString();
            reader.Read();
            if (string.Equals(propertyName, "values", StringComparison.OrdinalIgnoreCase))
            {
                values.AddRange(ReadArray(ref reader));
                hasValues = true;
            }
            else if (string.Equals(propertyName, "scalarValue", StringComparison.OrdinalIgnoreCase))
            {
                scalarValue = ReadSingleValue(ref reader);
            }
            else
            {
                reader.Skip();
            }
        }

        return hasValues ? ReportParameterValue.Multiple(values) : ReportParameterValue.Scalar(scalarValue);
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        ReportParameterValue value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WritePropertyName("values");
        writer.WriteStartArray();
        foreach (var item in value.Values)
        {
            WriteSingleValue(writer, item, options);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("scalarValue");
        WriteSingleValue(writer, value.ScalarValue, options);
        writer.WriteEndObject();
    }

    private static IReadOnlyList<object?> ReadArray(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            return [ReadSingleValue(ref reader)];
        }

        var values = new List<object?>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            values.Add(ReadSingleValue(ref reader));
        }

        return values;
    }

    private static object? ReadSingleValue(ref Utf8JsonReader reader)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => ReadStringValue(ref reader),
            JsonTokenType.Number => ReadNumberValue(ref reader),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.StartObject or JsonTokenType.StartArray => JsonDocument.ParseValue(ref reader).RootElement.Clone(),
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone(),
        };

    private static object? ReadStringValue(ref Utf8JsonReader reader)
    {
        var value = reader.GetString();
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        return value;
    }

    private static object ReadNumberValue(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var int64Value))
        {
            return int64Value;
        }

        if (reader.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        return reader.GetDouble();
    }

    private static void WriteSingleValue(
        Utf8JsonWriter writer,
        object? value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value is JsonElement jsonElement)
        {
            jsonElement.WriteTo(writer);
            return;
        }

        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
