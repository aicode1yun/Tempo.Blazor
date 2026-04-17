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
        results[0].DataKey.Should().Be("label");
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
        results[0].DataKey.Should().Be("Label");
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

    [Fact]
    public void Search_Regex_MatchesPattern()
    {
        var doc = EmptyDoc();
        doc.Nodes.Add(new DiagramNode { Id = "n1", StencilId = "uml.class", Data = { ["label"] = "CustomerOrder" } });

        var results = DiagramSearchService.Search(doc, @"^Cust.*Order$", useRegex: true);

        results.Should().ContainSingle();
        results[0].NodeId.Should().Be("n1");
        results[0].MatchType.Should().Be(DiagramSearchMatchType.Label);
    }

    [Fact]
    public void Search_Regex_InvalidPattern_ReturnsEmptyResults()
    {
        var doc = EmptyDoc();
        doc.Nodes.Add(new DiagramNode { Id = "n1", StencilId = "uml.class", Data = { ["label"] = "Test" } });

        var results = DiagramSearchService.Search(doc, "[invalid", useRegex: true);

        results.Should().BeEmpty();
    }

    [Fact]
    public void TryCreateRegex_ValidPattern_ReturnsTrue()
    {
        var success = DiagramSearchService.TryCreateRegex(@"test\d+", out var regex, out var error);

        success.Should().BeTrue();
        regex.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void TryCreateRegex_InvalidPattern_ReturnsFalseWithError()
    {
        var success = DiagramSearchService.TryCreateRegex("[unclosed", out var regex, out var error);

        success.Should().BeFalse();
        regex.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ReplaceInResult_NodeLabel_ReplacesText()
    {
        var doc = EmptyDoc();
        doc.Nodes.Add(new DiagramNode { Id = "n1", StencilId = "uml.class", Data = { ["label"] = "OldCustomer" } });
        var result = new DiagramSearchResult { NodeId = "n1", MatchType = DiagramSearchMatchType.Label, DataKey = "label", MatchedText = "OldCustomer" };
        var regex = new System.Text.RegularExpressions.Regex("Old");

        var replaceResult = DiagramSearchService.ReplaceInResult(doc, result, regex, "New");

        replaceResult.Should().NotBeNull();
        replaceResult!.OldValue.Should().Be("OldCustomer");
        replaceResult.NewValue.Should().Be("NewCustomer");
        replaceResult.DataKey.Should().Be("label");
    }

    [Fact]
    public void ReplaceInResult_EdgeLabel_ReplacesText()
    {
        var doc = EmptyDoc();
        doc.Edges.Add(new DiagramEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "n2", Label = "old label" });
        var result = new DiagramSearchResult { EdgeId = "e1", MatchType = DiagramSearchMatchType.Label, DataKey = "Label", MatchedText = "old label" };
        var regex = new System.Text.RegularExpressions.Regex("old");

        var replaceResult = DiagramSearchService.ReplaceInResult(doc, result, regex, "new");

        replaceResult.Should().NotBeNull();
        replaceResult!.OldValue.Should().Be("old label");
        replaceResult.NewValue.Should().Be("new label");
    }

    [Fact]
    public void ReplaceInResult_IdMatch_IsNotReplaceable()
    {
        var doc = EmptyDoc();
        doc.Nodes.Add(new DiagramNode { Id = "n1", StencilId = "uml.class" });
        var result = new DiagramSearchResult { NodeId = "n1", MatchType = DiagramSearchMatchType.Id, MatchedText = "n1" };
        var regex = new System.Text.RegularExpressions.Regex("n1");

        var replaceResult = DiagramSearchService.ReplaceInResult(doc, result, regex, "n2");

        replaceResult.Should().BeNull();
    }

    [Fact]
    public void ReplaceInResult_StencilIdMatch_IsNotReplaceable()
    {
        var doc = EmptyDoc();
        doc.Nodes.Add(new DiagramNode { Id = "n1", StencilId = "uml.class" });
        var result = new DiagramSearchResult { NodeId = "n1", MatchType = DiagramSearchMatchType.StencilId, MatchedText = "uml.class" };
        var regex = new System.Text.RegularExpressions.Regex("class");

        var replaceResult = DiagramSearchService.ReplaceInResult(doc, result, regex, "interface");

        replaceResult.Should().BeNull();
    }

    [Fact]
    public void SearchAllPages_Regex_MatchesAcrossPages()
    {
        var doc = new DiagramDocument { Title = "Test" };
        doc.Pages.Add(new DiagramPage { Name = "Page 1", Width = 3000, Height = 2000, Nodes = [new DiagramNode { Id = "n1", StencilId = "uml.class", Data = { ["label"] = "Alpha" } }], Edges = [] });
        doc.Pages.Add(new DiagramPage { Name = "Page 2", Width = 3000, Height = 2000, Nodes = [new DiagramNode { Id = "n2", StencilId = "uml.class", Data = { ["label"] = "Beta" } }], Edges = [] });
        doc.ActivePageIndex = 0;

        var results = DiagramSearchService.SearchAllPages(doc, @"^(Alpha|Beta)$", useRegex: true);

        results.Should().HaveCount(2);
        results[0].PageIndex.Should().Be(0);
        results[1].PageIndex.Should().Be(1);
    }
}
