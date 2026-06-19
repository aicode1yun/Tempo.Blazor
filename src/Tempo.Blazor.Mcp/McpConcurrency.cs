namespace Tempo.Blazor.Mcp;

/// <summary>Shared optimistic-concurrency helpers for MCP write tools.</summary>
internal static class McpConcurrency
{
    public static string? DateTimeConflict(
        DateTime? expected,
        DateTime current,
        string readToolName)
    {
        if (expected is null)
        {
            return null;
        }

        return Math.Abs((current - expected.Value).TotalMilliseconds) > 1
            ? $"The document was modified since you read it (current modifiedAt {current:O}). Re-read with {readToolName} and retry."
            : null;
    }

    public static string? TokenConflict(
        string? expected,
        string? current,
        string readToolName)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return null;
        }

        return !string.Equals(expected, current, StringComparison.Ordinal)
            ? $"The document was modified since you read it. Re-read with {readToolName} and retry."
            : null;
    }
}
