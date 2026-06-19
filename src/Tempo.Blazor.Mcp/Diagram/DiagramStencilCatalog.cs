using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Mcp.Diagram;

/// <summary>Projects diagram stencil providers into compact, LLM-friendly catalog shapes.</summary>
public static class DiagramStencilCatalog
{
    public static IReadOnlyList<DiagramStencil> All(IEnumerable<IDiagramStencilProvider>? providers)
        => (providers ?? [])
            .OrderByDescending(p => p.Priority)
            .SelectMany(p => p.GetStencilSets())
            .SelectMany(s => s.Stencils)
            .Where(s => !string.IsNullOrWhiteSpace(s.Id))
            .GroupBy(s => s.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(s => s.SetId, StringComparer.Ordinal)
            .ThenBy(s => s.PaletteOrder)
            .ThenBy(s => s.Order)
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .ToList();

    public static object Compact(DiagramStencil s) => new
    {
        id = s.Id,
        name = s.Name,
        category = s.Category,
        setId = s.SetId,
        paletteId = s.PaletteId,
        kind = s.Kind.ToString(),
        defaultWidth = s.DefaultWidth,
        defaultHeight = s.DefaultHeight,
        tags = s.Tags,
        keywords = s.Keywords
    };

    public static object Full(DiagramStencil s) => new
    {
        id = s.Id,
        name = s.Name,
        nameResourceKey = s.NameResourceKey,
        category = s.Category,
        setId = s.SetId,
        setNameResourceKey = s.SetNameResourceKey,
        paletteId = s.PaletteId,
        paletteNameResourceKey = s.PaletteNameResourceKey,
        kind = s.Kind.ToString(),
        defaultWidth = s.DefaultWidth,
        defaultHeight = s.DefaultHeight,
        ports = s.Ports,
        connectionPoints = s.ConnectionPoints,
        layout = s.Layout,
        isCollapsible = s.IsCollapsible,
        isSwimlane = s.IsSwimlane,
        isTable = s.IsTable,
        edgeDefaults = s.EdgeDefaults,
        defaultData = s.DefaultData,
        tags = s.Tags,
        keywords = s.Keywords,
        origin = s.Origin.ToString(),
        externalAssetSourceId = s.ExternalAssetSourceId
    };

    public static HashSet<string>? BuildKnownStencilIds(IEnumerable<IDiagramStencilProvider>? providers)
    {
        var ids = All(providers)
            .Select(s => s.Id)
            .ToHashSet(StringComparer.Ordinal);

        return ids.Count == 0 ? null : ids;
    }

    public static string? SuggestId(IEnumerable<IDiagramStencilProvider>? providers, string unknown)
    {
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var stencil in All(providers))
        {
            var d = Levenshtein(unknown, stencil.Id);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = stencil.Id;
            }
        }

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
