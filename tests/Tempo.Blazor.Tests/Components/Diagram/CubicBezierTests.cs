using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class CubicBezierTests : LocalizationTestBase
{
    public CubicBezierTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void CurvedRouting_WithCubicBezier_RendersCPath()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 100, W = 100, H = 50 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            Routing = "curved",
            CubicBezier = true
        };
        doc.Edges.Add(edge);

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var path = cut.Find("path.tm-diagram-edge-path");
        var d = path.GetAttribute("d");
        d.Should().Contain(" C ");
        d.Should().NotContain(" Q ");
    }

    [Fact]
    public void CurvedRouting_WithoutCubicBezier_RendersQPath()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 100, W = 100, H = 50 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            Routing = "curved",
            CubicBezier = false
        };
        doc.Edges.Add(edge);

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var path = cut.Find("path.tm-diagram-edge-path");
        var d = path.GetAttribute("d");
        d.Should().Contain(" Q ");
        d.Should().NotContain(" C ");
    }
}
