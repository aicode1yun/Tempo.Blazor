using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Mcp.Diagram;
using Tempo.Blazor.Mcp.Tests.Fixtures;

namespace Tempo.Blazor.Mcp.Tests;

public class DiagramOperationToolsTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;
    private static IEnumerable<FakeDiagramStencilProvider> Stencils() => [new FakeDiagramStencilProvider()];

    [Fact]
    public void Engine_AddNode_AddsAndReportsCreatedId()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();

        var result = DiagramOperationEngine.Apply(
            doc,
            "[{\"op\":\"addNode\",\"stencilId\":\"test.process\",\"x\":10,\"y\":20,\"w\":160,\"h\":80}]");

        result.Success.Should().BeTrue();
        result.Applied.Should().Be(1);
        result.CreatedIds.Should().ContainSingle();
        doc.Nodes.Should().ContainSingle().Which.StencilId.Should().Be("test.process");
    }

    [Fact]
    public void Engine_UnknownOp_FailsWithIndex()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();

        var result = DiagramOperationEngine.Apply(doc, "[{\"op\":\"frobnicate\"}]");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("operations[0]").And.Contain("frobnicate");
    }

    [Fact]
    public void Engine_AddEdge_ConnectsExistingNodes()
    {
        var doc = new DiagramDocument();
        doc.EnsurePages();
        doc.Nodes.Add(new DiagramNode { Id = "a", StencilId = "test.process" });
        doc.Nodes.Add(new DiagramNode { Id = "b", StencilId = "test.database" });

        var result = DiagramOperationEngine.Apply(
            doc,
            "[{\"op\":\"addEdge\",\"sourceNodeId\":\"a\",\"targetNodeId\":\"b\",\"label\":\"writes\"}]");

        result.Success.Should().BeTrue();
        doc.Edges.Should().ContainSingle().Which.Label.Should().Be("writes");
    }

    [Fact]
    public async Task ApplyOperations_HappyPath_PersistsAndReportsApplied()
    {
        var backend = new FakeDiagramBackend();
        var id = backend.Add("Flow", "/Diagrams");
        var ops = "[{\"op\":\"addNode\",\"stencilId\":\"test.process\",\"x\":10,\"y\":20,\"w\":160,\"h\":80}]";

        var root = Parse(await DiagramOperationTools.ApplyOperations(backend, backend, Stencils(), id, ops));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("applied").GetInt32().Should().Be(1);
        (await backend.GetDiagramDocumentAsync(id))!.Nodes.Should().ContainSingle();
    }

    [Fact]
    public async Task ApplyOperations_InvalidResult_SavesNothing()
    {
        var backend = new FakeDiagramBackend();
        var id = backend.Add("Flow", "/Diagrams");
        var ops = "[{\"op\":\"addNode\",\"stencilId\":\"missing.stencil\",\"w\":160,\"h\":80}]";

        var root = Parse(await DiagramOperationTools.ApplyOperations(backend, backend, Stencils(), id, ops));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
        (await backend.GetDiagramDocumentAsync(id))!.Nodes.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyOperations_StaleExpectedModifiedAt_ReturnsConflict()
    {
        var backend = new FakeDiagramBackend();
        var id = backend.Add("Flow", "/Diagrams");
        var ops = "[{\"op\":\"addNode\",\"stencilId\":\"test.process\",\"w\":160,\"h\":80}]";

        var root = Parse(await DiagramOperationTools.ApplyOperations(
            backend,
            backend,
            Stencils(),
            id,
            ops,
            expectedModifiedAt: DateTime.UtcNow.AddMinutes(-10)));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("conflict");
    }

    [Fact]
    public async Task ReplaceDocument_ValidDocument_Persists()
    {
        var backend = new FakeDiagramBackend();
        var id = backend.Add("Flow", "/Diagrams");

        var doc = new DiagramDocument { Title = "Replaced" };
        doc.EnsurePages();
        doc.Nodes.Add(new DiagramNode { StencilId = "test.process", W = 160, H = 80 });
        var json = DiagramSerializer.Serialize(doc);

        var root = Parse(await DiagramOperationTools.ReplaceDocument(backend, backend, Stencils(), id, json));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        (await backend.GetDiagramDocumentAsync(id))!.Title.Should().Be("Replaced");
    }

    [Fact]
    public async Task ReplaceDocument_InvalidDocument_SavesNothing()
    {
        var backend = new FakeDiagramBackend();
        var id = backend.Add("Flow", "/Diagrams");

        var doc = new DiagramDocument { Title = "Bad" };
        doc.EnsurePages();
        doc.Nodes.Add(new DiagramNode { StencilId = "missing.stencil", W = 160, H = 80 });
        var json = DiagramSerializer.Serialize(doc);

        var root = Parse(await DiagramOperationTools.ReplaceDocument(backend, backend, Stencils(), id, json));

        root.GetProperty("error").GetString().Should().Be("validation_failed");
        (await backend.GetDiagramDocumentAsync(id))!.Title.Should().Be("Flow");
    }
}
