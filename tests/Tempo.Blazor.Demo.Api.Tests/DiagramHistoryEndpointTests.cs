using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Demo.Api.Endpoints;

namespace Tempo.Blazor.Demo.Api.Tests;

public class DiagramHistoryEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DiagramHistoryEndpointTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task SaveVersion_AndListVersions_ReturnsVersion()
    {
        var diagramId = $"test-{Guid.NewGuid():N}";
        var doc = CreateSampleDocument(diagramId);

        var saveResponse = await _client.PostAsJsonAsync($"/api/diagrams/{diagramId}/versions", new SaveVersionRequest
        {
            Document = doc,
            Label = "First version"
        });

        Assert.Equal(HttpStatusCode.Created, saveResponse.StatusCode);
        var saveResult = await saveResponse.Content.ReadFromJsonAsync<VersionResponse>();
        Assert.NotNull(saveResult);
        Assert.Equal(1, saveResult.Version);

        var versions = await _client.GetFromJsonAsync<List<DiagramHistoryVersion>>($"/api/diagrams/{diagramId}/versions");
        Assert.NotNull(versions);
        Assert.Single(versions);
        Assert.Equal(1, versions[0].Version);
        Assert.Equal("First version", versions[0].Label);
    }

    [Fact]
    public async Task SaveMultipleVersions_IncrementsVersion()
    {
        var diagramId = $"test-{Guid.NewGuid():N}";
        var doc = CreateSampleDocument(diagramId);

        await _client.PostAsJsonAsync($"/api/diagrams/{diagramId}/versions", new SaveVersionRequest { Document = doc });
        await _client.PostAsJsonAsync($"/api/diagrams/{diagramId}/versions", new SaveVersionRequest { Document = doc });
        await _client.PostAsJsonAsync($"/api/diagrams/{diagramId}/versions", new SaveVersionRequest { Document = doc });

        var versions = await _client.GetFromJsonAsync<List<DiagramHistoryVersion>>($"/api/diagrams/{diagramId}/versions");
        Assert.NotNull(versions);
        Assert.Equal(3, versions.Count);
        Assert.Contains(versions, v => v.Version == 1);
        Assert.Contains(versions, v => v.Version == 2);
        Assert.Contains(versions, v => v.Version == 3);
    }

    [Fact]
    public async Task LoadSpecificVersion_ReturnsDocument()
    {
        var diagramId = $"test-{Guid.NewGuid():N}";
        var doc = CreateSampleDocument(diagramId);
        doc.Title = "Version 1";

        await _client.PostAsJsonAsync($"/api/diagrams/{diagramId}/versions", new SaveVersionRequest { Document = doc });

        doc.Title = "Version 2";
        await _client.PostAsJsonAsync($"/api/diagrams/{diagramId}/versions", new SaveVersionRequest { Document = doc });

        var loaded = await _client.GetFromJsonAsync<DiagramDocument>($"/api/diagrams/{diagramId}/versions/1");
        Assert.NotNull(loaded);
        Assert.Equal("Version 1", loaded.Title);
    }

    [Fact]
    public async Task GetDiagrams_IncludesSavedDiagram()
    {
        var diagramId = $"test-{Guid.NewGuid():N}";
        var doc = CreateSampleDocument(diagramId);

        await _client.PostAsJsonAsync($"/api/diagrams/{diagramId}/versions", new SaveVersionRequest { Document = doc });

        var diagrams = await _client.GetFromJsonAsync<List<DiagramSummaryDto>>("/api/diagrams");
        Assert.NotNull(diagrams);
        Assert.Contains(diagrams, d => d.DiagramId == diagramId);
    }

    [Fact]
    public async Task DeleteDiagram_RemovesAllVersions()
    {
        var diagramId = $"test-{Guid.NewGuid():N}";
        var doc = CreateSampleDocument(diagramId);

        await _client.PostAsJsonAsync($"/api/diagrams/{diagramId}/versions", new SaveVersionRequest { Document = doc });

        var deleteResponse = await _client.DeleteAsync($"/api/diagrams/{diagramId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var versions = await _client.GetFromJsonAsync<List<DiagramHistoryVersion>>($"/api/diagrams/{diagramId}/versions");
        Assert.NotNull(versions);
        Assert.Empty(versions);
    }

    private static DiagramDocument CreateSampleDocument(string diagramId) => new()
    {
        Id = diagramId,
        Title = "Test diagram",
        Nodes =
        [
            new()
            {
                Id = "n1",
                StencilId = "general.rectangle",
                X = 50,
                Y = 50,
                W = 120,
                H = 60,
                Data = new() { ["label"] = "Start" }
            }
        ],
        Edges = []
    };

    private sealed class VersionResponse
    {
        public int Version { get; set; }
    }
}
