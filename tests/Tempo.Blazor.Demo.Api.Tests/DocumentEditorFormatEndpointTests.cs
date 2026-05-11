using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Odt;

namespace Tempo.Blazor.Demo.Api.Tests;

public class DocumentEditorFormatEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DocumentEditorFormatEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task JsonSourceEndpoints_LoadAndSaveDocument()
    {
        var load = await _client.GetFromJsonAsync<DocumentEditorLoadResult>("/api/document-editor/contract-demo");

        load.Should().NotBeNull();
        load!.Found.Should().BeTrue();
        load.Document.Should().NotBeNull();

        load.Document!.Metadata.Title = "Saved through JSON endpoint";
        var response = await _client.PutAsJsonAsync("/api/document-editor/contract-demo", new DocumentEditorSaveRequest
        {
            DocumentId = "contract-demo",
            Document = load.Document,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DemoSeed_IncludesImageDocumentCommentsAndVersions()
    {
        var load = await _client.GetFromJsonAsync<DocumentEditorLoadResult>("/api/document-editor/documents/exhibits-demo");

        load.Should().NotBeNull();
        load!.Found.Should().BeTrue();
        load.Document.Should().NotBeNull();
        load.Document!.Metadata.Title.Should().Be("Evidence exhibit");
        load.Document.Blocks
            .Select(block => block.Content)
            .OfType<ImageBlockContent>()
            .Should()
            .Contain(image => image.Source == DocumentImageSource.Url)
            .And.Contain(image => image.Source == DocumentImageSource.Asset && image.AssetId == "exhibit-provider-asset");

        var comments = await _client.GetFromJsonAsync<IReadOnlyList<DocumentComment>>(
            "/api/document-editor/documents/contract-demo/comments");
        var versions = await _client.GetFromJsonAsync<IReadOnlyList<DocumentVersion>>(
            "/api/document-editor/documents/contract-demo/versions");

        comments.Should().NotBeNull();
        comments.Should().Contain(comment => comment.Entries.Any(entry => entry.Text.Contains("client token", StringComparison.OrdinalIgnoreCase)));
        versions.Should().NotBeNull();
        versions.Should().Contain(version => version.Label == "1.0");
    }

    [Fact]
    public async Task ExportDocx_ReturnsDocxPackage()
    {
        var response = await _client.GetAsync("/api/document-editor/contract-demo/export/docx");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportOdt_ReturnsOdtPackage()
    {
        var response = await _client.GetAsync("/api/document-editor/contract-demo/export/odt");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/vnd.oasis.opendocument.text");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ImportDocx_SavesImportedDocument()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateImportDocument("Imported DOCX"));
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(exported.Content), "file", "import.docx");

        var response = await _client.PostAsync("/api/document-editor/import/docx", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var imported = await response.Content.ReadFromJsonAsync<DocumentFormatImportResult>();
        imported.Should().NotBeNull();
        imported!.Document.Metadata.Title.Should().Be("Imported DOCX");
    }

    [Fact]
    public async Task ImportOdt_SavesImportedDocument()
    {
        var exported = await new DocumentOdtExporter().ExportAsync(CreateImportDocument("Imported ODT"));
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(exported.Content), "file", "import.odt");

        var response = await _client.PostAsync("/api/document-editor/import/odt", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var imported = await response.Content.ReadFromJsonAsync<DocumentFormatImportResult>();
        imported.Should().NotBeNull();
        imported!.Document.Blocks.Should().Contain(block => block.Content is HeadingBlockContent);
    }

    [Fact]
    public async Task RenditionEndpoints_CreateRenditionPagesAndAnchorMap()
    {
        var versionResponse = await _client.PostAsJsonAsync(
            "/api/document-editor/documents/contract-demo/versions",
            new DocumentVersionCreateRequest
            {
                Kind = DocumentVersionKind.Major,
                Label = "Signing baseline"
            });
        versionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var version = await versionResponse.Content.ReadFromJsonAsync<DocumentVersion>();

        var renditionResponse = await _client.PostAsJsonAsync(
            "/api/document-editor/documents/contract-demo/renditions",
            new DocumentRenditionRequest
            {
                DocumentVersionId = version!.Id
            });

        renditionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var renditionResult = await renditionResponse.Content.ReadFromJsonAsync<DocumentRenditionResult>();
        renditionResult.Should().NotBeNull();
        renditionResult!.Rendition.Should().NotBeNull();
        renditionResult.Rendition!.Hash.SourceSnapshotHash.Should().Be(version.Snapshot.Hash);

        var pages = await _client.GetFromJsonAsync<IReadOnlyList<DocumentRenditionPage>>(
            $"/api/document-editor/renditions/{renditionResult.Rendition.Id}/pages");
        var anchors = await _client.GetFromJsonAsync<IReadOnlyList<DocumentRenditionAnchor>>(
            $"/api/document-editor/renditions/{renditionResult.Rendition.Id}/anchors");

        pages.Should().ContainSingle(page => page.PageNumber == 1);
        anchors.Should().Contain(anchor => anchor.Type == DocumentRenditionAnchorType.Token && anchor.Key == "client.name");
    }

    private static DocumentEditorDocument CreateImportDocument(string title)
    {
        var document = DocumentEditorDocument.Empty();
        document.Metadata.Title = title;
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Heading,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines = [new TextRun { Text = title }]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "Imported body" }]
            }
        });
        return document;
    }
}
