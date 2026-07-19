using FluentAssertions;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class TmDiagramCanvasTests : DiagramTestBase
{
    [Fact]
    public void ComputeEdgePath_CurvedRouting_ReturnsValidSvgPath()
    {
        var doc = new DiagramDocument
        {
            Title = "Test",
            Width = 1000,
            Height = 1000,
            Nodes =
            [
                new DiagramNode { Id = "n1", StencilId = "rect", X = 0, Y = 0, W = 100, H = 100 },
                new DiagramNode { Id = "n2", StencilId = "rect", X = 200, Y = 0, W = 100, H = 100 }
            ],
            Edges =
            [
                new DiagramEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "n2", Routing = "curved", CubicBezier = true }
            ]
        };

        var cut = Render<TmDiagramCanvas>(parameters => parameters
            .Add(p => p.Document, doc));

        var pathElement = cut.Find("path[data-edge-id=\"e1\"]");
        var d = pathElement.GetAttribute("d");
        d.Should().StartWith("M");
        d.Should().Contain("C");
    }

    [Fact]
    public void ComputeEdgePath_StraightRouting_DoesNotContainCurvedCommand()
    {
        var doc = new DiagramDocument
        {
            Title = "Test",
            Width = 1000,
            Height = 1000,
            Nodes =
            [
                new DiagramNode { Id = "n1", StencilId = "rect", X = 0, Y = 0, W = 100, H = 100 },
                new DiagramNode { Id = "n2", StencilId = "rect", X = 200, Y = 0, W = 100, H = 100 }
            ],
            Edges =
            [
                new DiagramEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "n2", Routing = "straight" }
            ]
        };

        var cut = Render<TmDiagramCanvas>(parameters => parameters
            .Add(p => p.Document, doc));

        var pathElement = cut.Find("path[data-edge-id=\"e1\"]");
        var d = pathElement.GetAttribute("d");
        d.Should().StartWith("M");
        d.Should().NotContain("C");
    }

    [Fact]
    public void ToggleLayerVisibility_HidesNodesOnLayer()
    {
        var layer1 = new DiagramLayer { Id = "l1", Name = "Layer 1", Order = 0, IsVisible = true };
        var layer2 = new DiagramLayer { Id = "l2", Name = "Layer 2", Order = 1, IsVisible = true };
        var doc = new DiagramDocument
        {
            Title = "Test",
            Width = 1000,
            Height = 1000,
            Layers = { layer1, layer2 },
            Nodes =
            [
                new DiagramNode { Id = "n1", StencilId = "rect", X = 0, Y = 0, W = 100, H = 100, LayerId = "l1" },
                new DiagramNode { Id = "n2", StencilId = "rect", X = 200, Y = 0, W = 100, H = 100, LayerId = "l2" }
            ]
        };

        var cut = Render<TmDiagramCanvas>(parameters => parameters
            .Add(p => p.Document, doc));

        cut.FindAll("[data-node-id]").Count.Should().Be(2);

        layer1.IsVisible = false;
        cut.Render(parameters => parameters.Add(p => p.Document, doc));

        var nodes = cut.FindAll("[data-node-id]");
        nodes.Count.Should().Be(1);
        nodes[0].GetAttribute("data-node-id").Should().Be("n2");
    }
}
