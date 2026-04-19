using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class RubberBandEdgeSelectionTests : LocalizationTestBase
{
    public RubberBandEdgeSelectionTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public async Task MultiSelect_IncludingEdge_RendersEdgeOutline()
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

        // Simulate JS rubber-band selecting the edge
        await cut.Instance.OnMultiSelect([edge.Id]);
        cut.Render();

        var outline = cut.Find("path.tm-diagram-edge-path--selected-outline");
        outline.Should().NotBeNull();
    }

    [Fact]
    public async Task MultiSelect_MixedNodeAndEdge_RendersBothSelected()
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

        // Simulate JS rubber-band selecting both node and edge
        await cut.Instance.OnMultiSelect([n1.Id, edge.Id]);
        cut.Render();

        var outlines = cut.FindAll("path.tm-diagram-edge-path--selected-outline");
        outlines.Count.Should().Be(1);

        // Node should also be selected (selection outlines rendered by JS, but we can verify
        // the edge selection state is maintained)
        cut.Instance.GetType().GetField("_currentSelectionIds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(cut.Instance)!.Should().BeEquivalentTo(new[] { n1.Id, edge.Id });
    }
}
