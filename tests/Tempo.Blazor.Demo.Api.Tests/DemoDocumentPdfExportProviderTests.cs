using System.Text;
using FluentAssertions;
using Tempo.Blazor.Demo.Api.Services;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>
/// The demo PDF export provider must use the production WYSIWYG renderer
/// (<c>TempoDocumentPdfRenderer</c>) whenever the request carries the canvas layout snapshot,
/// and keep the legacy text-only rendering as the fallback for snapshot-less requests.
/// </summary>
public class DemoDocumentPdfExportProviderTests
{
    [Fact]
    public async Task ExportPdf_WithLayoutSnapshot_RendersOnePdfPagePerSnapshotPage()
    {
        var provider = new DemoDocumentPdfExportProvider();
        var request = new DocumentPdfExportRequest
        {
            DocumentId = "snapshot-doc",
            Document = DocumentEditorDocument.Empty(),
            LayoutSnapshotJson = """
            {
              "schemaVersion": 1,
              "pageCount": 2,
              "pages": [
                { "index": 0, "width": 794, "height": 1123, "commands": [
                  { "id": "t1", "type": "text", "x": 96, "y": 100, "width": 300, "height": 20, "baseline": 116,
                    "text": "Žluťoučký kůň – strana 1", "fontFamily": "Arial", "fontSize": 16, "fontWeight": "400",
                    "fontStyle": "normal", "fill": "#111827" } ] },
                { "index": 1, "width": 794, "height": 1123, "commands": [
                  { "id": "t2", "type": "text", "x": 96, "y": 100, "width": 300, "height": 20, "baseline": 116,
                    "text": "Strana 2", "fontFamily": "Arial", "fontSize": 16, "fontWeight": "400",
                    "fontStyle": "normal", "fill": "#111827" } ] }
              ]
            }
            """,
        };

        var result = await provider.ExportPdfAsync(request);

        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().Be("snapshot-doc.pdf");
        Encoding.ASCII.GetString(result.Content, 0, 5).Should().Be("%PDF-");
        CountOccurrences(Encoding.Latin1.GetString(result.Content), "/MediaBox").Should().Be(2);
    }

    [Fact]
    public async Task ExportPdf_WithoutLayoutSnapshot_KeepsLegacyTextOnlyFallback()
    {
        var provider = new DemoDocumentPdfExportProvider();
        var document = DocumentEditorDocument.Empty();
        document.DocumentId = "legacy-doc";
        var request = new DocumentPdfExportRequest
        {
            DocumentId = "legacy-doc",
            Document = document,
        };

        var result = await provider.ExportPdfAsync(request);

        result.ContentType.Should().Be("application/pdf");
        Encoding.ASCII.GetString(result.Content, 0, 5).Should().Be("%PDF-");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}

/// <summary>
/// The POST export endpoint caches the last produced PDF per document so demo surfaces
/// (TmPdfViewer via <c>/pdf-viewer?url=…</c>) and E2E can open the real exported file.
/// </summary>
public class DocumentEditorPdfExportEndpointTests : Xunit.IClassFixture<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DocumentEditorPdfExportEndpointTests(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostExport_ThenGetLast_ReturnsTheSameProductionPdf()
    {
        var request = new DocumentPdfExportRequest
        {
            DocumentId = "contract-demo",
            Document = DocumentEditorDocument.Empty(),
            LayoutSnapshotJson = """
            {
              "schemaVersion": 1,
              "pageCount": 1,
              "pages": [
                { "index": 0, "width": 794, "height": 1123, "commands": [
                  { "id": "t1", "type": "text", "x": 96, "y": 100, "width": 300, "height": 20, "baseline": 116,
                    "text": "Endpoint export", "fontFamily": "Arial", "fontSize": 16, "fontWeight": "400",
                    "fontStyle": "normal", "fill": "#111827" } ] }
              ]
            }
            """,
        };

        var post = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(
            _client, "/api/document-editor/contract-demo/export/pdf", request);
        post.EnsureSuccessStatusCode();
        var result = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<DocumentPdfExportResult>(post.Content);
        result!.Content.Length.Should().BeGreaterThan(0);

        var last = await _client.GetAsync("/api/document-editor/contract-demo/export/pdf/last");
        last.EnsureSuccessStatusCode();
        last.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var lastBytes = await last.Content.ReadAsByteArrayAsync();
        lastBytes.Should().Equal(result.Content, "the last-export endpoint must serve exactly the produced PDF");
    }

    [Fact]
    public async Task GetLast_WithoutPriorExport_Returns404()
    {
        var response = await _client.GetAsync("/api/document-editor/never-exported-doc/export/pdf/last");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

}
