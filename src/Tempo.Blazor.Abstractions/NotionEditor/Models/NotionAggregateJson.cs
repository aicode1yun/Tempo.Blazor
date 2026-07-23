using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Canonical JSON settings for the Notion aggregate authoring contract.</summary>
public static class NotionAggregateJson
{
    /// <summary>
    /// Gets the shared serializer options used by providers, tools, and contract tests.
    /// </summary>
    /// <remarks>
    /// Public contract members also carry explicit JSON names. The web defaults and camel-case enum
    /// converter pin the remaining wire conventions without relying on a host application's options.
    /// </remarks>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = false
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
