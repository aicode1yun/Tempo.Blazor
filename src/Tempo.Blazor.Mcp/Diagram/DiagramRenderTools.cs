using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Mcp.Diagram;

/// <summary>MCP tool that renders a diagram document to a static SVG string for embedding/preview.</summary>
[McpServerToolType]
public static class DiagramRenderTools
{
    [McpServerTool(Name = "diagram_render_svg")]
    [Description("Render a diagram document (DiagramDocument JSON — e.g. from diagram_get_document or modeling_get_view) to a static SVG string, without a browser. Options: theme ('light' default or 'dark'), pageIndex, width, height, padding, includeGrid, backgroundColor. Requires the host to register the diagram SVG renderer (AddTempoBlazorDiagramEditor); returns 'unsupported' when it is not available.")]
    public static string RenderSvg(
        [Description("Full diagram document JSON to render.")] string documentJson,
        IDiagramSvgRenderer? renderer = null,
        [Description("Colour theme: 'light' (default) or 'dark'.")] string? theme = null,
        [Description("Zero-based page index to render. Omit for the active page.")] int? pageIndex = null,
        [Description("Explicit output width. Omit (with height) to auto-fit to content.")] double? width = null,
        [Description("Explicit output height. Omit (with width) to auto-fit to content.")] double? height = null,
        [Description("Padding around auto-fitted content in pixels (default 20).")] double padding = 20,
        [Description("Draw a dotted grid behind the diagram.")] bool includeGrid = false,
        [Description("Override the theme background colour (any CSS colour).")] string? backgroundColor = null)
    {
        if (renderer is null)
        {
            return McpToolResults.Failure(McpToolResults.Unsupported, "No diagram SVG renderer is registered on the host (call AddTempoBlazorDiagramEditor).");
        }

        if (!DiagramSerializer.TryDeserialize(documentJson, out var document) || document is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The diagram document JSON could not be parsed.");
        }

        var options = new DiagramSvgRenderOptions
        {
            Theme = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? DiagramSvgTheme.Dark : DiagramSvgTheme.Light,
            PageIndex = pageIndex,
            Width = width,
            Height = height,
            Padding = padding,
            IncludeGrid = includeGrid,
            BackgroundColor = backgroundColor
        };

        var svg = renderer.RenderSvg(document, options);
        return McpToolResults.Success(new { svg });
    }
}
