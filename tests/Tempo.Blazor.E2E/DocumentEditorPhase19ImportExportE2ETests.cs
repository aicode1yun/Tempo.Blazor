using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 19 persistence and import/export boundaries.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase19ImportExportE2ETests : DocumentEditorE2ETestBase
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(DocumentEditorJson.Options)
    {
        PropertyNameCaseInsensitive = true
    };

    [TestMethod]
    public async Task Phase19_SaveReload_ImageAndTablePropertiesPersist()
    {
        using var http = CreateApiClient();
        var backup = await LoadDocumentAsync(http, "contract-demo");
        var phaseDocument = CreatePhase19Document();

        try
        {
            await SaveDocumentAsync(http, phaseDocument);
            var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

            await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] img[alt='Phase 19 image']").First)
                .ToBeVisibleAsync(new() { Timeout = 10000 });
            await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] table").Filter(new() { HasText = "Phase 19 cell" }).First)
                .ToBeVisibleAsync(new() { Timeout = 10000 });

            await page.Locator("[data-testid='document-save']").ClickAsync();
            await Assertions.Expect(page.Locator("[data-testid='document-save-message']")).ToContainTextAsync("Saved", new() { Timeout = 10000 });
            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
            await WaitForDocumentEditorReadyAsync(page);

            await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] img[alt='Phase 19 image']").First)
                .ToBeVisibleAsync(new() { Timeout = 10000 });
            await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] table").Filter(new() { HasText = "Phase 19 cell" }).First)
                .ToBeVisibleAsync(new() { Timeout = 10000 });

            var reloaded = await LoadDocumentAsync(http, "contract-demo");
            reloaded.Should().NotBeNull();
            var image = reloaded!.Blocks.Select(block => block.Content).OfType<ImageBlockContent>().Single();
            image.Caption.Should().Be("Phase 19 caption");
            image.Size.Width.Should().Be(240);
            image.FloatingLayout!.WrapMode.Should().Be(DocumentWrapMode.Square);
            var table = reloaded.Blocks.Select(block => block.Content).OfType<TableBlockContent>().Single();
            table.Layout.Width.Should().Be(360);
            table.Rows[0].Cells[0].BackgroundColor.Should().Be("#FFEEAA");
        }
        finally
        {
            if (backup is not null)
            {
                await SaveDocumentAsync(http, backup);
            }
        }
    }

    [TestMethod]
    public async Task Phase19_ExportDocxImportDocxAndExportPdfSmoke()
    {
        using var http = CreateApiClient();
        var backup = await LoadDocumentAsync(http, "contract-demo");
        var phaseDocument = CreatePhase19Document();

        try
        {
            await SaveDocumentAsync(http, phaseDocument);

            var docx = await http.GetByteArrayAsync("/api/document-editor/contract-demo/export/docx");
            docx.Length.Should().BeGreaterThan(1000);
            var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(docx));
            imported.Document.Blocks.Select(block => block.Content).OfType<ImageBlockContent>().Single().Caption.Should().Be("Phase 19 caption");
            var importedTable = imported.Document.Blocks.Select(block => block.Content).OfType<TableBlockContent>().Single();
            importedTable.Layout.Width.Should().BeApproximately(360, 0.1);
            importedTable.Rows[0].Cells[0].BackgroundColor.Should().Be("#FFEEAA");

            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(docx)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document") }
            }, "file", "phase19.docx");
            var importResponse = await http.PostAsync("/api/document-editor/import/docx", form);
            importResponse.EnsureSuccessStatusCode();

            var pdf = await http.GetByteArrayAsync("/api/document-editor/contract-demo/export/pdf");
            Encoding.ASCII.GetString(pdf, 0, Math.Min(pdf.Length, 8)).Should().StartWith("%PDF");
            Encoding.ASCII.GetString(pdf).Should().Contain("Phase 19 export text");
        }
        finally
        {
            if (backup is not null)
            {
                await SaveDocumentAsync(http, backup);
            }
        }
    }

    private static HttpClient CreateApiClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        return new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5100") };
    }

    private static async Task<DocumentEditorDocument?> LoadDocumentAsync(HttpClient http, string documentId)
    {
        var result = await http.GetFromJsonAsync<DocumentEditorLoadResult>(
            $"/api/document-editor/{documentId}",
            ApiJsonOptions);
        return result?.Document;
    }

    private static async Task SaveDocumentAsync(HttpClient http, DocumentEditorDocument document)
    {
        var response = await http.PutAsJsonAsync(
            $"/api/document-editor/{document.DocumentId}",
            new DocumentEditorSaveRequest
            {
                DocumentId = document.DocumentId,
                Document = document,
                ConcurrencyMode = DocumentEditorConcurrencyMode.Force
            },
            ApiJsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static DocumentEditorDocument CreatePhase19Document()
    {
        var document = DocumentEditorDocument.Empty("contract-demo");
        document.Metadata.Title = "Phase 19 export text";
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "phase19-paragraph",
                Type = DocumentBlockType.Paragraph,
                Order = 0,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Phase 19 export text" }] }
            },
            new DocumentBlock
            {
                Id = "phase19-image",
                Type = DocumentBlockType.Image,
                Order = 1,
                Content = new ImageBlockContent
                {
                    Source = DocumentImageSource.Url,
                    Url = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=",
                    AltText = "Phase 19 image",
                    Caption = "Phase 19 caption",
                    LinkUrl = "https://example.test/phase19",
                    Size = new DocumentImageSize { Width = 240, Height = 120 },
                    FloatingLayout = new DocumentFloatingLayout
                    {
                        Inline = false,
                        WrapMode = DocumentWrapMode.Square,
                        HorizontalPosition = DocumentImageHorizontalPosition.Right,
                        DistanceLeft = 12
                    }
                }
            },
            new DocumentBlock
            {
                Id = "phase19-table",
                Type = DocumentBlockType.Table,
                Order = 2,
                Content = new TableBlockContent
                {
                    Layout = new TableLayoutContent
                    {
                        Width = 360,
                        Alignment = TableHorizontalAlignment.Center,
                        CellPadding = 8
                    },
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent
                                {
                                    Id = "phase19-cell",
                                    Width = 120,
                                    BackgroundColor = "#FFEEAA",
                                    VerticalAlignment = TableCellVerticalAlignment.Middle,
                                    Blocks =
                                    [
                                        new DocumentBlock
                                        {
                                            Type = DocumentBlockType.Paragraph,
                                            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Phase 19 cell" }] }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
        ];
        return document;
    }
}
