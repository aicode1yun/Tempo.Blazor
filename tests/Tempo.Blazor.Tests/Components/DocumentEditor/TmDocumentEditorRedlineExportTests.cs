using System.Reflection;
using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// The editor's redline export handler must turn the compare result into a tracked-changes
/// document (DocumentRedlineBuilder) and ship it through the format provider as DOCX, then
/// trigger the browser download.
/// </summary>
public class TmDocumentEditorRedlineExportTests : LocalizationTestBase
{
    [Fact]
    public async Task ExportRedline_BuildsTrackedChangesDocumentAndExportsDocx()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-redline");
        var formatProvider = new CapturingFormatProvider();

        var cut = RenderDocumentEditor(parameters => parameters
            .Add(p => p.DocumentId, "doc-redline")
            .Add(p => p.Provider, provider)
            .Add(p => p.FormatProvider, formatProvider));
        cut.WaitForElement("[data-testid='document-canvas-engine-host']");

        var compareResult = CreateCompareResult();
        var handler = typeof(TmDocumentEditor).GetMethod("ExportRedlineDocxAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        handler.Should().NotBeNull("the editor must expose the redline export handler wired to the compare dialog");

        await cut.InvokeAsync(async () => await (Task)handler!.Invoke(cut.Instance, [compareResult])!);

        formatProvider.LastExportRequest.Should().NotBeNull();
        formatProvider.LastExportRequest!.Format.Should().Be(DocumentFormatProviderKind.Docx);
        formatProvider.LastExportRequest.Document.Revisions.Should().NotBeEmpty("the exported document must carry tracked changes");
        formatProvider.LastExportRequest.Document.Revisions
            .Should().Contain(revision => revision.Type == DocumentRevisionType.Insertion)
            .And.Contain(revision => revision.Type == DocumentRevisionType.Deletion);
        JSInterop.Invocations.Should().Contain(invocation => invocation.Identifier == "tmDocumentEditor.downloadFile");
    }

    private static DocumentCompareResult CreateCompareResult()
    {
        var baseDocument = Document("v1", "Cena je 100 Kč.");
        var compareDocument = Document("v2", "Cena je 200 Kč.");
        return new DocumentCompareResult
        {
            Success = true,
            BaseDocument = baseDocument,
            CompareDocument = compareDocument,
            Changes =
            [
                new DocumentCompareBlockChange
                {
                    Kind = DocumentCompareChangeKind.Changed,
                    BlockId = "b1",
                    OldText = "Cena je 100 Kč.",
                    NewText = "Cena je 200 Kč.",
                    TextDiff = new DocumentTextDiffResult
                    {
                        Segments =
                        [
                            new DocumentTextDiffSegment { Kind = DocumentTextDiffSegmentKind.Unchanged, Text = "Cena je " },
                            new DocumentTextDiffSegment { Kind = DocumentTextDiffSegmentKind.Removed, Text = "100" },
                            new DocumentTextDiffSegment { Kind = DocumentTextDiffSegmentKind.Added, Text = "200" },
                            new DocumentTextDiffSegment { Kind = DocumentTextDiffSegmentKind.Unchanged, Text = " Kč." },
                        ],
                    },
                },
            ],
            Summary = new DocumentCompareSummary { ChangedBlocks = 1 },
        };
    }

    private static DocumentEditorDocument Document(string suffix, string text)
    {
        var document = DocumentEditorDocument.Empty();
        document.DocumentId = $"doc-redline-{suffix}";
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "b1",
                Type = DocumentBlockType.Paragraph,
                Order = 1,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] },
            },
        ];
        return document;
    }

    private sealed class CapturingFormatProvider : IDocumentFormatProvider
    {
        public DocumentFormatExportProviderRequest? LastExportRequest { get; private set; }

        public Task<IReadOnlyList<DocumentFormatProviderCapability>> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DocumentFormatProviderCapability> capabilities =
            [
                new DocumentFormatProviderCapability
                {
                    Format = DocumentFormatProviderKind.Docx,
                    CanImport = true,
                    CanExport = true,
                    FileExtensions = [".docx"],
                },
            ];
            return Task.FromResult(capabilities);
        }

        public Task<DocumentFormatExportProviderResult> ExportAsync(
            DocumentFormatExportProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            LastExportRequest = request;
            return Task.FromResult(new DocumentFormatExportProviderResult
            {
                Content = [1, 2, 3, 4],
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                FileName = "redline.docx",
            });
        }

        public Task<DocumentFormatImportProviderResult> ImportAsync(
            DocumentFormatImportProviderRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
