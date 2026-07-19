using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class VirtualBendTransactionTests : LocalizationTestBase
{
    public VirtualBendTransactionTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public async Task VirtualBendInsert_CreatesTransaction()
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
            Routing = "orthogonal",
            Waypoints = [new DiagramPoint(100, 25), new DiagramPoint(200, 25)]
        };
        doc.Edges.Add(edge);

        var stack = new DiagramCommandStack();

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false)
            .Add(c => c.CommandStack, stack));

        await cut.Instance.OnVirtualBendInsert(edge.Id, 0, 150, 25);

        stack.IsInTransaction.Should().BeTrue();
    }

    [Fact]
    public async Task VirtualBendInsert_ThenWaypointMove_CommitsTransaction()
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
            Routing = "orthogonal",
            Waypoints = [new DiagramPoint(100, 25), new DiagramPoint(200, 25)]
        };
        doc.Edges.Add(edge);

        var stack = new DiagramCommandStack();

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false)
            .Add(c => c.CommandStack, stack));

        var idx = await cut.Instance.OnVirtualBendInsert(edge.Id, 0, 150, 25);
        stack.IsInTransaction.Should().BeTrue();
        edge.Waypoints.Count.Should().Be(3);

        await cut.Instance.OnEdgeWaypointMoved(edge.Id, idx, 160, 30);

        stack.IsInTransaction.Should().BeFalse();
        stack.CanUndo.Should().BeTrue();
        stack.NextUndoName.Should().Be("Virtual bend");
    }

    [Fact]
    public async Task VirtualBendInsert_ThenCancel_RollbacksTransaction()
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
            Routing = "orthogonal",
            Waypoints = [new DiagramPoint(100, 25), new DiagramPoint(200, 25)]
        };
        doc.Edges.Add(edge);

        var stack = new DiagramCommandStack();

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false)
            .Add(c => c.CommandStack, stack));

        await cut.Instance.OnVirtualBendInsert(edge.Id, 0, 150, 25);
        stack.IsInTransaction.Should().BeTrue();
        edge.Waypoints.Count.Should().Be(3);

        await cut.Instance.OnCancelEdgeEdit();

        stack.IsInTransaction.Should().BeFalse();
        edge.Waypoints.Count.Should().Be(2);
        stack.CanUndo.Should().BeFalse();
    }

    [Fact]
    public async Task NormalWaypointMove_DoesNotUseTransaction()
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
            Waypoints = [new DiagramPoint(120, 25)]
        };
        doc.Edges.Add(edge);

        var stack = new DiagramCommandStack();

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false)
            .Add(c => c.CommandStack, stack));

        await cut.Instance.OnEdgeWaypointMoved(edge.Id, 0, 130, 25);

        stack.IsInTransaction.Should().BeFalse();
        stack.CanUndo.Should().BeTrue();
    }
}
