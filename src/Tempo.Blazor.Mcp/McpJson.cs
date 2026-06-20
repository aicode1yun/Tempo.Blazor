using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.Mcp;

/// <summary>Shared JSON options for MCP tool input parsing and output formatting.</summary>
public static class McpJson
{
    /// <summary>camelCase, indented, null-ignoring, string enums — readable for an LLM.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
