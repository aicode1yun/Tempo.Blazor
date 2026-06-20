using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Mcp.Diagram;
using Tempo.Blazor.Mcp.Tests.Fixtures;

namespace Tempo.Blazor.Mcp.Tests;

public class DiagramImplementationBriefTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task GetImplementationBrief_ReturnsPagesNodesEdgesAndStencilUsage()
    {
        var backend = new FakeDiagramBackend();
        var doc = new DiagramDocument { Title = "Flow" };
        doc.EnsurePages();
        doc.Nodes.Add(new DiagramNode
        {
            Id = "a",
            StencilId = "test.process",
            Data = new Dictionary<string, object> { ["name"] = "Start" }
        });
        doc.Nodes.Add(new DiagramNode { Id = "b", StencilId = "test.database" });
        doc.Edges.Add(new DiagramEdge { SourceNodeId = "a", TargetNodeId = "b", Label = "writes" });
        var id = backend.Add("Flow", "/Diagrams", doc);

        var root = Parse(await DiagramBriefTools.GetImplementationBrief(backend, backend, id));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var brief = root.GetProperty("brief");
        brief.GetProperty("title").GetString().Should().Be("Flow");
        brief.GetProperty("pages")[0].GetProperty("nodes").GetArrayLength().Should().Be(2);
        brief.GetProperty("pages")[0].GetProperty("edges").GetArrayLength().Should().Be(1);
        brief.GetProperty("stencilsUsed").EnumerateArray()
            .Select(i => i.GetProperty("stencilId").GetString())
            .Should().Contain("test.process");
    }
}
