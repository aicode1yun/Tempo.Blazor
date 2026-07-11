using System.Text;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.DocumentFormats.Markdown;
using Dm = Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for markdown/HTML table import and export in the Notion editor.
/// Runs against the self-hosted HTTPS demo API (5100) and HTTPS demo WASM (7106) started by PlaywrightTestBase.
/// </summary>
[TestClass]
public class NotionTableImportExportE2ETests : NotionE2ETestBase
{
    private const string OuterPipeTable = """
        # Table With Outer Pipes

        | Name | Status | Owner |
        | --- | --- | --- |
        | CF26 | Ready | Pavel |
        | CF27 | Draft | Jana |
        """;

    private const string NoOuterPipeAlignedTable = """
        # Table Without Outer Pipes

        Plain | Left | Center | Right
        --- | :--- | :---: | ---:
        a | b | c | d
        e | f | g | h
        """;

    private const string EdgeCaseTables = """
        # Edge Cases

        | Expression | Meaning |
        | --- | --- |
        | a \| b | union |
        | c |  |

        | Single |
        | :---: |
        | only |

        | HeaderOnly | NoBody |
        | ---: | :--- |
        """;

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Markdown table with outer pipes imports as a real Table block with correct rows and columns")]
    public async Task TableImport_WithOuterPipes_RendersTableBlock()
    {
        var page = await OpenNotionEditorAsync();
        await ImportMarkdownAsync(page, OuterPipeTable, "Table With Outer Pipes");

        var table = await WaitForTableAsync(page);

        // Header row + 2 body rows. The control row lives in <thead>, data rows in <tbody>.
        var bodyRows = table.Locator("tbody tr.tm-notion-table-row");
        Assert.AreEqual(3, await bodyRows.CountAsync(), "Imported table should have a header row plus two body rows.");

        var firstRowCells = bodyRows.Nth(0).Locator("td.tm-notion-table__cell-td");
        Assert.AreEqual(3, await firstRowCells.CountAsync(), "Imported table should have three columns.");

        await AssertCellTextAsync(bodyRows.Nth(0), 0, "Name");
        await AssertCellTextAsync(bodyRows.Nth(0), 2, "Owner");
        await AssertCellTextAsync(bodyRows.Nth(2), 1, "Draft");

        Assert.IsTrue(
            await table.Locator(".tm-notion-table--has-header-row").CountAsync() > 0
                || (await table.GetAttributeAsync("class"))!.Contains("has-header-row"),
            "Imported table should keep the header-row flag.");

        await CaptureBaselineAsync("table-import-export", "outer-pipes-table");
        TestContext.WriteLine("UX: table renders with the standard Notion block chrome — bordered grid, header row shaded via --tm-color-bg-subtle, drag handles on hover.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Markdown table without outer pipes imports correctly and per-column alignment reaches the rendered cells")]
    public async Task TableImport_WithoutOuterPipesAndWithAlignment_RendersAlignedTable()
    {
        var page = await OpenNotionEditorAsync();
        await ImportMarkdownAsync(page, NoOuterPipeAlignedTable, "Table Without Outer Pipes");

        var table = await WaitForTableAsync(page);
        var bodyRows = table.Locator("tbody tr.tm-notion-table-row");
        Assert.AreEqual(3, await bodyRows.CountAsync(), "Pipe-less table should import a header row plus two body rows.");

        var dataRow = bodyRows.Nth(1);
        Assert.AreEqual(4, await dataRow.Locator("td.tm-notion-table__cell-td").CountAsync(), "Pipe-less table should have four columns.");

        // `---` leaves the renderer default; `:---`, `:---:` and `---:` must be applied.
        await AssertComputedAlignmentAsync(dataRow, 1, "left");
        await AssertComputedAlignmentAsync(dataRow, 2, "center");
        await AssertComputedAlignmentAsync(dataRow, 3, "right");

        await CaptureBaselineAsync("table-import-export", "aligned-table-light");

        await SetThemeAsync(page, dark: true);
        await CaptureBaselineAsync("table-import-export", "aligned-table-dark");
        await SetThemeAsync(page, dark: false);

        TestContext.WriteLine("UX: alignment is visible — column 2 flush left, column 3 centred, column 4 flush right; column 1 keeps the default. Dark mode inherits --tm-* tokens, so borders and header shading stay consistent with neighbouring blocks; text-align is geometric and needs no theme variant.");
    }

