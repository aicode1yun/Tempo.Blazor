using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Configuration;

namespace Tempo.Blazor.Tests.Diagram;

public class DiagramSvgRendererTests
{
    private static DiagramStencilRegistry BuildRegistry()
    {
        var registry = new DiagramStencilRegistry();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
        registry.RegisterProvider(new Uml25DiagramStencilProvider());
        registry.RegisterProvider(new Bpmn2DiagramStencilProvider());
        return registry;
    }

    private static DiagramSvgRenderer BuildRenderer() => new(BuildRegistry());

    private static DiagramDocument BasicDocument()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();
        var page = doc.Pages[0];

        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 40, Y = 40, W = 120, H = 60 };
        n1.Data["label"] = "Alpha";
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 240, Y = 40, W = 120, H = 60 };
        n2.Data["label"] = "Beta";
        n2.Style.Fill = "#fde68a";

        page.Nodes.Add(n1);
        page.Nodes.Add(n2);
        page.Edges.Add(new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            ConnectorType = "association",
            Label = "flows"
        });
        return doc;
    }

    [Fact]
    public void RenderSvg_Produces_Well_Formed_Svg_With_Nodes_And_Edges()
    {
        var svg = BuildRenderer().RenderSvg(BasicDocument());

        svg.Should().StartWith("<svg xmlns=\"http://www.w3.org/2000/svg\"");
        svg.Should().EndWith("</svg>");
        svg.Should().Contain("id=\"nodes\"");
        svg.Should().Contain("id=\"edges\"");
        svg.Should().Contain("Alpha");
        svg.Should().Contain("flows");
        svg.Should().Contain("marker-end=\"url(#arrow-association)\"");
    }

    [Fact]
    public void RenderSvg_Is_Deterministic()
    {
        var renderer = BuildRenderer();
        var doc = BasicDocument();

        var first = renderer.RenderSvg(doc);
        var second = renderer.RenderSvg(doc);

        second.Should().Be(first);
    }

    [Fact]
    public void RenderSvg_Custom_Node_Fill_Appears_In_Output()
    {
        var svg = BuildRenderer().RenderSvg(BasicDocument());

        svg.Should().Contain("fill=\"#fde68a\"");
    }

    [Fact]
    public void RenderSvg_Empty_Document_Still_Renders_Background_Without_Node_Groups()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();

        var svg = BuildRenderer().RenderSvg(doc);

        svg.Should().StartWith("<svg");
        svg.Should().EndWith("</svg>");
        svg.Should().Contain("id=\"nodes\""); // the nodes layer is present
        CountOccurrences(svg, "<g transform=\"translate(").Should().Be(0); // but holds no node groups
    }

    [Fact]
    public void RenderSvg_Large_Graph_Completes_And_Contains_All_Nodes()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();
        var page = doc.Pages[0];
        for (int i = 0; i < 400; i++)
        {
            page.Nodes.Add(new DiagramNode
            {
                StencilId = "general.rectangle",
                X = (i % 20) * 160,
                Y = (i / 20) * 100,
                W = 120,
                H = 60
            });
        }

        var svg = BuildRenderer().RenderSvg(doc);

        // Every node emits one translate group inside the nodes layer.
        CountOccurrences(svg, "<g transform=\"translate(").Should().Be(400);
    }

    [Fact]
    public void RenderSvg_Dark_Theme_Uses_Dark_Background_And_Differs_From_Light()
    {
        var renderer = BuildRenderer();
        var doc = BasicDocument();

        var light = renderer.RenderSvg(doc, new DiagramSvgRenderOptions { Theme = DiagramSvgTheme.Light });
        var dark = renderer.RenderSvg(doc, new DiagramSvgRenderOptions { Theme = DiagramSvgTheme.Dark });

        dark.Should().Contain("fill=\"#1e1e2e\"");
        dark.Should().NotBe(light);
        // The explicit custom node fill must survive the theme switch.
        dark.Should().Contain("fill=\"#fde68a\"");
    }

    [Fact]
    public void RenderSvg_BackgroundColor_Override_Wins_Over_Theme()
    {
        var svg = BuildRenderer().RenderSvg(BasicDocument(), new DiagramSvgRenderOptions { BackgroundColor = "#123456" });

        svg.Should().Contain("fill=\"#123456\"");
    }

    [Fact]
    public void RenderSvg_Does_Not_Emit_Script_Or_Event_Handlers()
    {
        var doc = BasicDocument();
        // Attempt an injection through a user-controlled label.
        doc.Pages[0].Edges[0].Label = "<script>alert(1)</script>";
        doc.Pages[0].Nodes[0].Data["label"] = "\"><img onerror=alert(1) src=x>";

        var svg = BuildRenderer().RenderSvg(doc);

        // Angle brackets and quotes from user content must be escaped, so no live tag or
        // attribute can form. (Inert text like "onerror=" may survive as escaped body text.)
        svg.Should().NotContain("<script");
        svg.Should().NotContain("javascript:");
        svg.Should().NotContain("<img");
        svg.Should().Contain("&lt;script&gt;"); // the injections were neutralised, not dropped
        svg.Should().Contain("&lt;img");
    }

    [Fact]
    public void AddTempoBlazorDiagramEditor_Registers_Singleton_Renderer()
    {
        var services = new ServiceCollection();
        services.AddTempoBlazorDiagramEditor();
        using var provider = services.BuildServiceProvider();

        var a = provider.GetService<IDiagramSvgRenderer>();
        var b = provider.GetService<IDiagramSvgRenderer>();

        a.Should().NotBeNull();
        a.Should().BeOfType<DiagramSvgRenderer>();
        b.Should().BeSameAs(a); // singleton
        a!.RenderSvg(BasicDocument()).Should().Contain("<svg");
    }

    [Theory]
    [InlineData(DiagramSvgTheme.Light, "diagram-svg-basic-light.svg")]
    [InlineData(DiagramSvgTheme.Dark, "diagram-svg-basic-dark.svg")]
    public void RenderSvg_Matches_Golden(DiagramSvgTheme theme, string goldenFile)
    {
        var svg = BuildRenderer().RenderSvg(BasicDocument(), new DiagramSvgRenderOptions { Theme = theme });
        var path = GoldenPath(goldenFile);

        if (Environment.GetEnvironmentVariable("TEMPO_REGENERATE_DIAGRAM_SVG") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, svg);
            return;
        }

        File.Exists(path).Should().BeTrue($"golden file '{goldenFile}' must exist (regenerate with TEMPO_REGENERATE_DIAGRAM_SVG=1)");
        var golden = File.ReadAllText(path);
        svg.Should().Be(golden);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static string GoldenPath(string fileName, [CallerFilePath] string callerPath = "")
        => Path.Combine(Path.GetDirectoryName(callerPath)!, "DiagramSvgGolden", fileName);
}
