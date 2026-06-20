using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Mcp.Diagram;

/// <summary>MCP tools for discovering diagram/draw stencil definitions.</summary>
[McpServerToolType]
public static class DiagramStencilCatalogTools
{
    [McpServerTool(Name = "diagram_list_stencils")]
    [Description("List diagram/draw stencils available to create nodes or edges. Start with compact=true to keep the response small, then call diagram_get_stencil for the full contract. Optionally filter by category, setId, paletteId or kind.")]
    public static string ListStencils(
        IEnumerable<IDiagramStencilProvider> stencilProviders,
        [Description("Return compact projection when true.")] bool compact = true,
        [Description("Optional category filter, e.g. UML or BPMN.")] string? category = null,
        [Description("Optional stencil set filter.")] string? setId = null,
        [Description("Optional palette filter.")] string? paletteId = null,
        [Description("Optional kind filter, e.g. Node or Edge.")] string? kind = null,
        [Description("Pagination offset.")] int skip = 0,
        [Description("Maximum number of stencils to return (default 200).")] int take = 200)
    {
        var stencils = DiagramStencilCatalog.All(stencilProviders).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            stencils = stencils.Where(s => string.Equals(s.Category, category, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(setId))
        {
            stencils = stencils.Where(s => string.Equals(s.SetId, setId, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(paletteId))
        {
            stencils = stencils.Where(s => string.Equals(s.PaletteId, paletteId, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(kind))
        {
            stencils = stencils.Where(s => string.Equals(s.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase));
        }

        var all = stencils.ToList();
        var page = all
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 500))
            .Select(s => compact ? DiagramStencilCatalog.Compact(s) : DiagramStencilCatalog.Full(s))
            .ToList();

        return McpToolResults.Success(new
        {
            totalCount = all.Count,
            items = page
        });
    }

    [McpServerTool(Name = "diagram_get_stencil")]
    [Description("Return the full diagram/draw stencil contract for one stencil id. Suggests a correction when the id is misspelled.")]
    public static string GetStencil(
        IEnumerable<IDiagramStencilProvider> stencilProviders,
        [Description("Stencil id, e.g. uml.class.")] string id)
    {
        var stencil = DiagramStencilCatalog.All(stencilProviders)
            .FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

        if (stencil is null)
        {
            var suggestion = DiagramStencilCatalog.SuggestId(stencilProviders, id);
            var message = suggestion is null
                ? $"Diagram stencil '{id}' not found."
                : $"Diagram stencil '{id}' not found. Did you mean '{suggestion}'?";
            return McpToolResults.Failure(McpToolResults.NotFound, message);
        }

        return McpToolResults.Success(new { stencil = DiagramStencilCatalog.Full(stencil) });
    }
}