    [TestMethod]
    [Description("Exporting a page with an aligned table produces valid GFM and re-imports to an identical structure")]
    public async Task TableExport_ProducesGfmWithAlignment_AndRoundTrips()
    {
        var page = await OpenNotionEditorAsync();
        await ImportMarkdownAsync(page, NoOuterPipeAlignedTable, "Table Without Outer Pipes");
        await WaitForTableAsync(page);

        var markdown = Encoding.UTF8.GetString(await DownloadMarkdownAsync(page));
        TestContext.WriteLine($"Exported markdown:{Environment.NewLine}{markdown}");

        StringAssert.Contains(markdown, "| Plain | Left | Center | Right |");
        StringAssert.Contains(markdown, "| --- | :--- | :---: | ---: |");
        StringAssert.Contains(markdown, "| a | b | c | d |");
        StringAssert.Contains(markdown, "| e | f | g | h |");

        // Round-trip: re-importing the exported markdown must yield the same table structure.
        var original = ImportTable(NoOuterPipeAlignedTable);
        var roundTripped = ImportTable(markdown);

        CollectionAssert.AreEqual(
            original.ColumnAlignments,
            roundTripped.ColumnAlignments.ToList(),
            "Column alignments must survive the export/import round-trip.");
        Assert.AreEqual(original.Rows.Count, roundTripped.Rows.Count, "Row count must survive the round-trip.");
        for (var row = 0; row < original.Rows.Count; row++)
        {
            Assert.AreEqual(
                original.Rows[row].Cells.Count,
                roundTripped.Rows[row].Cells.Count,
                $"Row {row} must keep its cell count.");
            for (var column = 0; column < original.Rows[row].Cells.Count; column++)
            {
                Assert.AreEqual(
                    CellText(original, row, column),
                    CellText(roundTripped, row, column),
                    $"Cell [{row},{column}] must survive the round-trip.");
            }
        }
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Edge cases: empty cells, escaped pipes, single-column, header-only, and HTML <table> import")]
    public async Task TableImport_EdgeCases_RenderAndExportCorrectly()
    {
        var page = await OpenNotionEditorAsync();
        await ImportMarkdownAsync(page, EdgeCaseTables, "Edge Cases");

        await WaitForTableAsync(page);
        var tables = page.Locator(".tm-notion-table-block");
        Assert.AreEqual(3, await tables.CountAsync(), "Three separate tables should be imported, not merged into one.");

        // Escaped pipe stays cell content, empty cell survives.
        var firstTableRows = tables.Nth(0).Locator("tbody tr.tm-notion-table-row");
        await AssertCellTextAsync(firstTableRows.Nth(1), 0, "a | b");
        await AssertCellTextAsync(firstTableRows.Nth(2), 1, "");

        // Single-column table.
        var singleRows = tables.Nth(1).Locator("tbody tr.tm-notion-table-row");
        Assert.AreEqual(1, await singleRows.Nth(0).Locator("td.tm-notion-table__cell-td").CountAsync(), "Single-column table should render exactly one column.");
        await AssertComputedAlignmentAsync(singleRows.Nth(0), 0, "center");

        // Header-only table still renders as a table.
        var headerOnlyRows = tables.Nth(2).Locator("tbody tr.tm-notion-table-row");
        Assert.AreEqual(1, await headerOnlyRows.CountAsync(), "Header-only table should render its single header row.");
        await AssertComputedAlignmentAsync(headerOnlyRows.Nth(0), 0, "right");

        await CaptureBaselineAsync("table-import-export", "edge-case-tables");

        var markdown = Encoding.UTF8.GetString(await DownloadMarkdownAsync(page));
        TestContext.WriteLine($"Edge-case export:{Environment.NewLine}{markdown}");
        StringAssert.Contains(markdown, @"| a \| b | union |");
        StringAssert.Contains(markdown, "| :---: |");
        StringAssert.Contains(markdown, "| ---: | :--- |");

        TestContext.WriteLine("UX: empty cells keep full row height via the zero-width-space placeholder, so the grid never collapses; the escaped pipe reads as literal text; single-column and header-only tables keep the same border treatment as multi-column tables.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("HTML <table> import produces a Table block with header row and child rows")]
    public async Task TableImport_FromHtml_RendersTableBlock()
    {
        const string html = """
            <h1>HTML Table Import</h1>
            <table>
              <thead><tr><th>Name</th><th>Status</th></tr></thead>
              <tbody>
                <tr><td>CF26</td><td>Ready</td></tr>
                <tr><td>CF27</td><td>Draft</td></tr>
              </tbody>
            </table>
            """;

        var page = await OpenNotionEditorAsync();
        await ImportFileAsync(page, "notion-import-html", await WriteTempFileAsync(html, ".html"), "HTML Table Import");

        var table = await WaitForTableAsync(page);
        var bodyRows = table.Locator("tbody tr.tm-notion-table-row");
        Assert.AreEqual(3, await bodyRows.CountAsync(), "HTML table should import a header row plus two body rows.");
        Assert.AreEqual(2, await bodyRows.Nth(0).Locator("td.tm-notion-table__cell-td").CountAsync(), "HTML table should have two columns.");
        await AssertCellTextAsync(bodyRows.Nth(2), 0, "CF27");

        await CaptureBaselineAsync("table-import-export", "html-table");
        TestContext.WriteLine("UX: an HTML-sourced table is visually indistinguishable from a markdown-sourced one, which is the intended parity.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<ILocator> WaitForTableAsync(IPage page)
    {
        var table = page.Locator(".tm-notion-table").First;
        await table.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });

        // Rows load asynchronously through GetChildBlocksAsync.
        await page.Locator("tbody tr.tm-notion-table-row").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30000
        });

        return table;
    }

    private static async Task AssertCellTextAsync(ILocator row, int columnIndex, string expected)
    {
        var cell = row.Locator("td.tm-notion-table__cell-td").Nth(columnIndex).Locator(".tm-notion-table__cell").First;
        var text = (await cell.InnerTextAsync()).Replace("​", string.Empty).Trim();
        Assert.AreEqual(expected, text, $"Cell in column {columnIndex} should read '{expected}'.");
    }

    private static async Task AssertComputedAlignmentAsync(ILocator row, int columnIndex, string expected)
    {
        var cell = row.Locator("td.tm-notion-table__cell-td").Nth(columnIndex);
        var actual = await cell.EvaluateAsync<string>("element => getComputedStyle(element).textAlign");
        Assert.AreEqual(expected, actual, $"Column {columnIndex} should be {expected}-aligned.");
    }

    private static async Task SetThemeAsync(IPage page, bool dark)
    {
        await page.EvaluateAsync(
            """
            dark => {
                if (dark) {
                    document.documentElement.setAttribute('data-theme', 'dark');
                    document.body.classList.add('tm-dark');
                } else {
                    document.documentElement.removeAttribute('data-theme');
                    document.body.classList.remove('tm-dark');
                }
            }
            """,
            dark);
        await page.WaitForTimeoutAsync(250);
    }

    private async Task ImportMarkdownAsync(IPage page, string markdown, string expectedTitle)
        => await ImportFileAsync(page, "notion-import-markdown", await WriteTempFileAsync(markdown, ".md"), expectedTitle);

    private static async Task<string> WriteTempFileAsync(string content, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"tm-table-e2e-{Guid.NewGuid():N}{extension}");
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
        return path;
    }

