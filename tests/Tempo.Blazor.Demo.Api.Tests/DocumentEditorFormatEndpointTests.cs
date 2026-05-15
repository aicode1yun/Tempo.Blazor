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
    public async Task ExportPdf_ReturnsPdfFile()
    {
        var response = await _client.GetAsync("/api/document-editor/contract-demo/export/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Take(4).Should().Equal(0x25, 0x50, 0x44, 0x46);
    }

    [Fact]
    public async Task ExportPdf_MissingDocument_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/document-editor/missing-document/export/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
    public async Task ProviderStyleImportDocx_ReturnsProviderResult()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateImportDocument("Provider Imported DOCX"));
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(exported.Content), "file", "provider-warning.docx");

        var response = await _client.PostAsync("/api/document-editor/formats/import?format=Docx", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var imported = await response.Content.ReadFromJsonAsync<DocumentFormatImportProviderResult>();
        imported.Should().NotBeNull();
        imported!.Success.Should().BeTrue();
        imported.Format.Should().Be(DocumentFormatProviderKind.Docx);
        imported.Document!.Metadata.Title.Should().Be("Provider Imported DOCX");
        imported.Warnings.Should().Contain(warning => warning.Code == "demo.approximation");
    }

    [Fact]
    public async Task ProviderStyleExportDocx_ReturnsProviderResult()
    {
        var document = CreateImportDocument("Provider Exported DOCX");

        var response = await _client.PostAsJsonAsync("/api/document-editor/formats/export", new DocumentFormatExportProviderRequest
        {
            DocumentId = document.DocumentId,
            Format = DocumentFormatProviderKind.Docx,
            Document = document,
            FileName = "provider-export"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var exported = await response.Content.ReadFromJsonAsync<DocumentFormatExportProviderResult>();
        exported.Should().NotBeNull();
        exported!.Success.Should().BeTrue();
        exported.Content.Should().NotBeEmpty();
        exported.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        exported.FileName.Should().EndWith(".docx");
    }

    [Fact]
    public async Task ProviderStyleExportPdf_ReturnsProviderResult()
    {
        var document = CreateImportDocument("Provider Exported PDF");

        var response = await _client.PostAsJsonAsync($"/api/document-editor/{document.DocumentId}/export/pdf", new DocumentPdfExportRequest
        {
            DocumentId = document.DocumentId,
            Document = document,
            FileName = "provider-export",
            Options = new DocumentPdfExportOptions
            {
                IncludeComments = false,
                IncludeSuggestions = false,
                PageSetup = new DocumentPdfPageSetupOptions
                {
                    PageSize = DocumentPageSize.Letter,
                    Orientation = DocumentPdfPageOrientation.Portrait,
                    Margins = new DocumentPageMargins { Top = 36, Right = 36, Bottom = 36, Left = 36 }
                }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var exported = await response.Content.ReadFromJsonAsync<DocumentPdfExportResult>();
        exported.Should().NotBeNull();
        exported!.Content.Take(4).Should().Equal(0x25, 0x50, 0x44, 0x46);
        exported.ContentType.Should().Be("application/pdf");
        exported.FileName.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task CompareDocuments_ByDocumentIds_ReturnsComparisonResult()
    {
        var response = await _client.GetAsync("/api/document-editor/compare?baseDocumentId=contract-demo&compareDocumentId=filing-demo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DocumentCompareResult>();
        result.Should().NotBeNull();
        result!.Summary.HasChanges.Should().BeTrue();
        result.BaseDocument!.DocumentId.Should().Be("contract-demo");
        result.CompareDocument!.DocumentId.Should().Be("filing-demo");
    }

    [Fact]
    public async Task CompareDocuments_CurrentSnapshotVsStoredDocument_ReturnsComparisonResult()
    {
        var current = CreateImportDocument("Current Snapshot");
        current.DocumentId = "current-snapshot";

        var response = await _client.PostAsJsonAsync("/api/document-editor/compare", new DocumentCompareRequest
        {
            DocumentId = current.DocumentId,
            CurrentDocument = current,
            BaseSource = new DocumentCompareSource
            {
                Kind = DocumentCompareSourceKind.Current,
                Document = current
            },
            CompareSource = new DocumentCompareSource
            {
                Kind = DocumentCompareSourceKind.DocumentId,
                DocumentId = "contract-demo"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DocumentCompareResult>();
        result.Should().NotBeNull();
        result!.Summary.HasChanges.Should().BeTrue();
        result.BaseDocument!.DocumentId.Should().Be("current-snapshot");
        result.CompareDocument!.DocumentId.Should().Be("contract-demo");
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

    [Fact]
    public async Task CollaborationEndpoints_JoinBroadcastBatchAndCursor()
    {
        var joinResponse = await _client.PostAsJsonAsync(
            "/api/document-editor/collaboration/join",
            new DocumentCollaborationJoinRequest
            {
                DocumentId = "contract-demo",
                ClientId = "api-test",
                Author = new DocumentEditorAuthor { Id = "api-test", DisplayName = "API Test" }
            });

        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await joinResponse.Content.ReadFromJsonAsync<DocumentCollaborationSession>();
        session.Should().NotBeNull();

        var batchResponse = await _client.PostAsJsonAsync(
            $"/api/document-editor/collaboration/{session!.Id}/batches",
            new DocumentOperationBatch
            {
                DocumentId = "contract-demo",
                Operations =
                [
                    new DocumentOperation
                    {
                        Type = DocumentOperationType.SetBlockAttribute,
                        Target = new DocumentOperationTarget { BlockId = "b-1" },
                        AttributeName = "text",
                        AttributeValueJson = "\"API collaboration\""
                    }
                ]
            });
        batchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var batches = await _client.GetFromJsonAsync<IReadOnlyList<DocumentCollaborationOperationBatch>>(
            "/api/document-editor/collaboration/documents/contract-demo/batches?afterSequence=0");
        batches.Should().ContainSingle(batch => batch.SessionId == session.Id);

        var cursorResponse = await _client.PostAsJsonAsync(
            "/api/document-editor/collaboration/cursors",
            new DocumentCollaborationCursor
            {
                DocumentId = "contract-demo",
                SessionId = session.Id,
                ClientId = "api-test",
                DisplayName = "API Test",
                BlockId = "b-1",
                Offset = 2
            });
        cursorResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cursors = await _client.GetFromJsonAsync<IReadOnlyList<DocumentCollaborationCursor>>(
            "/api/document-editor/collaboration/documents/contract-demo/cursors");
        cursors.Should().ContainSingle(cursor => cursor.DisplayName == "API Test");

        var leaveResponse = await _client.PostAsync(
            $"/api/document-editor/collaboration/{session.Id}/leave",
            content: null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SuggestionEndpoints_CreateListAndReviewSuggestion()
    {
        var suggestion = new DocumentSuggestion
        {
            DocumentId = "contract-demo",
            Type = DocumentSuggestionType.ReplaceText,
            Range = new DocumentRevisionRange { BlockId = "b-1" },
            OriginalText = "Original",
            SuggestedText = "Suggested through API",
            Author = new DocumentEditorAuthor { Id = "api-author", DisplayName = "API Author" },
            BaseSnapshotHash = new string('c', 64),
            Operations =
            [
                new DocumentOperation
                {
                    Type = DocumentOperationType.SetBlockAttribute,
                    Target = new DocumentOperationTarget { BlockId = "b-1" },
                    AttributeName = "text",
                    AttributeValueJson = "\"Suggested through API\""
                }
            ]
        };

        var createResponse = await _client.PostAsJsonAsync("/api/document-editor/suggestions", suggestion);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<DocumentSuggestion>();
        created.Should().NotBeNull();
        created!.Status.Should().Be(DocumentSuggestionStatus.Pending);

        var pending = await _client.GetFromJsonAsync<IReadOnlyList<DocumentSuggestion>>(
            "/api/document-editor/suggestions/documents/contract-demo?status=Pending");
        pending.Should().ContainSingle(item => item.Id == created.Id);

        var reviewResponse = await _client.PostAsJsonAsync(
            "/api/document-editor/suggestions/review",
            new DocumentSuggestionReviewRequest
            {
                DocumentId = "contract-demo",
                SuggestionId = created.Id,
                Status = DocumentSuggestionStatus.Accepted,
                Reviewer = new DocumentEditorAuthor { Id = "api-reviewer", DisplayName = "API Reviewer" }
            });
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reviewed = await reviewResponse.Content.ReadFromJsonAsync<DocumentSuggestion>();
        reviewed.Should().NotBeNull();
        reviewed!.Status.Should().Be(DocumentSuggestionStatus.Accepted);
        reviewed.Reviewer!.Id.Should().Be("api-reviewer");
        reviewed.ReviewedAt.Should().NotBeNull();

        var pendingAfterReview = await _client.GetFromJsonAsync<IReadOnlyList<DocumentSuggestion>>(
            "/api/document-editor/suggestions/documents/contract-demo?status=Pending");
        pendingAfterReview.Should().NotContain(item => item.Id == created.Id);
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
