using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class DiagramDocumentMultiPageTests
{
    [Fact]
    public void ActivePage_ReturnsFirstPageByDefault()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();

        doc.ActivePage.Should().NotBeNull();
        doc.ActivePage.Name.Should().Be("Page 1");
        doc.ActivePageIndex.Should().Be(0);
    }

    [Fact]
    public void Nodes_ProxyToActivePage()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();
        var node = new DiagramNode { Id = "n1", StencilId = "rect" };

        doc.Nodes.Add(node);

        doc.ActivePage.Nodes.Should().ContainSingle();
        doc.Nodes[0].Id.Should().Be("n1");
    }

    [Fact]
    public void Width_ProxyToActivePage()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();

        doc.Width = 4000;

        doc.ActivePage.Width.Should().Be(4000);
    }

    [Fact]
    public void Serialize_MultiPageDocument_PreservesPages()
    {
        var doc = new DiagramDocument { Title = "Multi" };
        doc.EnsurePages();
        doc.Pages[0].Name = "Page A";
        doc.Pages[0].Nodes.Add(new DiagramNode { Id = "n1", StencilId = "rect" });

        var page2 = new DiagramPage { Name = "Page B", Width = 5000, Height = 3000 };
        page2.Edges.Add(new DiagramEdge { Id = "e1", SourceNodeId = "a", TargetNodeId = "b" });
        doc.Pages.Add(page2);
        doc.ActivePageIndex = 1;

        var json = DiagramSerializer.Serialize(doc);
        var restored = DiagramSerializer.Deserialize(json);

        restored.Pages.Should().HaveCount(2);
        restored.ActivePageIndex.Should().Be(1);
        restored.ActivePage.Name.Should().Be("Page B");
        restored.ActivePage.Width.Should().Be(5000);
        restored.ActivePage.Edges.Should().HaveCount(1);
        restored.Pages[0].Nodes.Should().HaveCount(1);
    }

    [Fact]
    public void Deserialize_LegacyV1_MigratesToSinglePage()
    {
        var json = """
            {
              "version": "1.0",
              "title": "Legacy",
              "width": 1500,
              "height": 1000,
              "nodes": [{"id":"n1","stencilId":"rect","x":0,"y":0,"w":100,"h":100}],
              "edges": []
            }
            """;

        var doc = DiagramSerializer.Deserialize(json);

        doc.Version.Should().Be("2.0");
        doc.Pages.Should().HaveCount(1);
        doc.ActivePage.Width.Should().Be(1500);
        doc.ActivePage.Nodes.Should().HaveCount(1);
    }
}
