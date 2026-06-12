using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Wireframe;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>
/// MCP tools that let an LLM discover which Tempo.Blazor components it can place in a wireframe
/// and how to configure them.
/// </summary>
[McpServerToolType]
public static class WireframeComponentCatalogTools
{
    [McpServerTool(Name = "wireframe_list_components")]
    [Description("List the wireframe components available to place on a design. Start with compact=true to keep the response small, then call wireframe_get_component_schema for the full property contract of a chosen type. Optionally filter by category.")]
    public static string ListComponents(
        WireframeSchemaRegistry registry,
        [Description("When true, return only type/category/displayName. Recommended first call.")] bool compact = true,
        [Description("Optional category filter (e.g. 'Buttons', 'Inputs', 'Layout').")] string? category = null,
        [Description("Pagination offset.")] int skip = 0,
        [Description("Maximum number of components to return (default 200).")] int take = 200)
    {
        var all = string.IsNullOrWhiteSpace(category)
            ? registry.GetAll()
            : registry.GetByCategory(category);

        var ordered = all.ToList();
        var page = ordered.Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 1000));
        var items = page.Select(s => compact ? WireframeCatalog.Compact(s) : WireframeCatalog.Full(s)).ToList();

        return McpToolResults.Success(new
        {
            totalCount = ordered.Count,
            categories = registry.GetCategories().ToList(),
            items
        });
    }

    [McpServerTool(Name = "wireframe_get_component_schema")]
    [Description("Get the full property contract for one wireframe component type (dimensions and every prop with its type, default and allowed values). Returns not_found with a suggestion when the type is misspelled.")]
    public static string GetComponentSchema(
        WireframeSchemaRegistry registry,
        [Description("The component type id, e.g. 'TmButton'.")] string type)
    {
        var schema = registry.GetSchema(type);
        if (schema is null)
        {
            var suggestion = WireframeCatalog.SuggestType(registry, type);
            var message = suggestion is null
                ? $"Unknown component type '{type}'."
                : $"Unknown component type '{type}'. Did you mean '{suggestion}'?";
            return McpToolResults.Failure(McpToolResults.NotFound, message);
        }

        return McpToolResults.Success(new { component = WireframeCatalog.Full(schema) });
    }
}
