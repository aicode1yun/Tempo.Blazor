using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Tempo.Blazor.Demo.Api.Endpoints;
using Tempo.Blazor.Demo.Api.Services;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>
/// Phase 4 headless facade demo endpoint: POST /api/document-editor/assembly/render assembles the
/// demo contract template (IF/ELSE, repeating items, computed total/due date) with the supplied
/// dataset and renders a PDF or per-page PNG previews purely server-side.
/// </summary>
public class DemoAssemblyRenderEndpointTests : Xunit.IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DemoAssemblyRenderEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private static object CreateDataset(int amount) => new
    {
        values = new Dictionary<string, string?>
        {
            ["contract.client"] = "Acme s.r.o.",
            ["contract.amount"] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        },
        itemRows = new[]
        {
            new Dictionary<string, string?> { ["name"] = "Servis A", ["price"] = "15000" },
            new Dictionary<string, string?> { ["name"] = "Servis B", ["price"] = "10000" },
        },
    };

    [Fact]
    public async Task Render_Pdf_ReturnsAServerAssembledWysiwygPdf()
    {
        if (!new DemoDocumentExportFontCatalog().HasFonts)
        {
            return;
        }

        var response = await _client.PostAsJsonAsync("/api/document-editor/assembly/render", CreateDataset(25000));

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        Encoding.Latin1.GetString(bytes).Should().Contain("/FontFile", "the endpoint renders through the production vector renderer");
    }

    [Fact]
    public async Task Render_Png_ReturnsOnePreviewPerPageAtRequestedDpi()
    {
        if (!new DemoDocumentExportFontCatalog().HasFonts)
        {
            return;
        }

        var response = await _client.PostAsJsonAsync(
            "/api/document-editor/assembly/render",
            new
            {
                values = new Dictionary<string, string?>
                {
                    ["contract.client"] = "Acme s.r.o.",
                    ["contract.amount"] = "5000",
                },
                itemRows = new[] { new Dictionary<string, string?> { ["name"] = "Servis C", ["price"] = "5000" } },
                format = "png",
                dpi = 192,
            });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DemoAssemblyRenderPngResult>();
        result!.PageCount.Should().BeGreaterThanOrEqualTo(1);
        result.Pages.Should().HaveCount(result.PageCount);

        var png = Convert.FromBase64String(result.Pages[0].Png);
        png[..8].Should().Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        // A4 portrait at 192 dpi ≈ 1588 px wide (793.7 css px × 2).
        result.Pages[0].Width.Should().BeInRange(1580, 1596);
    }
}
