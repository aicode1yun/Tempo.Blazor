using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.E2E;

[TestClass]
[TestCategory("WASM")]
public sealed class TmButtonDataTableScopeE2ETests : WasmTestBase
{
    [TestMethod]
    public async Task TmButton_NewVariants_RenderExpectedCssClasses()
    {
        var page = await OpenDemoPageAsync("/buttons", "[data-testid='button-variant-outline-secondary']");

        await ExpectClassAsync(page, "button-variant-outline-secondary", "tm-btn-outline-secondary");
        await ExpectClassAsync(page, "button-variant-warning", "tm-btn-warning");
        await ExpectClassAsync(page, "button-variant-outline-warning", "tm-btn-outline-warning");
    }

    [TestMethod]
    public async Task TmDataTable_RowAttributes_RenderForFlatVirtualizedAndGroupedRows()
    {
        var page = await OpenDemoPageAsync("/data-table", "[data-testid='datatable-rowattrs-flat-alice']");

        await ExpectRowAttributesAsync(page, "datatable-rowattrs-flat-alice", "flat", "Engineering", "91");
        await ExpectRowAttributesAsync(page, "datatable-rowattrs-virtual-bob", "virtual", "Sales", "84");
        await ExpectRowAttributesAsync(page, "datatable-rowattrs-grouped-diana", "grouped", "Engineering", "95");
    }

    private async Task<IPage> OpenDemoPageAsync(string path, string readySelector)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1366, 900);
        await page.GotoAsync($"{BaseUrl}{path}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForAppReadyAsync(page);
        await page.WaitForSelectorAsync(readySelector, new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
        return page;
    }

    private static async Task ExpectClassAsync(IPage page, string testId, string expectedClass)
    {
        var button = page.Locator($"[data-testid='{testId}']");
        await Assertions.Expect(button).ToHaveClassAsync(new Regex($@"(^|\s){Regex.Escape(expectedClass)}(\s|$)"));
    }

    private static async Task ExpectRowAttributesAsync(IPage page, string testId, string mode, string dept, string score)
    {
        var row = page.Locator($"[data-testid='{testId}']");
        await Assertions.Expect(row).ToHaveAttributeAsync("data-row-mode", mode);
        await Assertions.Expect(row).ToHaveAttributeAsync("data-row-dept", dept);
        await Assertions.Expect(row).ToHaveAttributeAsync("data-row-score", score);
    }
}
