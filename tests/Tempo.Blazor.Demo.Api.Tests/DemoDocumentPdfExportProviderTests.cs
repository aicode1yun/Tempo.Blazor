using System.Text;
using FluentAssertions;
using Tempo.Blazor.Demo.Api.Services;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>
/// The demo PDF export provider uses the production WYSIWYG renderer for BOTH paths (phase 3 of
/// the headless document runtime): requests carrying the canvas layout snapshot render it
/// directly, and snapshot-less requests are laid out server-side through
/// <c>ITempoDocumentLayoutService</c> (the same JS layout chain the editor paints with) — the
/// legacy text-only PDF writer is gone.
/// </summary>
public class DemoDocumentPdfExportProviderTests
{
    private static DemoDocumentPdfExportProvider CreateProvider()
        => new(new JintDocumentLayoutEngine(), new DemoDocumentExportFontCatalog());

    [Fact]
    public async Task ExportPdf_WithLayoutSnapshot_RendersOnePdfPagePerSnapshotPage()
    {
        var provider = CreateProvider();
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
    public async Task ExportPdf_WithoutLayoutSnapshot_LaysOutServerSideWithWysiwygParity()
    {
        var fontCatalog = new DemoDocumentExportFontCatalog();
        if (!fontCatalog.HasFonts)
        {
            // Same skip pattern as the DejaVu-based renderer tests: no system fonts on this machine.
            return;
        }

        var provider = CreateProvider();
        var document = DocumentEditorDocument.Empty("headless-doc");
        document.Metadata.Title = "Headless export";
        document.Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Heading,
                Order = 0,
                Content = new HeadingBlockContent { Level = 1, Inlines = [new TextRun { Text = "Smlouva o dílo" }] },
            },
            new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Order = 1,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Text = "Příliš žluťoučký kůň úpěl ďábelské ódy — headless server-side layout." }],
                },
            },
        ];

        var result = await provider.ExportPdfAsync(new DocumentPdfExportRequest
        {
            DocumentId = "headless-doc",
            Document = document,
        });

        result.ContentType.Should().Be("application/pdf");
        var text = Encoding.Latin1.GetString(result.Content);
        Encoding.ASCII.GetString(result.Content, 0, 5).Should().Be("%PDF-");
        CountOccurrences(text, "/MediaBox").Should().BeGreaterThanOrEqualTo(1);
        // The WYSIWYG renderer embeds and subsets real fonts — the legacy stub used base-14
        // Helvetica only. This is the structural proof the stub is gone.
        text.Should().Contain("/FontFile", "server-side layout must flow through the production vector renderer");
        text.Should().NotContain("% Tempo.Blazor demo PDF export", "the legacy text-only writer must be deleted");
    }

    [Fact]
    public async Task ExportPdf_WithoutLayoutSnapshot_EmptyDocument_YieldsOnePageWysiwygPdf()
    {
        var fontCatalog = new DemoDocumentExportFontCatalog();
        if (!fontCatalog.HasFonts)
        {
            return;
        }

        var provider = CreateProvider();

        var result = await provider.ExportPdfAsync(new DocumentPdfExportRequest
        {
            DocumentId = "empty-doc",
            Document = DocumentEditorDocument.Empty("empty-doc"),
        });

        Encoding.ASCII.GetString(result.Content, 0, 5).Should().Be("%PDF-");
        CountOccurrences(Encoding.Latin1.GetString(result.Content), "/MediaBox").Should().Be(1,
            "an empty document lays out as a single empty page");
    }

    [Fact]
    public void FontCatalog_RegistersSystemFacesUnderArialAndAptosAliases()
    {
        var catalog = new DemoDocumentExportFontCatalog();
        if (!catalog.HasFonts)
        {
            return;
        }

        catalog.Fonts.Should().Contain(face => face.Family == "Arial");
        catalog.Fonts.Should().Contain(
            face => face.Family == "Aptos",
            "demo documents use the 'Aptos, Arial, sans-serif' theme — the alias keeps face resolution deterministic");
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
    public async Task GetExport_WithoutSnapshot_ServesHeadlessWysiwygPdf()
    {
        var catalog = new DemoDocumentExportFontCatalog();
        if (!catalog.HasFonts)
        {
            return;
        }

        // GET export has no client snapshot — the server lays the stored document out headlessly.
        var response = await _client.GetAsync("/api/document-editor/contract-demo/export/pdf");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        var text = Encoding.Latin1.GetString(bytes);
        text.Should().Contain("/FontFile", "the headless path renders through the production vector renderer");
    }

    [Fact]
    public async Task GetLast_WithoutPriorExport_Returns404()
    {
        var response = await _client.GetAsync("/api/document-editor/never-exported-doc/export/pdf/last");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

}
