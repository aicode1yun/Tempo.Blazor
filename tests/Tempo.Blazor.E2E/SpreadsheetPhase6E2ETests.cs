using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class SpreadsheetPhase6E2ETests : WasmTestBase
{
    [TestMethod]
    public async Task NamedRange_CreateAndUseInFormula()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        // Navigate to A1 via Name Box
        await page.FillAsync(".tm-spreadsheet-formula-bar__ref", "A1");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(200);

        // Open Name Manager via Data tab
        await page.ClickAsync(".tm-spreadsheet-toolbar__tab:text-is('Data')");
        await page.ClickAsync("button[title='Name Manager']");
        await page.WaitForSelectorAsync(".tm-spreadsheet-name-manager", new() { State = WaitForSelectorState.Visible });

        // Create named range
        await page.ClickAsync(".tm-spreadsheet-name-manager__toolbar button:has-text('New')");
        await page.WaitForSelectorAsync(".tm-spreadsheet-named-range-edit", new() { State = WaitForSelectorState.Visible });
        await page.FillAsync("#nr-name", "TestRange");
        await page.FillAsync("#nr-refers", "=Sheet1!A1:A3");
        await page.ClickAsync(".tm-spreadsheet-named-range-edit__btn--ok");

        // Close manager
        await page.ClickAsync(".tm-spreadsheet-name-manager__btn--close");

        // Navigate to B1 via Name Box and enter formula
        await page.FillAsync(".tm-spreadsheet-formula-bar__ref", "B1");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(200);
        await page.Keyboard.TypeAsync("=SUM(TestRange)");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(500);

        // Navigate back to B1 to verify formula result (formula bar shows display value, not input, after commit)
        await page.FillAsync(".tm-spreadsheet-formula-bar__ref", "B1");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(300);
        await page.WaitForSelectorAsync(".tm-spreadsheet-formula-bar__display");

        // Verify formula accepted (cell shows result, not #NAME?)
        var displayValue = await page.TextContentAsync(".tm-spreadsheet-formula-bar__display");
        Assert.IsFalse(displayValue?.Contains("#NAME?") ?? false, $"Display should not contain #NAME?, was: {displayValue}");
    }

    [TestMethod]
    public async Task Hyperlink_InsertWebLink()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        await page.ClickAsync(".tm-spreadsheet-canvas-grid");
        await page.WaitForTimeoutAsync(300);

        // Open Insert tab → Link
        await page.ClickAsync(".tm-spreadsheet-toolbar__tab:text-is('Insert')");
        await page.ClickAsync("button[title='Insert link']");
        await page.WaitForSelectorAsync(".tm-spreadsheet-hyperlink", new() { State = WaitForSelectorState.Visible });

        // Fill dialog
        await page.SelectOptionAsync("#hl-type", "Web");
        await page.FillAsync("#hl-target", "https://example.com");
        await page.FillAsync("#hl-display", "Example");
        await page.ClickAsync(".tm-spreadsheet-hyperlink__btn--ok");

        // Verify cell has blue hyperlink styling (canvas renders it)
        await page.WaitForTimeoutAsync(300);
        var cellText = await page.TextContentAsync(".tm-spreadsheet-canvas-grid");
        StringAssert.Contains(cellText, "Example");
    }

    [TestMethod]
    public async Task NameBox_NavigateToNamedRange()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        // Pre-create a named range via Name Manager
        await page.ClickAsync(".tm-spreadsheet-toolbar__tab:text-is('Data')");
        await page.ClickAsync("button[title='Name Manager']");
        await page.WaitForSelectorAsync(".tm-spreadsheet-name-manager", new() { State = WaitForSelectorState.Visible });
        await page.ClickAsync(".tm-spreadsheet-name-manager__toolbar button:has-text('New')");
        await page.WaitForSelectorAsync(".tm-spreadsheet-named-range-edit", new() { State = WaitForSelectorState.Visible });
        await page.FillAsync("#nr-name", "JumpTarget");
        await page.FillAsync("#nr-refers", "=Sheet1!D10");
        await page.ClickAsync(".tm-spreadsheet-named-range-edit__btn--ok");
        await page.ClickAsync(".tm-spreadsheet-name-manager__btn--close");

        // Use Name Box to navigate
        await page.ClickAsync(".tm-spreadsheet-formula-bar__ref");
        await page.FillAsync(".tm-spreadsheet-formula-bar__ref", "JumpTarget");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(500);

        // Name box should now show D10
        var nameBoxValue = await page.InputValueAsync(".tm-spreadsheet-formula-bar__ref");
        Assert.AreEqual("D10", nameBoxValue);
    }
}
