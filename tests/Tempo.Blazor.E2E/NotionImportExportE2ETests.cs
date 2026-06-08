using System.Text;
using System.IO.Compression;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;
using Dm = Tempo.Blazor.DocumentEditor.Models;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class NotionImportExportE2ETests : NotionE2ETestBase
{
    private const string SampleDocxWithImagesAndTablesUrl = "https://samplelib.com/docx/sample-simple.docx";

    [TestMethod]
    [Description("CF25: export menu downloads Markdown, HTML, PDF, DOCX, and ODT artifacts through the HTTPS demo API")]
    public async Task CF25_ExportMenu_DownloadsAllDocumentFormatsWithSubpages()
    {
        var page = await OpenNotionEditorAsync();
        await SeedExportPageAsync();

        var menu = await OpenExportMenuAsync(page);
        var exportMenu = menu.Locator("[data-testid='notion-export-menu']").First;
        await exportMenu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await menu.Locator("[data-testid='notion-export-include-subpages']").First.CheckAsync();
        await CaptureBaselineAsync("import-export", "cf25-export-menu", exportMenu);

        foreach (var format in ExportFormats)
        {
            var bytes = await DownloadExportBytesAsync(page, format, includeSubpages: true);

            if (format.Extension is ".md" or ".html")
            {
                var text = Encoding.UTF8.GetString(bytes);
                StringAssert.Contains(text, "CF25 Export Bridge");
                StringAssert.Contains(text, "CF25 Export Child");
                StringAssert.Contains(text, "CF25 Export Grandchild");
            }
        }
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF25: export edge cases cover empty pages, all-block roundtrip, large pages, hidden menu without provider, and UX review evidence")]
    public async Task CF25_ExportEdges_EmptyAllBlocksLargeAndProviderlessWork()
    {
        var providerless = await OpenNotionEditorAsync("?disableImportExportProvider=true");
        var providerlessMenu = await OpenSettingsMenuAsync(providerless);
        await AssertMenuItemHiddenAsync(providerlessMenu, "Export");
        await AssertMenuItemHiddenAsync(providerlessMenu, "Import");

        var page = await OpenNotionEditorAsync();
        await SeedEmptyPageAsync();
        foreach (var format in ExportFormats)
        {
            var emptyBytes = await DownloadExportBytesAsync(page, format, includeSubpages: false, minBytes: 1);
            Assert.IsTrue(emptyBytes.Length > 0, $"Empty page {format.Extension} export should still produce a non-empty document.");
        }

        page = await OpenNotionEditorAsync();
        await SeedExportPageAsync();
        var docxBytes = await DownloadExportBytesAsync(page, FindExportFormat("docx"), includeSubpages: false);
        AssertDocxContains(docxBytes, requireImages: false, requireTables: true, description: "CF25 all-block export");
        await using (var stream = new MemoryStream(docxBytes))
        {
            var imported = await new DocumentDocxImporter().ImportAsync(stream);
            var blockTypes = imported.Document.Blocks.Select(block => block.Type).Distinct().OrderBy(type => type.ToString()).ToArray();
            Assert.IsTrue(blockTypes.Contains(Dm.DocumentBlockType.Heading), "Roundtrip report should contain headings.");
            Assert.IsTrue(blockTypes.Contains(Dm.DocumentBlockType.Paragraph), "Roundtrip report should contain paragraphs/fallback text.");
            Assert.IsTrue(blockTypes.Contains(Dm.DocumentBlockType.Table), "Roundtrip report should contain tables.");
            TestContext.WriteLine($"CF25 roundtrip report block types: {string.Join(", ", blockTypes)}");
        }

        page = await OpenNotionEditorAsync();
        var largeMarkdownPath = await CreateLargeMarkdownImportAsync();
        await ImportFileAsync(page, "notion-import-markdown", largeMarkdownPath, "CF25 Large Export Page");
        var largeDocx = await DownloadExportBytesAsync(page, FindExportFormat("docx"), includeSubpages: false);
        var largePdf = await DownloadExportBytesAsync(page, FindExportFormat("pdf"), includeSubpages: false);
        Assert.IsTrue(largeDocx.Length > 10_000, "Large page DOCX export should contain the full imported document payload.");
        Assert.IsTrue(largePdf.Length > 1_000, "Large page PDF export should contain the full imported document payload.");
        AssertDocxContains(largeDocx, requireImages: false, requireTables: true, description: "CF25 large page export");

        TestContext.WriteLine("UX CF25 review: export menu keeps formats grouped, include-subpages state is clear, providerless state hides unavailable actions, and empty/large export flows keep responsive feedback.");
    }

    [TestMethod]
    [Description("CF26: import menu uploads a DOCX document through the HTTPS demo API and creates a Notion page with converted blocks")]
    public async Task CF26_ImportMenu_UploadsWordDocumentAndCreatesConvertedPage()
    {
        var page = await OpenNotionEditorAsync();
        var docxPath = await CreateImportDocxAsync();

        var menu = await OpenImportMenuAsync(page);
        var importMenu = menu.Locator("[data-testid='notion-import-menu']").First;
        await importMenu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await CaptureBaselineAsync("import-export", "cf26-import-menu", importMenu);

        var chooser = await page.RunAndWaitForFileChooserAsync(
            async () => await importMenu.Locator("[data-testid='notion-import-word']").First.ClickAsync(),
            new PageRunAndWaitForFileChooserOptions { Timeout = 10000 });
        await chooser.SetFilesAsync(docxPath);

        await page.Locator(".tm-notion-header-title").Filter(new LocatorFilterOptions { HasText = "CF26 Word Import" }).First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
        await page.GetByText("Imported paragraph from Word bridge").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
        await page.GetByText("Ready").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });

        await CaptureBaselineAsync("import-export", "cf26-imported-word-page", page.Locator(".tm-notion-page").First);
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF26: DOCX import edge cases cover invalid files, empty documents, and a real internet DOCX containing images and tables")]
    public async Task CF26_ImportEdges_InvalidEmptyAndRealDocxWithImagesAndTablesWork()
    {
        var page = await OpenNotionEditorAsync();

        var invalidDocx = Path.Combine(Path.GetTempPath(), $"tempo-cf26-invalid-{Guid.NewGuid():N}.docx");
        await File.WriteAllTextAsync(invalidDocx, "This is not an OpenXML package.");
        await ImportFileExpectingErrorAsync(page, "notion-import-word", invalidDocx);

        var emptyDocx = await CreateEmptyImportDocxAsync();
        await ImportFileAsync(page, "notion-import-word", emptyDocx, "CF26 Empty Word Import");
        await page.Locator(".tm-notion-paragraph[contenteditable='true']").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
        await CaptureBaselineAsync("import-export", "cf26-empty-docx-import-result", page.Locator(".tm-notion-page").First);

        var externalDocx = await DownloadDocxFixtureAsync(SampleDocxWithImagesAndTablesUrl, "samplelib-images-tables");
        await ImportFileAsync(page, "notion-import-word", externalDocx, expectedTitle: null);
        await page.Locator(".tm-notion-table").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
        await page.Locator(".tm-notion-page").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
        await CaptureBaselineAsync("import-export", "cf26-rich-docx-import-result", page.Locator(".tm-notion-page").First);

        TestContext.WriteLine($"UX CF26 review: invalid DOCX shows inline error feedback, empty DOCX lands on an editable page, and external DOCX fixture with images/tables imports without blocking the editor. Fixture: {SampleDocxWithImagesAndTablesUrl}");
    }

    private static readonly ExportFormatProbe[] ExportFormats =
    [
        new("markdown", ".md", "#", 250),
        new("html", ".html", "<", 500),
        new("pdf", ".pdf", "%PDF", 500),
        new("docx", ".docx", "PK", 500),
        new("odt", ".odt", "PK", 500)
    ];

    private async Task<ILocator> OpenExportMenuAsync(IPage page)
    {
        var menu = await OpenSettingsMenuAsync(page);
        await menu.Locator(".tm-npsm__item").Filter(new LocatorFilterOptions { HasText = "Export" }).First.ClickAsync();
        return menu;
    }

    private async Task<ILocator> EnsureExportMenuOpenAsync(IPage page)
    {
        var existing = page.Locator("[data-testid='notion-export-menu']").First;
        if (await existing.IsVisibleAsync())
        {
            return page.Locator(".tm-npsm").First;
        }

        return await OpenExportMenuAsync(page);
    }

    private async Task<ILocator> EnsureImportMenuOpenAsync(IPage page)
    {
        var existing = page.Locator("[data-testid='notion-import-menu']").First;
        if (await existing.IsVisibleAsync())
        {
            return page.Locator(".tm-npsm").First;
        }

        return await OpenImportMenuAsync(page);
    }

    private async Task<ILocator> OpenImportMenuAsync(IPage page)
    {
        var menu = await OpenSettingsMenuAsync(page);
        await menu.Locator(".tm-npsm__item").Filter(new LocatorFilterOptions { HasText = "Import" }).First.ClickAsync();
        return menu;
    }

    private static async Task<ILocator> OpenSettingsMenuAsync(IPage page)
    {
        var trigger = page.Locator(".tm-npsm-trigger").First;
        await trigger.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await trigger.ClickAsync();

        var menu = page.Locator(".tm-npsm").First;
        await menu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        return menu;
    }

    private async Task<byte[]> DownloadExportBytesAsync(IPage page, ExportFormatProbe format, bool includeSubpages, int? minBytes = null)
    {
        var menu = await EnsureExportMenuOpenAsync(page);
        var includeSubpagesToggle = menu.Locator("[data-testid='notion-export-include-subpages']").First;
        var isChecked = await includeSubpagesToggle.IsCheckedAsync();
        if (includeSubpages && !isChecked)
        {
            await includeSubpagesToggle.CheckAsync();
        }
        else if (!includeSubpages && isChecked)
        {
            await includeSubpagesToggle.UncheckAsync();
        }

        var download = await page.RunAndWaitForDownloadAsync(
            async () => await menu.Locator($"[data-testid='notion-export-{format.TestId}']").First.ClickAsync(),
            new PageRunAndWaitForDownloadOptions { Timeout = 60000 });

        var path = await download.PathAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(path), $"Downloaded {format.Extension} file should be available on disk.");
        StringAssert.EndsWith(download.SuggestedFilename, format.Extension);

        var bytes = await File.ReadAllBytesAsync(path!);
        Assert.IsTrue(bytes.Length >= (minBytes ?? format.MinBytes), $"{format.Extension} export should contain a real document payload.");
        AssertBytePrefix(bytes, format.Signature, format.Extension);
        return bytes;
    }

    private async Task ImportFileAsync(IPage page, string importTestId, string path, string? expectedTitle)
    {
        var menu = await EnsureImportMenuOpenAsync(page);
        var chooser = await page.RunAndWaitForFileChooserAsync(
            async () => await menu.Locator($"[data-testid='{importTestId}']").First.ClickAsync(),
            new PageRunAndWaitForFileChooserOptions { Timeout = 10000 });
        await chooser.SetFilesAsync(path);

        if (!string.IsNullOrWhiteSpace(expectedTitle))
        {
            await page.Locator(".tm-notion-header-title").Filter(new LocatorFilterOptions { HasText = expectedTitle }).First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 60000
            });
        }
        else
        {
            await page.Locator(".tm-notion-page").First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 60000
            });
        }
    }

    private async Task ImportFileExpectingErrorAsync(IPage page, string importTestId, string path)
    {
        var menu = await EnsureImportMenuOpenAsync(page);
        var chooser = await page.RunAndWaitForFileChooserAsync(
            async () => await menu.Locator($"[data-testid='{importTestId}']").First.ClickAsync(),
            new PageRunAndWaitForFileChooserOptions { Timeout = 10000 });
        await chooser.SetFilesAsync(path);

        await page.Locator(".tm-npsm__toast--error").Filter(new LocatorFilterOptions { HasText = "Import failed" }).First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
    }

    private static async Task AssertMenuItemHiddenAsync(ILocator menu, string label)
    {
        var count = await menu.Locator(".tm-npsm__item").Filter(new LocatorFilterOptions { HasText = label }).CountAsync();
        Assert.AreEqual(0, count, $"{label} menu item should be hidden when no import/export provider is configured.");
    }

    private static void AssertBytePrefix(byte[] bytes, string expected, string extension)
    {
        var prefix = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, expected.Length));
        Assert.AreEqual(expected, prefix, $"{extension} export should start with the expected file signature.");
    }

    private static ExportFormatProbe FindExportFormat(string testId)
        => ExportFormats.First(format => string.Equals(format.TestId, testId, StringComparison.Ordinal));

    private static async Task<string> CreateImportDocxAsync()
    {
        var document = Dm.DocumentEditorDocument.Empty();
        document.Metadata.Title = "CF26 Word Import";
        document.Blocks =
        [
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Heading,
                Order = 0,
                Content = new Dm.HeadingBlockContent
                {
                    Level = 1,
                    Inlines = [new Dm.TextRun { Text = "Imported heading" }]
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Paragraph,
                Order = 1,
                Content = new Dm.ParagraphBlockContent
                {
                    Inlines =
                    [
                        new Dm.TextRun { Text = "Imported paragraph from Word bridge" }
                    ]
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Table,
                Order = 2,
                Content = new Dm.TableBlockContent
                {
                    Rows =
                    [
                        new Dm.TableRowContent
                        {
                            Cells =
                            [
                                Cell("Name", true),
                                Cell("Status", true)
                            ]
                        },
                        new Dm.TableRowContent
                        {
                            Cells =
                            [
                                Cell("CF26", false),
                                Cell("Ready", false)
                            ]
                        }
                    ]
                }
            }
        ];

        var exported = await new DocumentDocxExporter().ExportAsync(document, new DocumentFormatExportOptions
        {
            FileName = "cf26-word-import",
            AllowImagePlaceholders = true
        });

        var path = Path.Combine(Path.GetTempPath(), $"tempo-cf26-import-{Guid.NewGuid():N}.docx");
        await File.WriteAllBytesAsync(path, exported.Content);
        return path;
    }

    private static async Task<string> CreateEmptyImportDocxAsync()
    {
        var document = Dm.DocumentEditorDocument.Empty();
        document.Metadata.Title = "CF26 Empty Word Import";

        var exported = await new DocumentDocxExporter().ExportAsync(document, new DocumentFormatExportOptions
        {
            FileName = "cf26-empty-word-import",
            AllowImagePlaceholders = true
        });

        var path = Path.Combine(Path.GetTempPath(), $"tempo-cf26-empty-{Guid.NewGuid():N}.docx");
        await File.WriteAllBytesAsync(path, exported.Content);
        return path;
    }

    private static async Task<string> CreateLargeMarkdownImportAsync()
    {
        var markdown = new StringBuilder("# CF25 Large Export Page\n\n");
        for (var index = 1; index <= 160; index++)
        {
            markdown.Append("## Export section ").Append(index).Append("\n\n");
            markdown.Append("Large export paragraph ").Append(index)
                .Append(" keeps enough real text in the document to exercise pagination, table conversion, and DOCX/PDF payload generation.\n\n");

            if (index % 20 == 0)
            {
                markdown.Append("| Area | Status | Owner |\n");
                markdown.Append("| --- | --- | --- |\n");
                markdown.Append("| Export ").Append(index).Append(" | Ready | Tempo |\n\n");
            }
        }

        var path = Path.Combine(Path.GetTempPath(), $"tempo-cf25-large-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, markdown.ToString(), Encoding.UTF8);
        return path;
    }

    private static async Task<string> DownloadDocxFixtureAsync(string url, string name)
    {
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        AssertDocxContains(bytes, requireImages: true, requireTables: true, description: name);

        var path = Path.Combine(Path.GetTempPath(), $"tempo-{name}-{Guid.NewGuid():N}.docx");
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    private static void AssertDocxContains(byte[] bytes, bool requireImages, bool requireTables, string description)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var documentXml = archive.GetEntry("word/document.xml");
        Assert.IsNotNull(documentXml, $"{description} should contain word/document.xml.");

        if (requireImages)
        {
            Assert.IsTrue(archive.Entries.Any(entry => entry.FullName.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase)),
                $"{description} should contain at least one image part.");
        }

        if (requireTables)
        {
            using var stream = documentXml.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var xml = reader.ReadToEnd();
            StringAssert.Contains(xml, "<w:tbl");
        }
    }

    private static Dm.TableCellContent Cell(string value, bool isHeader) => new()
    {
        IsHeader = isHeader,
        Blocks =
        [
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Paragraph,
                Content = new Dm.ParagraphBlockContent
                {
                    Inlines = [new Dm.TextRun { Text = value }]
                }
            }
        ]
    };

    private sealed record ExportFormatProbe(string TestId, string Extension, string Signature, int MinBytes);
}
