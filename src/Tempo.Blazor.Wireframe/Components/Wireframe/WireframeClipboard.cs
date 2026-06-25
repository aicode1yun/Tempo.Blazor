using System.Text.Json;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>Static clipboard for wireframe format painter (style + size).</summary>
public static class WireframeClipboard
{
    /// <summary>Copied style properties from a source element.</summary>
    public static Dictionary<string, JsonElement>? StyleProps { get; set; }

    /// <summary>Copied width (set when CopyStyleCommand includes size).</summary>
    public static double? Width { get; set; }

    /// <summary>Copied height (set when CopyStyleCommand includes size).</summary>
    public static double? Height { get; set; }

    /// <summary>Whether the clipboard contains any style properties.</summary>
    public static bool HasStyle => StyleProps is not null && StyleProps.Count > 0;

    /// <summary>Deep-clones a props dictionary.</summary>
    public static Dictionary<string, JsonElement> CloneProps(Dictionary<string, JsonElement> source)
    {
        var clone = new Dictionary<string, JsonElement>(source.Count);
        foreach (var kv in source)
        {
            // Serialize and deserialize to create a deep clone of the JsonElement
            var json = JsonSerializer.Serialize(kv.Value);
            clone[kv.Key] = JsonSerializer.Deserialize<JsonElement>(json);
        }
        return clone;
    }
}
