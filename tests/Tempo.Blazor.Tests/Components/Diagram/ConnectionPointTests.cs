using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class ConnectionPointTests : LocalizationTestBase
{
    public ConnectionPointTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Theory]
    [InlineData("general.rectangle", 9)]
    [InlineData("general.rounded", 9)]
    [InlineData("general.ellipse", 8)]
    [InlineData("general.rhombus", 5)]
    [InlineData("general.triangle", 6)]
    [InlineData("general.hexagon", 8)]
    [InlineData("uml.class", 9)]
    [InlineData("uml.actor", 8)]
    public void BuiltInStencil_HasExpectedConnectionPoints(string stencilId, int expectedCount)
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        var stencil = registry.GetStencil(stencilId);
        stencil.Should().NotBeNull();
        stencil!.ConnectionPoints.Count.Should().Be(expectedCount);
    }

    [Fact]
    public async Task Canvas_RenderedNode_HasConnectionPointElements()
    {
        var doc = new DiagramDocument();
        doc.Nodes.Add(new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 100,
            Y = 100,
            W = 120,
            H = 60
        });

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();

        var points = cut.FindAll(".tm-diagram-connection-point");
        points.Count.Should().Be(9);
    }

    [Fact]
    public async Task Canvas_ConnectionPoints_HaveCorrectDataAttributes()
    {
        var doc = new DiagramDocument();
        doc.Nodes.Add(new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 100,
            Y = 100,
            W = 120,
            H = 60
        });

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();

        var nw = cut.FindAll(".tm-diagram-connection-point")
            .FirstOrDefault(el => el.GetAttribute("data-cp-rx") == "0" && el.GetAttribute("data-cp-ry") == "0");
        nw.Should().NotBeNull();
        nw!.GetAttribute("data-cp-perimeter").Should().Be("true");

        var center = cut.FindAll(".tm-diagram-connection-point")
            .FirstOrDefault(el => el.GetAttribute("data-cp-rx") == "0.5" && el.GetAttribute("data-cp-ry") == "0.5");
        center.Should().NotBeNull();
        center!.GetAttribute("data-cp-perimeter").Should().Be("false");
    }

    [Fact]
    public async Task EdgeCreated_WithSourceConstraint_SetsSourceConstraint()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 100, Y = 100, W = 40, H = 40 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 100, W = 40, H = 40 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc)
            .Add(e => e.ReadOnly, false));

        var canvas = cut.FindComponent<TmDiagramCanvas>();
        await cut.InvokeAsync(async () => await canvas.Instance.JsOnEdgeCreated(
            n1.Id, null, n2.Id, null,
            null, 0.5, null, 0.5,
            null, 0.5,
            0.25, 0.0, true,
            0.75, 1.0, true));

        doc.Edges.Count.Should().Be(1);
        var edge = doc.Edges[0];
        edge.SourceNodeId.Should().Be(n1.Id);
        edge.TargetNodeId.Should().Be(n2.Id);
        edge.SourcePortId.Should().BeNull();
        edge.TargetPortId.Should().BeNull();
        edge.SourceConstraint.Should().NotBeNull();
        edge.SourceConstraint!.RelativeX.Should().Be(0.25);
        edge.SourceConstraint.RelativeY.Should().Be(0.0);
        edge.SourceConstraint.Perimeter.Should().BeTrue();
        edge.TargetConstraint.Should().NotBeNull();
        edge.TargetConstraint!.RelativeX.Should().Be(0.75);
        edge.TargetConstraint.RelativeY.Should().Be(1.0);
        edge.TargetConstraint.Perimeter.Should().BeTrue();
    }

    [Fact]
    public async Task EdgeCreated_WithConstraintAndPort_ConstraintWins()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 100, Y = 100, W = 40, H = 40 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 100, W = 40, H = 40 };
        n1.Ports.Add(new DiagramPort { Side = PortSide.Right, Offset = 0.5 });
        n2.Ports.Add(new DiagramPort { Side = PortSide.Left, Offset = 0.5 });
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc)
            .Add(e => e.ReadOnly, false));

        var canvas = cut.FindComponent<TmDiagramCanvas>();
        // Pass both a portId and a constraint — constraint should win
        await cut.InvokeAsync(async () => await canvas.Instance.JsOnEdgeCreated(
            n1.Id, n1.Ports[0].Id, n2.Id, n2.Ports[0].Id,
            null, 0.5, null, 0.5,
            null, 0.5,
            0.0, 0.5, true,
            1.0, 0.5, true));

        doc.Edges.Count.Should().Be(1);
        var edge = doc.Edges[0];
        edge.SourcePortId.Should().BeNull();
        edge.TargetPortId.Should().BeNull();
        edge.SourceConstraint.Should().NotBeNull();
        edge.TargetConstraint.Should().NotBeNull();
    }

    [Fact]
    public async Task EdgeCreated_WithoutConstraint_UsesPort()
    {
        var doc = new DiagramDocument();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 100, Y = 100, W = 40, H = 40 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 100, W = 40, H = 40 };
        n1.Ports.Add(new DiagramPort { Side = PortSide.Right, Offset = 0.5 });
        n2.Ports.Add(new DiagramPort { Side = PortSide.Left, Offset = 0.5 });
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc)
            .Add(e => e.ReadOnly, false));

        var canvas = cut.FindComponent<TmDiagramCanvas>();
        await cut.InvokeAsync(async () => await canvas.Instance.JsOnEdgeCreated(
            n1.Id, n1.Ports[0].Id, n2.Id, n2.Ports[0].Id,
            null, 0.5, null, 0.5,
            null, 0.5,
            null, null, null,
            null, null, null));

        doc.Edges.Count.Should().Be(1);
        var edge = doc.Edges[0];
        edge.SourcePortId.Should().Be(n1.Ports[0].Id);
        edge.TargetPortId.Should().Be(n2.Ports[0].Id);
        edge.SourceConstraint.Should().BeNull();
        edge.TargetConstraint.Should().BeNull();
    }
}
