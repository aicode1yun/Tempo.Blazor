using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Mcp.Diagram;
using Tempo.Blazor.Mcp.Tests.Fixtures;

namespace Tempo.Blazor.Mcp.Tests;

public class DiagramValidationAndCatalogTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;
    private static IEnumerable<FakeDiagramStencilProvider> Stencils() => [new FakeDiagramStencilProvider()];

    [Fact]
    public void ValidateDocument_ValidDiagram_ReturnsValidTrue()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();
        doc.Nodes.Add(new DiagramNode { Id = "a", StencilId = "test.process", W = 160, H = 80 });

        var root = Parse(DiagramValidationTools.ValidateDocument(Stencils(), DiagramSerializer.Serialize(doc)));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("valid").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void ValidateDocument_DuplicateNodeId_ReturnsValidationError()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();
        doc.Nodes.Add(new DiagramNode { Id = "dup", StencilId = "test.process", W = 160, H = 80 });
        doc.Nodes.Add(new DiagramNode { Id = "dup", StencilId = "test.database", W = 160, H = 80 });

        var root = Parse(DiagramValidationTools.ValidateDocument(Stencils(), DiagramSerializer.Serialize(doc)));

        root.GetProperty("valid").GetBoolean().Should().BeFalse();
        root.GetProperty("validationErrors").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain(e => e!.Contains("duplicate node id"));
    }

    [Fact]
    public void ValidateDocument_EdgeReferencesMissingNode_ReturnsValidationError()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();
        doc.Nodes.Add(new DiagramNode { Id = "a", StencilId = "test.process", W = 160, H = 80 });
        doc.Edges.Add(new DiagramEdge { SourceNodeId = "a", TargetNodeId = "missing" });

        var root = Parse(DiagramValidationTools.ValidateDocument(Stencils(), DiagramSerializer.Serialize(doc)));

        root.GetProperty("valid").GetBoolean().Should().BeFalse();
        root.GetProperty("validationErrors").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain(e => e!.Contains("targetNodeId"));
    }

    [Fact]
    public void ListStencils_Compact_ReturnsAvailableStencils()
    {
        var root = Parse(DiagramStencilCatalogTools.ListStencils(Stencils(), compact: true));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("totalCount").GetInt32().Should().Be(2);
        var first = root.GetProperty("items").EnumerateArray().First();
        first.TryGetProperty("id", out _).Should().BeTrue();
        first.TryGetProperty("layout", out _).Should().BeFalse();
    }

    [Fact]
    public void ListStencils_Full_IncludesLayout()
    {
        var root = Parse(DiagramStencilCatalogTools.ListStencils(Stencils(), compact: false));
        var hasLayout = root.GetProperty("items").EnumerateArray()
            .Any(i => i.TryGetProperty("layout", out _));

        hasLayout.Should().BeTrue();
    }

    [Fact]
    public void GetStencil_KnownId_ReturnsFullContract()
    {
        var root = Parse(DiagramStencilCatalogTools.GetStencil(Stencils(), "test.process"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("stencil").GetProperty("id").GetString().Should().Be("test.process");
    }

    [Fact]
    public void GetStencil_UnknownId_ReturnsNotFoundWithSuggestion()
    {
        var root = Parse(DiagramStencilCatalogTools.GetStencil(Stencils(), "test.proces"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
        root.GetProperty("message").GetString().Should().Contain("test.process");
    }
}
