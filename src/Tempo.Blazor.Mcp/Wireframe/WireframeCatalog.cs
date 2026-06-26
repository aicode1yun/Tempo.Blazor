using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>
/// Pure helpers that project <see cref="WireframeSchemaRegistry"/> entries into LLM-friendly
/// shapes and suggest the nearest type name for typos. Used by the catalog MCP tools.
/// </summary>
public static class WireframeCatalog
{
    /// <summary>Compact projection: just enough to pick a component.</summary>
    public static object Compact(WireframeComponentSchema s) => new
    {
        type = s.Type,
        category = s.Category,
        displayName = s.DisplayName
    };

    /// <summary>Full projection: dimensions and the property contract.</summary>
    public static object Full(WireframeComponentSchema s) => new
    {
        type = s.Type,
        category = s.Category,
        displayName = s.DisplayName,
        defaultWidth = s.DefaultWidth,
        defaultHeight = s.DefaultHeight,
        localType = s.LocalType ?? WireframeComponentScope.GetLocalType(s.Type),
        scopeAppId = s.ScopeAppId,
        isBuiltIn = s.IsBuiltIn,
        props = s.Props.Select(p => new
        {
            name = p.Name,
            displayName = p.DisplayName,
            type = p.Type.ToString(),
            @default = p.Default,
            options = p.Options,
            required = p.IsRequired
        }).ToList()
    };

    /// <summary>Returns the registered type whose name is closest to <paramref name="unknown"/>, if reasonably near.</summary>
    public static string? SuggestType(WireframeSchemaRegistry registry, string unknown)
        => SuggestType(registry, unknown, scope: null);

    /// <summary>Returns the registered type whose name is closest to <paramref name="unknown"/> in <paramref name="scope"/>, if reasonably near.</summary>
    public static string? SuggestType(WireframeSchemaRegistry registry, string unknown, WireframeComponentScope? scope)
    {
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var schema in registry.GetAll(scope))
        {
            var localType = schema.LocalType ?? WireframeComponentScope.GetLocalType(schema.Type);
            var d = Math.Min(
                Levenshtein(unknown, schema.Type),
                Levenshtein(unknown, localType));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = schema.Type;
            }
        }

        // Only suggest when the names are plausibly the same intent.
        var threshold = Math.Max(2, unknown.Length / 2);
        return bestDistance <= threshold ? best : null;
    }

    internal static int Levenshtein(string a, string b)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
    }
}
