using System.Net;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Odt;

namespace Tempo.Blazor.Demo.Api.Tests;

public class DocumentEditorFormatEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";

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
        var contractLoad = await _client.GetFromJsonAsync<DocumentEditorLoadResult>("/api/document-editor/documents/contract-demo");

        load.Should().NotBeNull();
        load!.Found.Should().BeTrue();
        load.Document.Should().NotBeNull();
        load.Document!.Metadata.Title.Should().Be("Evidence exhibit");
        load.Document.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        DocumentImagePersistence.EnumerateDrawingRuns(load.Document)
            .Should()
            .OnlyContain(drawing => drawing.Source == DocumentImageSource.Asset)
            .And.Contain(drawing => drawing.AssetId == "exhibit-provider-asset");

        contractLoad.Should().NotBeNull();
        contractLoad!.Found.Should().BeTrue();
        contractLoad.Document.Should().NotBeNull();
        var contract = contractLoad.Document!;
        contract.HeadersFooters.Should().Contain(header => header.Id == "contract-header-primary");
        contract.HeadersFooters.Should().Contain(footer => footer.Id == "contract-footer-primary");
        contract.Revisions.Should().Contain(revision =>
            revision.Id == "contract-revision-scope"
            && revision.Action == DocumentRevisionAction.Pending
            && revision.Type == DocumentRevisionType.Insertion);
        contract.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        DocumentImagePersistence.EnumerateDrawingRuns(contract)
            .Should()
            .Contain(drawing =>
                drawing.Source == DocumentImageSource.Asset
                && drawing.AssetId == "contract-evidence-asset"
                && drawing.Size.Width >= 200);
        contract.Blocks
            .Select(block => block.Content)
            .OfType<ParagraphBlockContent>()
            .SelectMany(content => content.Inlines)
            .OfType<TextRun>()
            .Should()
            .Contain(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Bold))
            .And.Contain(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Revision && mark.RevisionId == "contract-revision-scope"));

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
    public async Task DemoSeeds_UseDrawingRunsWithoutTopLevelImageBlocks()
    {
        foreach (var documentId in new[] { "contract-demo", "exhibits-demo", "onlyoffice-parity-2026-05-24" })
        {
            var load = await _client.GetFromJsonAsync<DocumentEditorLoadResult>($"/api/document-editor/documents/{documentId}");
            load.Should().NotBeNull();
            load!.Found.Should().BeTrue();
            load.Document.Should().NotBeNull();
            load.Document!.Blocks.Should().NotContain(block => block.Content is ImageBlockContent, $"{documentId} should use drawing runs");
            DocumentImagePersistence.EnumerateDrawingRuns(load.Document).Should().NotBeEmpty($"{documentId} should include image drawing runs");
        }

        var onlyOffice = await _client.GetFromJsonAsync<DocumentEditorLoadResult>("/api/document-editor/documents/onlyoffice-parity-2026-05-24");
        var drawings = DocumentImagePersistence.EnumerateDrawingRuns(onlyOffice!.Document!).ToArray();
        var modes = drawings.Select(drawing => drawing.Layout.Wrap.Mode).ToArray();
        modes.Should().Contain(DocumentWrapMode.Inline);
        modes.Should().Contain(DocumentWrapMode.Square);
        modes.Should().Contain(DocumentWrapMode.TopBottom);
        modes.Should().Contain(DocumentWrapMode.Tight);
        modes.Should().Contain(DocumentWrapMode.Through);
        modes.Should().Contain(DocumentWrapMode.BehindText);
        modes.Should().Contain(DocumentWrapMode.InFrontOfText);
        drawings
            .Should()
            .Contain(drawing => drawing.Layout.Anchor.Region == DocumentRenditionAnchorScope.Header)
            .And.Contain(drawing => drawing.Layout.Anchor.Region == DocumentRenditionAnchorScope.Footer)
            .And.Contain(drawing => drawing.Layout.Anchor.CellId == "recovery-table-image-cell");
        drawings.Should().Contain(drawing => drawing.Layout.Transform.Crop.Left > 0);
        drawings.Should().Contain(drawing => Math.Abs(drawing.Layout.Transform.Rotation) > 0.01);
    }

    [Fact]
    public async Task Phase14_ContractDemoSeedIncludesOnlyOfficeLevelImageWrapScenarios()
    {
        var load = await _client.GetFromJsonAsync<DocumentEditorLoadResult>("/api/document-editor/documents/contract-demo");

        load.Should().NotBeNull();
        load!.Found.Should().BeTrue();
        load.Document.Should().NotBeNull();
        var document = load.Document!;
        document.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);

        var drawings = DocumentImagePersistence.EnumerateDrawingRuns(document)
            .ToDictionary(drawing => drawing.ObjectId, StringComparer.Ordinal);
        drawings.Keys.Should().Contain([
            "contract-left-wrap-image",
            "contract-right-wrap-image",
            "contract-center-wrap-image",
            "contract-offset-wrap-image",
            "contract-top-bottom-image",
            "contract-tight-wrap-image",
            "contract-in-front-image",
            "contract-behind-text-image",
            "contract-header-logo-image",
            "contract-footer-logo-image",
            "contract-table-cell-image"
        ]);

        AssertDrawing(drawings["contract-left-wrap-image"], DocumentWrapMode.Square, DocumentImageHorizontalPosition.Left, DocumentRenditionAnchorScope.Body);
        AssertDrawing(drawings["contract-right-wrap-image"], DocumentWrapMode.Square, DocumentImageHorizontalPosition.Right, DocumentRenditionAnchorScope.Body);
        AssertDrawing(drawings["contract-center-wrap-image"], DocumentWrapMode.Square, DocumentImageHorizontalPosition.Center, DocumentRenditionAnchorScope.Body);
        drawings["contract-center-wrap-image"].Layout.Anchor.BlockId.Should().Be("contract-center-wrap-text");

        var offset = drawings["contract-offset-wrap-image"];
        offset.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        offset.Layout.Position.HorizontalAlignment.Should().BeNull();
        offset.Layout.Position.X.Should().BeGreaterThan(40);
        offset.Layout.Position.Y.Should().BeGreaterThan(0);

        var tight = drawings["contract-tight-wrap-image"];
        tight.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Tight);
        tight.Layout.Wrap.Side.Should().Be(DocumentObjectWrapSide.Largest);
        tight.Layout.Wrap.WrapContourPoints.Should().HaveCountGreaterThanOrEqualTo(4);

        drawings["contract-in-front-image"].Layout.Wrap.Mode.Should().Be(DocumentWrapMode.InFrontOfText);
        drawings["contract-in-front-image"].Layout.Kind.Should().Be(DocumentObjectLayoutKind.Fixed);
        drawings["contract-in-front-image"].Layout.Anchor.FixedOnPage.Should().BeTrue();

        drawings["contract-behind-text-image"].Layout.Wrap.Mode.Should().Be(DocumentWrapMode.BehindText);
        drawings["contract-behind-text-image"].Layout.Kind.Should().Be(DocumentObjectLayoutKind.Fixed);
        drawings["contract-behind-text-image"].Layout.Anchor.FixedOnPage.Should().BeTrue();

        AssertDrawing(drawings["contract-header-logo-image"], DocumentWrapMode.Inline, null, DocumentRenditionAnchorScope.Header);
        drawings["contract-header-logo-image"].Layout.Anchor.HeaderFooterId.Should().Be("contract-header-primary");
        AssertDrawing(drawings["contract-footer-logo-image"], DocumentWrapMode.Inline, null, DocumentRenditionAnchorScope.Footer);
        drawings["contract-footer-logo-image"].Layout.Anchor.HeaderFooterId.Should().Be("contract-footer-primary");

        var tableCell = drawings["contract-table-cell-image"];
        tableCell.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.TableCell);
        tableCell.Layout.Anchor.TableId.Should().Be("contract-pricing-table");
        tableCell.Layout.Anchor.CellId.Should().Be("contract-pricing-table-r1-evidence");
        tableCell.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);

        GetBlockText(document, "contract-center-wrap-text").Should().Contain("both sides of the centered preview");
        GetBlockText(document, "contract-center-wrap-text").Length.Should().BeGreaterThan(240);
        GetBlockText(document, "contract-offset-wrap-text").Should().Contain("arbitrary drag-like offset");
        GetBlockText(document, "contract-tight-wrap-text").Should().Contain("custom diamond contour");
        GetBlockText(document, "contract-layering-text").Should().Contain("in front of text");
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
    public async Task Phase38_ExportDocx_ImageParityDemoContainsNativeDrawingMl()
    {
        var response = await _client.GetAsync("/api/document-editor/image-parity/export-docx");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var documentXml = ReadPackageXml(archive, "word/document.xml");
        var headerXml = ReadPackageXml(archive, archive.Entries.Single(entry => entry.FullName.StartsWith("word/header", StringComparison.OrdinalIgnoreCase)).FullName);
        var footerXml = ReadPackageXml(archive, archive.Entries.Single(entry => entry.FullName.StartsWith("word/footer", StringComparison.OrdinalIgnoreCase)).FullName);

        documentXml.Descendants(Wp + "inline").Should().NotBeEmpty();
        documentXml.Descendants(Wp + "anchor").Should().NotBeEmpty();
        headerXml.Descendants(W + "drawing").Should().NotBeEmpty();
        footerXml.Descendants(W + "drawing").Should().NotBeEmpty();
        documentXml.Descendants(W + "tc").Descendants(W + "drawing").Should().NotBeEmpty();
        documentXml.Descendants(Wp + "wrapTight").Should().NotBeEmpty();
        documentXml.Descendants(Wp + "wrapThrough").Should().NotBeEmpty();
        documentXml.Descendants(A + "srcRect").Should().NotBeEmpty();
        documentXml.Descendants(A + "xfrm")
            .Should()
            .Contain(element => element.Attribute("rot") != null);
        documentXml.Descendants(W + "t").Select(text => text.Value).Should().NotContain("[Image]");
        archive.Entries.Should().Contain(entry => entry.FullName.Contains("media/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Phase38_ImportDocx_ImageParityFixturePreservesDrawingRegions()
    {
        var response = await _client.PostAsync("/api/document-editor/image-parity/import-onlyoffice-fixture", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var imported = await response.Content.ReadFromJsonAsync<DocumentFormatImportProviderResult>();

        imported.Should().NotBeNull();
        imported!.Success.Should().BeTrue();
        imported.Document.Should().NotBeNull();
        imported.Document!.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        var drawings = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).ToArray();
        drawings.Should().Contain(drawing => drawing.Layout.Anchor.Region == DocumentRenditionAnchorScope.Body);
        drawings.Should().Contain(drawing => drawing.Layout.Anchor.Region == DocumentRenditionAnchorScope.Header);
        drawings.Should().Contain(drawing => drawing.Layout.Anchor.Region == DocumentRenditionAnchorScope.Footer);
        drawings.Should().Contain(drawing => drawing.Layout.Anchor.Region == DocumentRenditionAnchorScope.TableCell);
        drawings.Should().Contain(drawing => drawing.Source == DocumentImageSource.Asset);
    }

    [Fact]
    public async Task Phase38_ProviderStyleExportDocx_ReturnsCompatibilityWarningsForUi()
    {
        var document = CreateUnresolvedExternalImageDocument();

        var response = await _client.PostAsJsonAsync("/api/document-editor/formats/export", new DocumentFormatExportProviderRequest
        {
            DocumentId = document.DocumentId,
            Format = DocumentFormatProviderKind.Docx,
            Document = document,
            FileName = "provider-export-warning"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var exported = await response.Content.ReadFromJsonAsync<DocumentFormatExportProviderResult>();

        exported.Should().NotBeNull();
        exported!.Warnings.Should().Contain(warning =>
            warning.Code == "docx.imageExternalUrlUnsupported"
            && warning.Severity == DocumentFormatProviderWarningSeverity.Dropped);
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
    public async Task FormatExportImport_RoundtripsDrawingRunsWithoutLegacyImageBlocks()
    {
        var document = CreateDrawingRunDocument("Drawing export");

        var docxExport = await new DocumentDocxExporter().ExportAsync(document);
        var docxImport = await new DocumentDocxImporter().ImportAsync(new MemoryStream(docxExport.Content));
        var docxDrawings = DocumentImagePersistence.EnumerateDrawingRuns(docxImport.Document).ToArray();

        var odtExport = await new DocumentOdtExporter().ExportAsync(document);
        var odtImport = await new DocumentOdtImporter().ImportAsync(new MemoryStream(odtExport.Content));
        var odtDrawings = DocumentImagePersistence.EnumerateDrawingRuns(odtImport.Document).ToArray();

        docxDrawings.Should().ContainSingle();
        docxDrawings[0].AltText.Should().Be("Exported drawing");
        docxDrawings[0].Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        docxImport.Document.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);

        odtDrawings.Should().ContainSingle();
        odtDrawings[0].AltText.Should().Be("Exported drawing");
        odtDrawings[0].Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        odtImport.Document.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
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
                ProtocolVersion = DocumentOperationBatch.CurrentProtocolVersion,
                Operations =
                [
                    new DocumentOperation
                    {
                        OperationId = "api-operation-1",
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
        batches.Should().ContainSingle(batch =>
            batch.SessionId == session.Id
            && batch.Batch.ProtocolVersion == DocumentOperationBatch.CurrentProtocolVersion
            && batch.Batch.Operations.Single().OperationId == "api-operation-1");

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

    private static DocumentEditorDocument CreateDrawingRunDocument(string title)
    {
        var document = CreateImportDocument(title);
        document.Blocks.Add(new DocumentBlock
        {
            Id = "drawing-paragraph",
            Type = DocumentBlockType.Paragraph,
            Order = 2,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Before image " },
                    new DocumentDrawingRun
                    {
                        ObjectId = "export-drawing",
                        Source = DocumentImageSource.Url,
                        Url = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=",
                        AltText = "Exported drawing",
                        Caption = "Exported drawing caption",
                        Size = new DocumentImageSize { Width = 120, Height = 80 },
                        NaturalSize = new DocumentImageSize { Width = 120, Height = 80 },
                        Layout = new DocumentObjectLayout
                        {
                            Kind = DocumentObjectLayoutKind.Anchored,
                            Anchor = new DocumentObjectAnchor { BlockId = "drawing-paragraph", Offset = 13 },
                            Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Square, DistanceLeft = 6, DistanceRight = 6 },
                            Transform = new DocumentObjectTransform { Width = 120, Height = 80, NaturalWidth = 120, NaturalHeight = 80 }
                        }
                    }
                ]
            }
        });

        return document;
    }

    private static DocumentEditorDocument CreateUnresolvedExternalImageDocument()
    {
        var document = CreateImportDocument("External image warning");
        document.DocumentId = "external-image-warning";
        document.Blocks.Add(new DocumentBlock
        {
            Id = "external-image-paragraph",
            Type = DocumentBlockType.Paragraph,
            Order = 2,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "External image " },
                    new DocumentDrawingRun
                    {
                        ObjectId = "external-image",
                        Source = DocumentImageSource.Url,
                        Url = "https://example.test/image.png",
                        AltText = "External unresolved image",
                        Size = new DocumentImageSize { Width = 64, Height = 64 },
                        NaturalSize = new DocumentImageSize { Width = 64, Height = 64 },
                        Layout = DocumentObjectLayout.Inline()
                    }
                ]
            }
        });

        return document;
    }

    private static void AssertDrawing(
        DocumentDrawingRun drawing,
        DocumentWrapMode wrapMode,
        DocumentImageHorizontalPosition? horizontalPosition,
        DocumentRenditionAnchorScope region)
    {
        drawing.Layout.Wrap.Mode.Should().Be(wrapMode);
        drawing.Layout.Anchor.Region.Should().Be(region);
        drawing.ObjectId.Should().NotBeNullOrWhiteSpace();
        drawing.Caption.Should().NotBeNullOrWhiteSpace();
        if (horizontalPosition.HasValue)
        {
            drawing.Layout.Position.HorizontalAlignment.Should().Be(horizontalPosition);
        }
    }

    private static string GetBlockText(DocumentEditorDocument document, string blockId)
    {
        var block = FindBlock(document.Blocks, blockId);
        block.Should().NotBeNull($"document should contain block {blockId}");
        return string.Concat(GetInlineText(block!.Content));
    }

    private static DocumentBlock? FindBlock(IEnumerable<DocumentBlock> blocks, string blockId)
    {
        foreach (var block in blocks)
        {
            if (string.Equals(block.Id, blockId, StringComparison.Ordinal))
            {
                return block;
            }

            if (block.Content is not TableBlockContent table)
            {
                continue;
            }

            var nested = FindBlock(table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks), blockId);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetInlineText(DocumentBlockContent? content)
        => content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines.Select(GetInlineText),
            HeadingBlockContent heading => heading.Inlines.Select(GetInlineText),
            ListBlockContent list => list.Inlines.Select(GetInlineText),
            QuoteBlockContent quote => quote.Inlines.Select(GetInlineText),
            _ => []
        };

    private static string GetInlineText(InlineContent inline)
        => inline switch
        {
            TextRun text => text.Text,
            TokenRun token => token.FallbackText ?? token.DisplayName,
            DocumentDrawingRun drawing => drawing.AltText ?? string.Empty,
            _ => string.Empty
        };

    private static XDocument ReadPackageXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        entry.Should().NotBeNull($"DOCX package should contain {path}");
        using var stream = entry!.Open();
        return XDocument.Load(stream);
    }
}
