using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.Components.Wireframe.Models;

/// <summary>Shared <see cref="JsonSerializerOptions"/> used across all wireframe serialization.</summary>
public static class WireframeJsonOptions
{
    /// <summary>
    /// camelCase, indented, write-indented, ignore null,
    /// enums as strings for AI readability.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