    private async Task ImportFileAsync(IPage page, string importTestId, string path, string expectedTitle)
    {
        var menu = await OpenImportMenuAsync(page);
        var chooser = await page.RunAndWaitForFileChooserAsync(
            async () => await menu.Locator($"[data-testid='{importTestId}']").First.ClickAsync(),
            new PageRunAndWaitForFileChooserOptions { Timeout = 10000 });
        await chooser.SetFilesAsync(path);

        await page.Locator(".tm-notion-header-title")
            .Filter(new LocatorFilterOptions { HasText = expectedTitle })
            .First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 60000 });
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

    private static async Task<ILocator> OpenImportMenuAsync(IPage page)
    {
        var existing = page.Locator("[data-testid='notion-import-menu']").First;
        if (await existing.IsVisibleAsync())
        {
            return page.Locator(".tm-npsm").First;
        }

        var menu = await OpenSettingsMenuAsync(page);
        await menu.Locator(".tm-npsm__item").Filter(new LocatorFilterOptions { HasText = "Import" }).First.ClickAsync();
        await page.Locator("[data-testid='notion-import-menu']").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        return menu;
    }

    private static async Task<ILocator> OpenExportMenuAsync(IPage page)
    {
        var existing = page.Locator("[data-testid='notion-export-menu']").First;
        if (await existing.IsVisibleAsync())
        {
            return page.Locator(".tm-npsm").First;
        }

        var menu = await OpenSettingsMenuAsync(page);
        await menu.Locator(".tm-npsm__item").Filter(new LocatorFilterOptions { HasText = "Export" }).First.ClickAsync();
        await page.Locator("[data-testid='notion-export-menu']").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        return menu;
    }

    private static async Task<byte[]> DownloadMarkdownAsync(IPage page)
    {
        var menu = await OpenExportMenuAsync(page);
        var includeSubpages = menu.Locator("[data-testid='notion-export-include-subpages']").First;
        if (await includeSubpages.IsCheckedAsync())
        {
            await includeSubpages.UncheckAsync();
        }

        var download = await page.RunAndWaitForDownloadAsync(
            async () => await menu.Locator("[data-testid='notion-export-markdown']").First.ClickAsync(),
            new PageRunAndWaitForDownloadOptions { Timeout = 60000 });

        var path = await download.PathAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(path), "Markdown export should be written to disk.");
        return await File.ReadAllBytesAsync(path!);
    }

    private static Dm.TableBlockContent ImportTable(string markdown)
    {
        var document = new DocumentMarkdownImporter().Import(markdown);
        return (Dm.TableBlockContent)document.Blocks.First(block => block.Content is Dm.TableBlockContent).Content;
    }

    private static string CellText(Dm.TableBlockContent table, int row, int column)
        => string.Concat(table.Rows[row].Cells[column].Blocks
            .SelectMany(block => ((Dm.ParagraphBlockContent)block.Content).Inlines)
            .OfType<Dm.TextRun>()
            .Select(run => run.Text)).Trim();
}
