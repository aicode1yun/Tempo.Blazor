using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class OutlineConnectTests : LocalizationTestBase
{
    public OutlineConnectTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void OnEdgeTerminalOutlineConnected_SetsConstraint()
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
            TargetPortId = n2.Ports.FirstOrDefault()?.Id
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Instance.OnEdgeTerminalOutlineConnected(edge.Id, "target", n2.Id, 0.25, 0.0).Wait();

        edge.TargetNodeId.Should().Be(n2.Id);
        edge.TargetPortId.Should().BeNull();
        edge.TargetConstraint.Should().NotBeNull();
        edge.TargetConstraint!.RelativeX.Should().Be(0.25);
        edge.TargetConstraint.RelativeY.Should().Be(0.0);
        edge.TargetConstraint.Perimeter.Should().BeFalse();
    }

    [Fact]
    public void OnEdgeTerminalOutlineConnected_WithCommandStack_IsUndoable()
    {
        var doc = new DiagramDocument();
        var stack = new DiagramCommandStack();
        var n1 = new DiagramNode { StencilId = "general.rectangle", X = 0, Y = 0, W = 100, H = 50 };
        var n2 = new DiagramNode { StencilId = "general.rectangle", X = 200, Y = 0, W = 100, H = 50 };
        doc.Nodes.Add(n1);
        doc.Nodes.Add(n2);

        var edge = new DiagramEdge
        {
            SourceNodeId = n1.Id,
            TargetNodeId = n2.Id,
            TargetPortId = n2.Ports.FirstOrDefault()?.Id
        };
        doc.Edges.Add(edge);

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.CommandStack, stack)
            .Add(c => c.ReadOnly, false));

        var oldPortId = edge.TargetPortId;

        cut.Instance.OnEdgeTerminalOutlineConnected(edge.Id, "target", n2.Id, 0.75, 1.0).Wait();

        edge.TargetPortId.Should().BeNull();
        edge.TargetConstraint.Should().NotBeNull();
        edge.TargetConstraint!.RelativeX.Should().Be(0.75);

        // Undo
        stack.Undo();

        edge.TargetPortId.Should().Be(oldPortId);
        edge.TargetConstraint.Should().BeNull();
    }
}
