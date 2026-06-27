using System.Text.Json;
using Tempo.Blazor.Mcp.Diagram;
using Tempo.Blazor.Mcp.Tests.Fixtures;

namespace Tempo.Blazor.Mcp.Tests;

public class DiagramDocumentToolsTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task ListDocuments_ReturnsStoredDiagramDocuments()
    {
        var backend = new FakeDiagramBackend();
        backend.Add("Checkout flow", "/Diagrams");
        backend.Add("Billing ERD", "/Diagrams");

        var root = Parse(await DiagramDocumentTools.ListDocuments(backend, folderPath: "/Diagrams"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("totalCount").GetInt32().Should().Be(2);
        root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString())
            .Should().Contain(["Checkout flow", "Billing ERD"]);
    }

    [Fact]
    public async Task GetDocument_ReturnsModifiedAtAndDocumentJson()
    {
        var backend = new FakeDiagramBackend();
        var id = backend.Add("Checkout flow", "/Diagrams");

        var root = Parse(await DiagramDocumentTools.GetDocument(backend, backend, id));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("id").GetGuid().Should().Be(id);
        root.TryGetProperty("modifiedAt", out _).Should().BeTrue();
        root.GetProperty("document").GetProperty("title").GetString().Should().Be("Checkout flow");
    }

    [Fact]
    public async Task GetDocument_Unknown_ReturnsNotFound()
    {
        var backend = new FakeDiagramBackend();

        var root = Parse(await DiagramDocumentTools.GetDocument(backend, backend, Guid.NewGuid()));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task CreateDocument_ReturnsIdAndPersists()
    {
        var backend = new FakeDiagramBackend();

        var root = Parse(await DiagramDocumentTools.CreateDocument(backend, backend, "New diagram"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var id = root.GetProperty("id").GetGuid();
        (await backend.GetDiagramDocumentAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateDocument_ForwardsScopeAppId_ToProvider()
    {
        var backend = new FakeDiagramBackend();
        var appId = Guid.NewGuid().ToString("D");

        var root = Parse(await DiagramDocumentTools.CreateDocument(backend, backend, "Scoped diagram", scopeAppId: appId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        backend.LastScopeAppId.Should().Be(appId);
    }

    [Fact]
    public async Task ListDocuments_ForwardsScopeAppId_ViaQuery()
    {
        var backend = new FakeDiagramBackend();
        var appId = Guid.NewGuid().ToString("D");

        var root = Parse(await DiagramDocumentTools.ListDocuments(backend, scopeAppId: appId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        backend.LastScopeAppId.Should().Be(appId);
    }
}
