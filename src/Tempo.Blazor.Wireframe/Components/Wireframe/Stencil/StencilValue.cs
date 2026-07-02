using System.Globalization;
using System.Text.Json;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Safe value wrapper used by the stencil expression evaluator.</summary>
public readonly record struct StencilValue(object? Raw)
{
    public static StencilValue Null { get; } = new(null);

    public bool IsNull => Raw is null || Raw is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

    /// <summary>Coerces the value to a string; null-like values return an empty string and never throw.</summary>
    public string AsString()
    {
        return Raw switch
        {
            null => string.Empty,
            string value => value,
            bool value => value ? "true" : "false",
            double value => value.ToString(CultureInfo.InvariantCulture),
            float value => value.ToString(CultureInfo.InvariantCulture),
            decimal value => value.ToString(CultureInfo.InvariantCulture),
            int value => value.ToString(CultureInfo.InvariantCulture),
            long value => value.ToString(CultureInfo.InvariantCulture),
            JsonElement element => JsonElementToString(element),
            _ => Convert.ToString(Raw, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    /// <summary>Coerces the value to a number; invalid values return 0 and never throw.</summary>
    public double AsDouble()
    {
        return Raw switch
        {
            null => 0,
            double value => value,
            float value => value,
            decimal value => (double)value,
            int value => value,
            long value => value,
            bool value => value ? 1 : 0,
            string value when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            JsonElement element => JsonElementToDouble(element),
            _ => 0
        };
    }

    /// <summary>Coerces the value to a boolean; null-like values return false and never throw.</summary>
    public bool AsBool()
    {
        return Raw switch
        {
            null => false,
            bool value => value,
            double value => Math.Abs(value) > double.Epsilon,
            float value => Math.Abs(value) > float.Epsilon,
            decimal value => value != 0,
            int value => value != 0,
            long value => value != 0,
            string value => StringToBool(value),
            JsonElement element => JsonElementToBool(element),
            _ => true
        };
    }

    internal bool IsNumeric(out double value)
    {
        value = AsDouble();
        return Raw switch
        {
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => true,
            JsonElement element => element.ValueKind == JsonValueKind.Number,
            string text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }

    private static string JsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static double JsonElementToDouble(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDouble(out var value) => value,
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) => value,
            JsonValueKind.True => 1,
            _ => 0
        };
    }

    private static bool JsonElementToBool(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetDouble(out var value) => Math.Abs(value) > double.Epsilon,
            JsonValueKind.String => StringToBool(element.GetString() ?? string.Empty),
            _ => false
        };
    }

    private static bool StringToBool(string value)
    {
        if (bool.TryParse(value, out var parsed))
            return parsed;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return Math.Abs(number) > double.Epsilon;

        return !string.IsNullOrWhiteSpace(value);
    }
}
