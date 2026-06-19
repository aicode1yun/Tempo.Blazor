using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tempo.Blazor.Mcp;

/// <summary>Shared JSON helpers used by MCP operation engines.</summary>
internal static class McpJsonHelpers
{
    public static bool TryParseOperationArray(
        string operationsJson,
        out JsonArray? operations,
        out IReadOnlyList<string> errors)
    {
        operations = null;
        try
        {
            if (JsonNode.Parse(operationsJson) is not JsonArray parsed)
            {
                errors = ["operations: expected a JSON array of operations."];
                return false;
            }

            operations = parsed;
            errors = [];
            return true;
        }
        catch (JsonException ex)
        {
            errors = [$"operations: invalid JSON ({ex.Message})."];
            return false;
        }
    }

    public static T Clone<T>(T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value, options);
        return JsonSerializer.Deserialize<T>(json, options)
            ?? throw new JsonException($"Could not clone {typeof(T).Name}.");
    }
}
