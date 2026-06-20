using System.Text;
using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor.CanvasEngine.Export;

public sealed class CanvasExportTests
{
    [Fact]
    public async Task ExportFormatAsync_RequestsCurrentCanvasSnapshotForProviderExport()
    {
        var snapshot = CreateDocument("Initial canvas text");
        var bridge = new CanvasExportBridge(_ => Task.FromResult(snapshot));
        var provider = new CapturingFormatProvider();
        snapshot = CreateDocument("Unsaved canvas export text");

        var result = await bridge.ExportFormatAsync(provider, DocumentFormatProviderKind.Docx, CreateAuthor());

        result.Success.Should().BeTrue();
        provider.LastExportRequest.Should().NotBeNull();
        provider.LastExportRequest!.DocumentId.Should().Be("canvas-phase19");
        provider.LastExportRequest.Format.Should().Be(DocumentFormatProviderKind.Docx);
        provider.LastExportRequest.FileName.Should().Be("Canvas Phase 19");
        GetParagraphText(provider.LastExportRequest.Document).Should().Be("Unsaved canvas export text");
    }

    [Fact]
    public async Task ExportPdfAsync_UsesCurrentCanvasSnapshotAndOptions()
    {
        var snapshot = CreateDocument("Current canvas PDF text");
        var bridge = new CanvasExportBridge(_ => Task.FromResult(snapshot));
        var provider = new CapturingPdfProvider();

        var result = await bridge.ExportPdfAsync(
            provider,
            CreateAuthor(),
            document => new DocumentPdfExportOptions
            {
                IncludeComments = document.Comments.Count > 0,
                ReviewDisplayMode = DocumentReviewDisplayMode.NoMarkup
            });

        result.Content.Should().NotBeEmpty();
        provider.LastRequest.Should().NotBeNull();
        GetParagraphText(provider.LastRequest!.Document).Should().Be("Current canvas PDF text");
        provider.LastRequest.Options.IncludeComments.Should().BeTrue();
        provider.LastRequest.Options.ReviewDisplayMode.Should().Be(DocumentReviewDisplayMode.NoMarkup);
    }

    [Fact]
    public async Task CreateCurrentCompareSourceAsync_EmbedsCurrentDocumentSnapshotAndJson()
    {
        var snapshot = CreateDocument("Canvas compare source text");
        var bridge = new CanvasExportBridge(_ => Task.FromResult(snapshot));

        var source = await bridge.CreateCurrentCompareSourceAsync("Current");

        source.Kind.Should().Be(DocumentCompareSourceKind.Current);
        source.DocumentId.Should().Be("canvas-phase19");
        source.Document.Should().NotBeNull();
        GetParagraphText(source.Document!).Should().Be("Canvas compare source text");
        source.JsonSnapshot.Should().Contain("Canvas compare source text");
        source.Label.Should().Be("Current");
    }

    [Fact]
    public async Task BuildDebugJsonAsync_IncludesCurrentCanvasSnapshotAndRuntimeDebug()
    {
        var snapshot = CreateDocument("Canvas debug text");
        var bridge = new CanvasExportBridge(_ => Task.FromResult(snapshot));

        var json = await bridge.BuildDebugJsonAsync(
            _ => Task.FromResult<string?>("{\"model\":{\"documentId\":\"canvas-phase19\"},\"modelVersion\":7}"),
            new[] { new { objectId = "drawing-1" } },
            new { recovered = true });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("canonicalDocument").GetProperty("DocumentId").GetString().Should().Be("canvas-phase19");
        root.GetProperty("canonicalDocument").GetProperty("Blocks")[0].GetRawText().Should().Contain("Canvas debug text");
        root.GetProperty("runtimeDebug").GetProperty("modelVersion").GetInt32().Should().Be(7);
        root.GetProperty("docxDrawingMetadata")[0].GetProperty("objectId").GetString().Should().Be("drawing-1");
        root.GetProperty("runtimeRecovery").GetProperty("recovered").GetBoolean().Should().BeTrue();
    }

    private static DocumentEditorDocument CreateDocument(string text)
    {
        var document = DocumentEditorDocument.Empty("canvas-phase19");
        document.Metadata.Title = "Canvas Phase 19";
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "paragraph-1",
                Type = DocumentBlockType.Paragraph,
                Order = 0,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Text = text }]
                }
            }
        ];
        document.Comments =
        [
            new DocumentComment
            {
                Id = "comment-1",
                Anchor = new DocumentCommentAnchor { BlockId = "paragraph-1" },
                Entries =
                [
                    new DocumentCommentEntry
                    {
                        Author = CreateAuthor(),
                        Text = "Canvas comment",
                        CreatedAt = DateTimeOffset.Parse("2026-06-05T10:00:00+02:00")
                    }
                ]
            }
        ];
        return document;
    }

    private static string GetParagraphText(DocumentEditorDocument document)
        => string.Concat(((ParagraphBlockContent)document.Blocks.Single().Content)
            .Inlines
            .OfType<TextRun>()
            .Select(run => run.Text));

    private static DocumentEditorAuthor CreateAuthor()
        => new()
        {
            Id = "phase19-author",
            DisplayName = "Phase 19 Author"
        };

    private sealed class CapturingFormatProvider : IDocumentFormatProvider
    {
        public DocumentFormatExportProviderRequest? LastExportRequest { get; private set; }

        public Task<IReadOnlyList<DocumentFormatProviderCapability>> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentFormatProviderCapability>>(
            [
                new()
                {
                    Format = DocumentFormatProviderKind.Docx,
                    CanImport = true,
                    CanExport = true,
                    FileExtensions = [".docx"],
                    ContentTypes = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"]
                }
            ]);

        public Task<DocumentFormatImportProviderResult> ImportAsync(DocumentFormatImportProviderRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentFormatImportProviderResult
            {
                Success = true,
                Format = request.Format,
                Document = CreateDocument(Encoding.UTF8.GetString(request.Content))
            });

        public Task<DocumentFormatExportProviderResult> ExportAsync(DocumentFormatExportProviderRequest request, CancellationToken cancellationToken = default)
        {
            LastExportRequest = request;
            return Task.FromResult(new DocumentFormatExportProviderResult
            {
                Success = true,
                Format = request.Format,
                Content = Encoding.UTF8.GetBytes(GetParagraphText(request.Document)),
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                FileName = $"{request.FileName}.docx"
            });
        }
    }

    private sealed class CapturingPdfProvider : IDocumentPdfExportProvider
    {
        public DocumentPdfExportRequest? LastRequest { get; private set; }

        public Task<DocumentPdfExportResult> ExportPdfAsync(DocumentPdfExportRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new DocumentPdfExportResult
            {
                Content = Encoding.ASCII.GetBytes("%PDF-1.7\n" + GetParagraphText(request.Document)),
                FileName = $"{request.FileName}.pdf"
            });
        }
    }
}
