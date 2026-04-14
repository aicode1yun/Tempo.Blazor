using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class TmDiagramEditorConnectTests : LocalizationTestBase
{
    public TmDiagramEditorConnectTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public async Task ConnectArrowClick_OpensStencilModal()
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

        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc)
            .Add(e => e.ReadOnly, false));

        // Select node so arrows appear
        var canvas = cut.FindComponent<TmDiagramCanvas>();
        await canvas.Instance.SetSelection(doc.Nodes[0].Id);
        cut.Render();

        // Click east arrow
        cut.Find(".tm-diagram-connect-arrow--e").Click();
        cut.Render();

        // Modal should be visible
        cut.Find(".tm-modal").Should().NotBeNull();
        cut.Find(".tm-modal-title").TextContent.Should().Contain("Select shape to connect");
    }

    [Fact]
    public async Task ModalStencilSelection_AddsNodeAndEdge()
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

        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc)
            .Add(e => e.ReadOnly, false));

        var canvas = cut.FindComponent<TmDiagramCanvas>();
        await canvas.Instance.SetSelection(doc.Nodes[0].Id);
        cut.Render();

        cut.Find(".tm-diagram-connect-arrow--e").Click();
        cut.Render();

        // Click first stencil in the modal grid (general.rectangle)
        var firstStencil = cut.Find(".tm-diagram-connect-modal__item");
        firstStencil.Click();
        cut.Render();

        // Document should now have 2 nodes and 1 edge
        doc.Nodes.Count.Should().Be(2);
        doc.Edges.Count.Should().Be(1);

        var edge = doc.Edges[0];
        edge.SourceNodeId.Should().Be(doc.Nodes[0].Id);
        edge.TargetNodeId.Should().Be(doc.Nodes[1].Id);
    }
}
