using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class EdgeSelectionOutlineTests : LocalizationTestBase
{
    public EdgeSelectionOutlineTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void SelectedEdge_RendersOutlinePath()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 0, W = 100, H = 50 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        // Select the edge by clicking the hit path
        var hitPath = cut.Find("path.tm-diagram-edge-hit-path");
        hitPath.Click();
        cut.Render();

        var outline = cut.Find("path.tm-diagram-edge-path--selected-outline");
        outline.Should().NotBeNull();
        var d = outline.GetAttribute("d");
        d.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void UnselectedEdge_DoesNotRenderOutlinePath()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 0, W = 100, H = 50 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var outlines = cut.FindAll("path.tm-diagram-edge-path--selected-outline");
        outlines.Should().BeEmpty();
    }
}
