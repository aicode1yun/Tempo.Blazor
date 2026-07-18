using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// The additive PdfForensicWatermark parameter must flow into the PDF export request options,
/// with the user name defaulting to the editor Author when the host leaves it empty.
/// </summary>
public class TmDocumentEditorForensicPdfTests : LocalizationTestBase
{
    [Fact]
    public async Task PdfExport_CarriesForensicWatermarkOptions_WithAuthorFallback()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-forensic");
        var pdfProvider = new CapturingPdfProvider();

        var cut = RenderDocumentEditor(parameters => parameters
            .Add(p => p.DocumentId, "doc-forensic")
            .Add(p => p.Provider, provider)
            .Add(p => p.PdfExportProvider, pdfProvider)
            .Add(p => p.Author, new DocumentEditorAuthor { Id = "u1", DisplayName = "Jana Malá" })
            .Add(p => p.PdfForensicWatermark, new DocumentPdfForensicWatermarkOptions { Opacity = 0.2 }));

        cut.WaitForElement("[data-testid='document-canvas-engine-host']");
        cut.Find("[data-testid='document-ribbon-tab-references']").Click();
        cut.Find("[data-testid='document-export-pdf']").Click();

        cut.WaitForAssertion(() => pdfProvider.LastRequest.Should().NotBeNull());
        var forensic = pdfProvider.LastRequest!.Options.ForensicWatermark;
        forensic.Should().NotBeNull("the parameter must flow into the export options");
        forensic!.Opacity.Should().Be(0.2);
        forensic.UserName.Should().Be("Jana Malá", "empty user name defaults to the editor author");
    }

    [Fact]
    public async Task PdfExport_WithoutParameter_LeavesForensicWatermarkNull()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-forensic");
        var pdfProvider = new CapturingPdfProvider();

        var cut = RenderDocumentEditor(parameters => parameters
            .Add(p => p.DocumentId, "doc-forensic")
            .Add(p => p.Provider, provider)
            .Add(p => p.PdfExportProvider, pdfProvider));

        cut.WaitForElement("[data-testid='document-canvas-engine-host']");
        cut.Find("[data-testid='document-ribbon-tab-references']").Click();
        cut.Find("[data-testid='document-export-pdf']").Click();

        cut.WaitForAssertion(() => pdfProvider.LastRequest.Should().NotBeNull());
        pdfProvider.LastRequest!.Options.ForensicWatermark.Should().BeNull();
    }

    private sealed class CapturingPdfProvider : IDocumentPdfExportProvider
    {
        public DocumentPdfExportRequest? LastRequest { get; private set; }

        public Task<DocumentPdfExportResult> ExportPdfAsync(
            DocumentPdfExportRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new DocumentPdfExportResult
            {
                Content = [1, 2, 3],
                ContentType = "application/pdf",
                FileName = "forensic.pdf",
            });
        }
    }
}
