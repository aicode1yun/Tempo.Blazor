using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// The demo page section that documents Markdown table import, alignment and round-trip.
/// </summary>
[TestClass]
public class NotionMarkdownTableDemoE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("The demo section parses the default markdown, previews the aligned table, and round-trips it back to markdown")]
    public async Task MarkdownTableDemo_ShowsStructurePreviewAndRoundTrip()
    {
        var page = await OpenNotionEditorAsync();

        var section = page.Locator("[data-testid='notion-markdown-table-demo']");
        await section.ScrollIntoViewIfNeededAsync();
        await section.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30000 });

        var structure = await section.Locator("[data-testid='markdown-table-structure']").InnerTextAsync();
        StringAssert.Contains(structure, "Table blocks: 1");
        StringAssert.Contains(structure, "Row blocks (children): 3");
        StringAssert.Contains(structure, "Columns: 4");
        StringAssert.Contains(structure, "None, Left, Center, Right");

        var preview = section.Locator("[data-testid='markdown-table-preview']");
        Assert.AreEqual(1, await preview.Locator("table").CountAsync());
        Assert.AreEqual("center", await preview.Locator("th").Nth(2).EvaluateAsync<string>("el => getComputedStyle(el).textAlign"));
        Assert.AreEqual("right", await preview.Locator("th").Nth(3).EvaluateAsync<string>("el => getComputedStyle(el).textAlign"));

        var roundTrip = await section.Locator("[data-testid='markdown-table-roundtrip']").InnerTextAsync();
        StringAssert.Contains(roundTrip, "| Plain | Left | Center | Right |");
        StringAssert.Contains(roundTrip, "| --- | :--- | :---: | ---: |");

        await CaptureBaselineAsync("code-markdown-preview", "demo-section-light", section);
        TestContext.WriteLine("UX: the demo puts source, parsed structure, rendered preview and re-exported markdown side by side, so the round-trip guarantee is visible at a glance.");
    }

    [TestMethod]
    [Description("Editing the demo markdown re-parses it live, including dropping the table when the separator is invalid")]
    public async Task MarkdownTableDemo_ReactsToEdits()
    {
        var page = await OpenNotionEditorAsync();

        var section = page.Locator("[data-testid='notion-markdown-table-demo']");
        await section.ScrollIntoViewIfNeededAsync();
        var source = section.Locator("[data-testid='markdown-table-source']");
        await source.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30000 });

        // A single-column table is still a table.
        await source.FillAsync("| Single |\n| :---: |\n| only |");
        await page.WaitForTimeoutAsync(600);
        var structure = await section.Locator("[data-testid='markdown-table-structure']").InnerTextAsync();
        StringAssert.Contains(structure, "Table blocks: 1");
        StringAssert.Contains(structure, "Columns: 1");
        StringAssert.Contains(structure, "Center");

        // A thematic break is not a delimiter row.
        await source.FillAsync("before\n\n---\n\nafter");
        await page.WaitForTimeoutAsync(600);
        structure = await section.Locator("[data-testid='markdown-table-structure']").InnerTextAsync();
        StringAssert.Contains(structure, "Table blocks: 0");
        Assert.AreEqual(0, await section.Locator("[data-testid='markdown-table-preview'] table").CountAsync());
    }
}
