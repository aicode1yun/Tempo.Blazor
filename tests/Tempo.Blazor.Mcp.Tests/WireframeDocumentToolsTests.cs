using System.Text.Json;
using Tempo.Blazor.Mcp;
using Tempo.Blazor.Mcp.Tests.Fixtures;
using Tempo.Blazor.Mcp.Wireframe;

namespace Tempo.Blazor.Mcp.Tests;

/// <summary>Tests for list/get/create wireframe document tools.</summary>
public class WireframeDocumentToolsTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task ListDocuments_ReturnsStoredDocuments()
    {
        var backend = new FakeWireframeBackend();
        backend.Add("Home", "/Designs");
        backend.Add("Checkout", "/Designs");

        var root = Parse(await WireframeDocumentTools.ListDocuments(backend, folderPath: "/Designs"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("totalCount").GetInt32().Should().Be(2);
        root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString())
            .Should().Contain(["Home", "Checkout"]);
    }

    [Fact]
    public async Task GetDocument_ReturnsModifiedAtAndDocumentJson()
    {
        var backend = new FakeWireframeBackend();
        var id = backend.Add("Home", "/Designs");

        var root = Parse(await WireframeDocumentTools.GetDocument(backend, backend, id));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("id").GetGuid().Should().Be(id);
        root.TryGetProperty("modifiedAt", out _).Should().BeTrue();
        root.GetProperty("document").GetProperty("title").GetString().Should().Be("Home");
    }

    [Fact]
    public async Task GetDocument_Unknown_ReturnsNotFound()
    {
        var backend = new FakeWireframeBackend();

        var root = Parse(await WireframeDocumentTools.GetDocument(backend, backend, Guid.NewGuid()));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task CreateDocument_ReturnsIdAndModifiedAt_AndPersists()
    {
        var backend = new FakeWireframeBackend();

        var root = Parse(await WireframeDocumentTools.CreateDocument(backend, backend, "New design"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var id = root.GetProperty("id").GetGuid();
        (await backend.GetWireframeDocumentAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateDocument_ForwardsScopeAppId_ToProvider()
    {
        var backend = new FakeWireframeBackend();
        var appId = Guid.NewGuid().ToString("D");

        var root = Parse(await WireframeDocumentTools.CreateDocument(backend, backend, "Scoped design", scopeAppId: appId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        backend.LastCreateScopeAppId.Should().Be(appId);
    }

    [Fact]
    public async Task ListDocuments_ForwardsScopeAppId_ViaQuery()
    {
        var backend = new FakeWireframeBackend();
        var appId = Guid.NewGuid().ToString("D");

        var root = Parse(await WireframeDocumentTools.ListDocuments(backend, scopeAppId: appId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        backend.LastBrowseScopeAppId.Should().Be(appId);
    }
}
