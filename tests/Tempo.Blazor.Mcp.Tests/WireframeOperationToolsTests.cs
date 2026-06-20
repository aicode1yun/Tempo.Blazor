using System.Text.Json;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Mcp;
using Tempo.Blazor.Mcp.Tests.Fixtures;
using Tempo.Blazor.Mcp.Wireframe;

namespace Tempo.Blazor.Mcp.Tests;

/// <summary>Tests for the operation engine and the apply_operations / replace_document tools.</summary>
public class WireframeOperationToolsTests
{
    private static WireframeSchemaRegistry Registry() => new([new BuiltInComponentSchemas()]);
    private static string KnownType() => Registry().GetAll().First().Type;
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    // ── Engine ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Engine_AddElement_AddsAndReportsCreatedId()
    {
        var doc = new WireframeDocument(); doc.EnsureActivePage();
        var ops = $"[{{\"op\":\"addElement\",\"type\":\"{KnownType()}\",\"x\":10,\"y\":10,\"w\":100,\"h\":40}}]";

        var result = WireframeOperationEngine.Apply(doc, ops);

        result.Success.Should().BeTrue();
        result.Applied.Should().Be(1);
        result.CreatedIds.Should().ContainSingle();
        doc.Elements.Should().ContainSingle().Which.Type.Should().Be(KnownType());
    }

    [Fact]
    public void Engine_UnknownOp_FailsWithIndex()
    {
        var doc = new WireframeDocument(); doc.EnsureActivePage();

        var result = WireframeOperationEngine.Apply(doc, "[{\"op\":\"frobnicate\"}]");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("operations[0]").And.Contain("frobnicate");
    }

    [Fact]
    public void Engine_UpdateMissingElement_Fails()
    {
        var doc = new WireframeDocument(); doc.EnsureActivePage();

        var result = WireframeOperationEngine.Apply(doc, "[{\"op\":\"updateElement\",\"id\":\"nope\",\"x\":5}]");

        result.Success.Should().BeFalse();
        result.Errors[0].Should().Contain("not found");
    }

    [Fact]
    public void Engine_SetTitleAndCanvasSize_Apply()
    {
        var doc = new WireframeDocument(); doc.EnsureActivePage();

        var result = WireframeOperationEngine.Apply(doc,
            "[{\"op\":\"setTitle\",\"title\":\"Orders\"},{\"op\":\"setCanvasSize\",\"width\":1440,\"height\":1024}]");

        result.Success.Should().BeTrue();
        doc.Title.Should().Be("Orders");
        doc.Width.Should().Be(1440);
    }

    [Fact]
    public void Engine_InvalidJson_Fails()
    {
        var doc = new WireframeDocument(); doc.EnsureActivePage();

        WireframeOperationEngine.Apply(doc, "{not an array}").Success.Should().BeFalse();
    }

    // ── apply_operations tool ─────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyOperations_HappyPath_PersistsAndReportsApplied()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Design", "/");
        var ops = $"[{{\"op\":\"addElement\",\"type\":\"{KnownType()}\",\"x\":10,\"y\":10,\"w\":100,\"h\":40}}]";

        var root = Parse(await WireframeOperationTools.ApplyOperations(backend, backend, Registry(), id, ops));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("applied").GetInt32().Should().Be(1);
        (await backend.GetWireframeDocumentAsync(id))!.Elements.Should().ContainSingle();
    }

    [Fact]
    public async Task ApplyOperations_InvalidResult_SavesNothing()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Design", "/");
        var ops = "[{\"op\":\"addElement\",\"type\":\"TotallyUnknownType\",\"w\":100,\"h\":40}]";

        var root = Parse(await WireframeOperationTools.ApplyOperations(backend, backend, Registry(), id, ops));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
        (await backend.GetWireframeDocumentAsync(id))!.Elements.Should().BeEmpty(); // unchanged
    }

    [Fact]
    public async Task ApplyOperations_StaleExpectedModifiedAt_ReturnsConflict()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Design", "/");
        var ops = $"[{{\"op\":\"addElement\",\"type\":\"{KnownType()}\",\"w\":100,\"h\":40}}]";

        var root = Parse(await WireframeOperationTools.ApplyOperations(
            backend, backend, Registry(), id, ops, expectedModifiedAt: DateTime.UtcNow.AddMinutes(-10)));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("conflict");
    }

    [Fact]
    public async Task ApplyOperations_UnknownDocument_ReturnsNotFound()
    {
        var backend = new FakeWireframeBackend();

        var root = Parse(await WireframeOperationTools.ApplyOperations(
            backend, backend, Registry(), Guid.NewGuid(), "[]"));

        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    // ── replace_document tool ──────────────────────────────────────────────────────

    [Fact]
    public async Task ReplaceDocument_ValidDocument_Persists()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Design", "/");

        var doc = new WireframeDocument { Title = "Replaced" };
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Type = KnownType(), W = 120, H = 40 });
        var json = WireframeSerializer.Serialize(doc);

        var root = Parse(await WireframeOperationTools.ReplaceDocument(backend, backend, Registry(), id, json));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        (await backend.GetWireframeDocumentAsync(id))!.Title.Should().Be("Replaced");
    }

    [Fact]
    public async Task ReplaceDocument_InvalidDocument_SavesNothing()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Design", "/");

        var doc = new WireframeDocument { Title = "Bad" };
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Type = "NopeType", W = 120, H = 40 });
        var json = WireframeSerializer.Serialize(doc);

        var root = Parse(await WireframeOperationTools.ReplaceDocument(backend, backend, Registry(), id, json));

        root.GetProperty("error").GetString().Should().Be("validation_failed");
        (await backend.GetWireframeDocumentAsync(id))!.Title.Should().Be("Design"); // unchanged
    }
}
