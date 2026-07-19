using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class CardinalityRenderingTests : LocalizationTestBase
{
    public CardinalityRenderingTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void EdgeWithCardinality_RendersTextElements()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 0, W = 100, H = 50 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            SourceCardinality = "1",
            TargetCardinality = "*"
        };
        doc.Edges.Add(edge);

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var texts = cut.FindAll("text.tm-diagram-edge-cardinality");
        texts.Count.Should().Be(2);
        texts[0].TextContent.Should().Be("1");
        texts[1].TextContent.Should().Be("*");
    }

    [Fact]
    public void EdgeWithoutCardinality_DoesNotRenderTextElements()
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

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();
        var texts = cut.FindAll("text.tm-diagram-edge-cardinality");
        texts.Should().BeEmpty();
    }
}
