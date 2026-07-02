using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Wireframe;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>LLM-facing authoring guide for wireframe canvas conventions and component props.</summary>
[McpServerToolType]
public static class WireframeAuthoringGuideTools
{
    [McpServerTool(Name = "wireframe_get_authoring_guide")]
    [Description("Return the wireframe authoring guide for the current component catalog: canvas conventions, sizing and variant guidance, supported layout ops and the full prop vocabulary. Pass scopeAppId to include app-scoped custom components alongside Tempo built-ins.")]
    public static string GetAuthoringGuideScoped(
        WireframeSchemaRegistry registry,
        [Description("Optional app id. When set, include Tempo baseline components plus custom components scoped to this app.")] string? scopeAppId = null)
    {
        var scope = WireframeComponentScope.FromAppId(scopeAppId);
        var components = registry.GetAll(scope)
            .Select(WireframeCatalog.Full)
            .ToList();

        return McpToolResults.Success(new
        {
            canvas = new
            {
                desktopWidth = WireframeArchetypes.DesktopWidth,
                mobileWidth = WireframeArchetypes.MobileWidth,
                navbarHeight = WireframeArchetypes.NavbarHeight,
                sectionSpacing = WireframeArchetypes.SectionSpacing
            },
            sizing = new
            {
                defaultSize = "Omit w/h to use the resolved component schema defaultWidth/defaultHeight.",
                auto = "Use w or h = 'auto' in addElement/layout children to resolve the component default size.",
                fill = "Use w or h = 'fill' in addElement/layout children to consume the page or layout span after padding.",
                presets = "When a component exposes a size prop with sizePresets, that preset seeds missing w/h unless explicit dimensions are supplied."
            },
            variants = new
            {
                enums = "Enum props are case-normalized when possible; invalid enum values are returned as warnings.",
                strict = "strict=true rejects prop/enum warning batches and scaffold missing-schema warnings before saving."
            },
            layoutOps = new
            {
                operations = new[] { "stack", "row", "grid" },
                sentinels = new[] { "auto", "fill" },
                anchors = new[] { "below", "rightOf" },
                commonParams = new[] { "pageId", "children", "ids", "gap", "padding", "columns", "wrap", "margin", "x", "y", "w", "h" }
            },
            archetypes = WireframeArchetypes.Names,
            propVocabulary = components
        });
    }

    public static string GetAuthoringGuide(WireframeSchemaRegistry registry)
        => GetAuthoringGuideScoped(registry);
}
