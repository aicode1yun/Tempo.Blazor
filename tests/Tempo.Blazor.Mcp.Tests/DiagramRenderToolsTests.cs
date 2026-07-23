using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Mcp.Diagram;

namespace Tempo.Blazor.Mcp.Tests;

/// <summary>Tests for the diagram_render_svg MCP tool.</summary>
public class DiagramRenderToolsTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>Fake renderer backed by the real shared builder (no stencils resolved).</summary>
    private sealed class FakeSvgRenderer : IDiagramSvgRenderer
    {
        public string RenderSvg(DiagramDocument document, DiagramSvgRenderOptions? options = null)
        {
            document.EnsurePages();
            options ??= new DiagramSvgRenderOptions();
            var palette = DiagramSvgPalette.ForTheme(options.Theme);
            return DiagramSvgBuilder.Build(document.ActivePage, options.ToExportOptions(), palette, _ => null);
        }
    }

    private static string SampleDocumentJson()
    {
        var doc = new DiagramDocument { Title = "Sample" };
        doc.EnsurePages();
        var node = new DiagramNode { StencilId = "general.rectangle", X = 40, Y = 40, W = 120, H = 60 };
        node.Data["label"] = "Node";
        doc.Pages[0].Nodes.Add(node);
        return JsonSerializer.Serialize(doc, DiagramJsonOptions.Default);
    }

    [Fact]
    public void RenderSvg_ValidDocument_ReturnsSvg()
    {
        var result = Parse(DiagramRenderTools.RenderSvg(SampleDocumentJson(), new FakeSvgRenderer()));

        result.GetProperty("success").GetBoolean().Should().BeTrue();
        result.GetProperty("svg").GetString().Should().StartWith("<svg");
    }

    [Fact]
    public void RenderSvg_DarkTheme_UsesDarkBackground()
    {
        var result = Parse(DiagramRenderTools.RenderSvg(SampleDocumentJson(), new FakeSvgRenderer(), theme: "dark"));

        result.GetProperty("svg").GetString().Should().Contain("#1e1e2e");
    }

    [Fact]
    public void RenderSvg_WithoutRenderer_ReturnsUnsupported()
    {
        var result = Parse(DiagramRenderTools.RenderSvg(SampleDocumentJson(), renderer: null));

        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("error").GetString().Should().Be("unsupported");
    }

    [Fact]
    public void RenderSvg_InvalidJson_ReturnsValidationFailed()
    {
        var result = Parse(DiagramRenderTools.RenderSvg("{ not valid", new FakeSvgRenderer()));

        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("error").GetString().Should().Be("validation_failed");
    }
}
