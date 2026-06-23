namespace Tempo.Blazor.Reporting.Services;

/// <summary>Helpers for stateless report viewer interaction tokens.</summary>
public static class ReportInteractionState
{
    /// <summary>Toggles a key in a stable comma-separated interaction token.</summary>
    public static string Toggle(string? token, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return token ?? string.Empty;
        }

        var values = Parse(token);
        if (!values.Add(key))
        {
            values.Remove(key);
        }

        return string.Join(",", values.Order(StringComparer.Ordinal));
    }

    /// <summary>Returns true when the interaction token contains a key.</summary>
    public static bool Contains(string? token, string key)
        => Parse(token).Contains(key);

    private static HashSet<string> Parse(string? token)
        => string.IsNullOrWhiteSpace(token)
            ? new HashSet<string>(StringComparer.Ordinal)
            : token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);
}
