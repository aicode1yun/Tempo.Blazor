using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class TmDiagramCanvasDanglingTests : LocalizationTestBase
{
    public TmDiagramCanvasDanglingTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public async Task SelectedDanglingEdge_RendersDanglingHandle()
    {
        var doc = new DiagramDocument();
        doc.Nodes.Add(new DiagramNode
        {
            Id = "n1",
            StencilId = "general.rectangle",
            X = 100, Y = 100, W = 120, H = 60
        });
        doc.Edges.Add(new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = null,
            SourcePoint = new DiagramPoint(10, 20),
            TargetNodeId = "n1",
        });

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        await cut.Instance.SetSelection("e1");
        cut.Render();

        var handle = cut.Find("rect.tm-diagram-edge-handle--dangling[data-dangling='source']");
        handle.Should().NotBeNull();
        handle.GetAttribute("data-edge-id").Should().Be("e1");
    }

    [Fact]
    public async Task SelectedEdgeWithDanglingTarget_RendersTargetDanglingHandle()
    {
        var doc = new DiagramDocument();
        doc.Nodes.Add(new DiagramNode
        {
            Id = "n1",
            StencilId = "general.rectangle",
            X = 100, Y = 100, W = 120, H = 60
        });
        doc.Edges.Add(new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = "n1",
            TargetNodeId = null,
            TargetPoint = new DiagramPoint(300, 200),
        });

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        await cut.Instance.SetSelection("e1");
        cut.Render();

        var handle = cut.Find("rect.tm-diagram-edge-handle--dangling[data-dangling='target']");
        handle.Should().NotBeNull();
        handle.GetAttribute("data-edge-id").Should().Be("e1");
    }

    [Fact]
    public void ConnectedEdge_DoesNotRenderDanglingHandles()
    {
        var doc = new DiagramDocument();
        doc.Nodes.Add(new DiagramNode
        {
            Id = "n1",
            StencilId = "general.rectangle",
            X = 100, Y = 100, W = 120, H = 60
        });
        doc.Nodes.Add(new DiagramNode
        {
            Id = "n2",
            StencilId = "general.rectangle",
            X = 300, Y = 100, W = 120, H = 60
        });
        doc.Edges.Add(new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = "n1",
            TargetNodeId = "n2",
        });

        var cut = Render<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false));

        cut.Render();

        cut.FindAll("rect.tm-diagram-edge-handle--dangling").Should().BeEmpty();
    }
}
