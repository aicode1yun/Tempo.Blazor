using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Reporting.Abstractions.Serialization;

/// <summary>JSON options for report server API DTO payloads.</summary>
public static class ReportApiJsonSerializer
{
    /// <summary>Canonical API JSON options.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
