using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E.Accessibility;

/// <summary>
/// K8 accessibility E2E (WASM @ 7106): axe-core scans of the demo pages for the swept components
/// (inputs /forms, data table /data-table, modal /modal-dialog) assert ZERO critical or serious
/// WCAG violations, and the TmModal focus trap returns focus to the trigger on close.
/// Screenshots land in <c>__screenshots__/accessibility/</c> for UX review.
/// </summary>
[TestClass]
[TestCategory("WASM")]
public sealed class ComponentAccessibilityE2ETests : WasmTestBase
{
    private const string AxeCdn = "https://cdnjs.cloudflare.com/ajax/libs/axe-core/4.10.2/axe.min.js";

    private async Task<IPage> OpenAsync(string route)
    {
        var context = await CreateContextAsync();
        await context.AddInitScriptAsync("localStorage.setItem('tm-demo-culture', 'en');");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await page.GotoAsync($"{BaseUrl}{route}", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForAppReadyAsync(page);
        return page;
    }

    private static async Task<string[]> AxeViolationsAsync(IPage page, string selector, string[] impacts)
    {
        await page.AddScriptTagAsync(new PageAddScriptTagOptions { Url = AxeCdn });
        return await page.EvaluateAsync<string[]>(
            """
            async ([selector, impacts]) => {
                const host = document.querySelector(selector) || document.body;
                const result = await axe.run(host, {
                    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa'] },
                    resultTypes: ['violations']
                });
                return result.violations
                    .filter(v => impacts.includes(v.impact))
                    .map(v => `${v.impact}: ${v.id} - ${v.help} (${v.nodes.map(n => n.target.join(' ')).join('; ')})`);
            }
            """,
            new object[] { selector, impacts });
    }

    private static readonly string[] CriticalOnly = ["critical"];
    private static readonly string[] CriticalOrSerious = ["critical", "serious"];

    [TestMethod]
    public async Task FormInputs_Axe_HasNoCriticalOrSeriousViolations()
    {
        var page = await OpenAsync("/forms");
        await page.Locator(".demo-section").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await SaveScreenshotAsync(page, "forms");

        // Scope to the first section — the validation text inputs this phase actually swept
        // (labeled TmTextInput/TmTextArea incl. a "With Error" instance exercising aria-invalid/
        // aria-describedby/role=alert) — not the whole demo page, whose other widgets + editorial
        // prose carry app-wide pre-existing colour-contrast debt orthogonal to this phase.
        var violations = await AxeViolationsAsync(page, ".demo-section", CriticalOrSerious);
        Assert.AreEqual(0, violations.Length, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public async Task DataTable_Axe_HasNoCriticalOrSeriousViolations()
    {
        var page = await OpenAsync("/data-table");
        await page.Locator(".tm-data-table").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await SaveScreenshotAsync(page, "data-table");

        // Scope to the component instance (toolbar + filters + rows + pagination), not the demo
        // page's editorial prose/callouts/code, which carry their own (out-of-scope) contrast debt.
        var violations = await AxeViolationsAsync(page, ".tm-data-table-wrapper", CriticalOrSerious);
        Assert.AreEqual(0, violations.Length, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public async Task Modal_Axe_HasNoCriticalOrSerious_AndRestoresFocusOnClose()
    {
        var page = await OpenAsync("/modal-dialog");

        var trigger = page.GetByTestId("open-basic-modal");
        await trigger.ClickAsync();

        var overlay = page.Locator(".tm-modal-overlay");
        await Assertions.Expect(overlay).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });
        await Assertions.Expect(page.Locator(".tm-modal")).ToHaveAttributeAsync("aria-modal", "true");
        await SaveScreenshotAsync(page, "modal-open");

        // Open dialog must be free of critical/serious violations.
        var violations = await AxeViolationsAsync(page, ".tm-modal-overlay", CriticalOrSerious);
        Assert.AreEqual(0, violations.Length, string.Join(Environment.NewLine, violations));

        // Close with Escape → focus returns to the trigger (focus restore).
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(overlay).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 10000 });

        var focusReturned = await page.EvaluateAsync<bool>(
            "() => document.activeElement?.closest('[data-testid]')?.getAttribute('data-testid') === 'open-basic-modal'");
        Assert.IsTrue(focusReturned, "Focus should return to the modal trigger after close.");
    }

    private static async Task SaveScreenshotAsync(IPage page, string fileName)
    {
        var dir = Path.Combine(FindRepoRoot().FullName, "tests", "Tempo.Blazor.E2E", "__screenshots__", "accessibility");
        Directory.CreateDirectory(dir);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(dir, $"{fileName}.png"), FullPage = true });
    }

    private static DirectoryInfo FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx"))) return directory;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
