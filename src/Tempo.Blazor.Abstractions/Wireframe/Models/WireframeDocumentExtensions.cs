using System.Text.Json;

namespace Tempo.Blazor.Components.Wireframe.Models;

/// <summary>Extension and factory helpers for <see cref="WireframeDocument"/> and related models.</summary>
public static class WireframeDocumentExtensions
{
    /// <summary>
    /// Creates a new <see cref="WireframeElement"/> with default dimensions.
    /// When a <paramref name="defaultWidth"/> / <paramref name="defaultHeight"/> are provided
    /// (typically from a <c>WireframeComponentDef</c>) they override the built-in fallbacks.
    /// </summary>
    public static WireframeElement NewElement(
        string type,
        double x,
        double y,
        double defaultWidth = 120,
        double defaultHeight = 36)
    {
        return new WireframeElement
        {
            Type = type,
            X = x,
            Y = y,
            W = defaultWidth,
            H = defaultHeight
        };
    }

    /// <summary>
    /// Creates a new <see cref="WireframeElement"/> and pre-populates its <see cref="WireframeElement.Props"/>
    /// from the <paramref name="propDefs"/> defaults. This ensures the canvas renders correctly
    /// immediately after drop — without relying on <c>GetBool</c>/<c>GetString</c> fallbacks.
    /// Props with a <c>null</c> default are skipped (the render fallback handles those).
    /// </summary>
    public static WireframeElement NewElement(
        string type,
        double x,
        double y,
        double defaultWidth,
        double defaultHeight,
        IEnumerable<PropDef> propDefs)
    {
        var el = new WireframeElement
        {
            Type = type,
            X = x,
            Y = y,
            W = defaultWidth,
            H = defaultHeight
        };

        foreach (var prop in propDefs)
        {
            if (prop.Default is not null)
                el.SetProp(prop.Name, prop.Default);
        }

        return el;
    }

    /// <summary>
    /// Deep-clones the document via JSON roundtrip.
    /// Used by Undo/Redo – not on a hot path, so JSON overhead is acceptable.
    /// </summary>
    public static WireframeDocument Clone(this WireframeDocument document)
    {
        var json = JsonSerializer.Serialize(document, WireframeJsonOptions.Default);
        return JsonSerializer.Deserialize<WireframeDocument>(json, WireframeJsonOptions.Default)!;
    }

    /// <summary>
    /// Returns the string value of a prop, or <paramref name="fallback"/> when the key is absent
    /// or the value is not a JSON string.
    /// </summary>
    public static string GetString(this Dictionary<string, JsonElement> props, string key, string fallback = "")
    {
        if (props.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString() ?? fallback;
        return fallback;
    }

    /// <summary>Returns the boolean value of a prop, or <paramref name="fallback"/>.</summary>
    public static bool GetBool(this Dictionary<string, JsonElement> props, string key, bool fallback = false)
    {
        if (!props.TryGetValue(key, out var el)) return fallback;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    /// <summary>Returns the integer value of a prop, or <paramref name="fallback"/>.</summary>
    public static int GetInt(this Dictionary<string, JsonElement> props, string key, int fallback = 0)
    {
        if (props.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.TryGetInt32(out var v) ? v : fallback;
        return fallback;
    }

    /// <summary>Returns the double value of a prop, or <paramref name="fallback"/>.</summary>
    public static double GetDouble(this Dictionary<string, JsonElement> props, string key, double fallback = 0.0)
    {
        if (props.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.TryGetDouble(out var v) ? v : fallback;
        return fallback;
    }

    /// <summary>
    /// Returns a string array from a JSON array prop, or an empty array when absent/wrong type.
    /// </summary>
    public static string[] GetStringList(this Dictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array)
            return [];

        return [.. el.EnumerateArray()
            .Where(i => i.ValueKind == JsonValueKind.String)
            .Select(i => i.GetString()!)];
    }

    /// <summary>
    /// Sets a prop value. Accepts any JSON-serialisable object.
    /// Passing <c>null</c> removes the key.
    /// </summary>
    public static void SetProp(this WireframeElement element, string key, object? value)
    {
        if (value is null)
        {
            element.Props.Remove(key);
            return;
        }

        var json = JsonSerializer.Serialize(value, WireframeJsonOptions.Default);
        element.Props[key] = JsonDocument.Parse(json).RootElement.Clone();
    }
}
