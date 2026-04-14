using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.Components.Diagram.Serialization;

/// <summary>Shared <see cref="JsonSerializerOptions"/> used across all diagram serialization.</summary>
public static class DiagramJsonOptions
{
    /// <summary>
    /// camelCase, indented, ignore null, enums as strings for AI readability.
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
