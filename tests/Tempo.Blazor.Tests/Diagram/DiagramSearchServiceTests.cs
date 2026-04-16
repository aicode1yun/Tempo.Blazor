using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class DiagramSearchServiceTests
{
    private static DiagramDocument EmptyDoc() => new()
    {
        Title = "Test", Width = 3000, Height = 2000, Nodes = [], Edges = []
    };

    [Fact]
    public void Search_ByStencilId_ReturnsNodeResult()
    {
        var doc = EmptyDoc();
        var node = new DiagramNode
        {
            Id = "n1",
            StencilId = "uml.package",
            Data = { ["label"] = "My Package" }
        };
        doc.Nodes.Add(node);

        var results = DiagramSearchService.Search(doc, "uml.package");

        results.Should().ContainSingle();
        results[0].NodeId.Should().Be("n1");
        results[0].MatchType.Should().Be(DiagramSearchMatchType.StencilId);
    }

    [Fact]
    public void Search_ByNodeDataLabel_ReturnsLabelResult()
    {
        var doc = EmptyDoc();
        var node = new DiagramNode
        {
            Id = "n2",
            StencilId = "general.rectangle",
            Data = { ["label"] = "Customer" }
        };
        doc.Nodes.Add(node);

        var results = DiagramSearchService.Search(doc, "cust");

        results.Should().ContainSingle();
        results[0].NodeId.Should().Be("n2");
        results[0].MatchType.Should().Be(DiagramSearchMatchType.Label);
    }

    [Fact]
    public void Search_ByEdgeLabel_ReturnsEdgeResult()
    {
        var doc = EmptyDoc();
        var edge = new DiagramEdge
        {
            Id = "e1",
            SourceNodeId = "n1",
            TargetNodeId = "n2",
            Label = "depends on"
        };
        doc.Edges.Add(edge);

        var results = DiagramSearchService.Search(doc, "depends");

        results.Should().ContainSingle();
        results[0].EdgeId.Should().Be("e1");
        results[0].MatchType.Should().Be(DiagramSearchMatchType.Label);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsNoResults()
    {
        var doc = EmptyDoc();
        doc.Nodes.Add(new DiagramNode { Id = "n1", StencilId = "uml.class" });

        var results = DiagramSearchService.Search(doc, "   ");

        results.Should().BeEmpty();
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        var doc = EmptyDoc();
        doc.Nodes.Add(new DiagramNode { Id = "n1", StencilId = "UML.Class", Data = { ["label"] = "HELLO" } });

        var results = DiagramSearchService.Search(doc, "hello");

        results.Should().ContainSingle();
        results[0].NodeId.Should().Be("n1");
    }
}
