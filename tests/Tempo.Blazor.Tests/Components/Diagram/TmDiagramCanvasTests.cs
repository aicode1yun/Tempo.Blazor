using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class TmDiagramCanvasTests : LocalizationTestBase
{
    public TmDiagramCanvasTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public async Task SelectedNode_RendersRotateHandle()
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

        await cut.Instance.SetSelection(doc.Nodes[0].Id);
        cut.Render();

        cut.Find(".tm-diagram-rotate-handle").Should().NotBeNull();
    }

    [Fact]
    public async Task SelectedNode_RendersConnectArrows()
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

        await cut.Instance.SetSelection(doc.Nodes[0].Id);
        cut.Render();

        cut.FindAll(".tm-diagram-connect-arrow").Count.Should().Be(4);
        cut.Find(".tm-diagram-connect-arrow--n").Should().NotBeNull();
        cut.Find(".tm-diagram-connect-arrow--e").Should().NotBeNull();
        cut.Find(".tm-diagram-connect-arrow--s").Should().NotBeNull();
        cut.Find(".tm-diagram-connect-arrow--w").Should().NotBeNull();
    }

    [Fact]
    public async Task ConnectArrowClick_RaisesEventCallback()
    {
        var doc = new DiagramDocument();
        var node = new DiagramNode
        {
            StencilId = "general.rectangle",
            X = 100,
            Y = 100,
            W = 120,
            H = 60
        };
        doc.Nodes.Add(node);

        (string NodeId, string Direction)? captured = null;

        var cut = RenderComponent<TmDiagramCanvas>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.ReadOnly, false)
            .Add(c => c.OnConnectArrowClicked, async args => { captured = args; }));

        await cut.Instance.SetSelection(node.Id);
        cut.Render();

        cut.Find(".tm-diagram-connect-arrow--e").Click();

        captured.Should().NotBeNull();
        captured.Value.NodeId.Should().Be(node.Id);
        captured.Value.Direction.Should().Be("e");
    }
}
