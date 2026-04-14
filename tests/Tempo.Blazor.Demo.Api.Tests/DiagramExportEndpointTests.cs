using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Demo.Api.Endpoints;

namespace Tempo.Blazor.Demo.Api.Tests;

public class DiagramExportEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DiagramExportEndpointTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task ExportSvg_ReturnsSvgContent()
    {
        var request = CreateSampleRequest();

        var response = await _client.PostAsJsonAsync("/api/diagram/export/svg", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportPng_ReturnsPngFile()
    {
        var request = CreateSampleRequest();

        var response = await _client.PostAsJsonAsync("/api/diagram/export/png", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        // PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    [Fact]
    public async Task ExportPdf_ReturnsPdfFile()
    {
        var request = CreateSampleRequest();

        var response = await _client.PostAsJsonAsync("/api/diagram/export/pdf", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        // PDF magic bytes "%PDF"
        Assert.Equal(0x25, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x44, bytes[2]);
        Assert.Equal(0x46, bytes[3]);
    }

    private static DiagramExportRequest CreateSampleRequest()
    {
        var doc = new DiagramDocument
        {
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
                },
                new()
                {
                    Id = "n2",
                    StencilId = "general.rectangle",
                    X = 250,
                    Y = 50,
                    W = 120,
                    H = 60,
                    Data = new() { ["label"] = "End" }
                }
            ],
            Edges =
            [
                new()
                {
                    Id = "e1",
                    SourceNodeId = "n1",
                    TargetNodeId = "n2",
                    ConnectorType = "association"
                }
            ]
        };

        var options = new DiagramExportOptions
        {
            Padding = 20,
            BackgroundColor = "#ffffff"
        };

        return new DiagramExportRequest { Document = doc, Options = options };
    }
}
