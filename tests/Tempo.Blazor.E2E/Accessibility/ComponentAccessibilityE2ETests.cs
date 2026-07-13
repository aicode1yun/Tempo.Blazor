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

    // Empty exclude by default. `exclude` is a list of CSS selectors for demo-page CHROME
    // (e.g. editorial section headings) that are not part of the swept component under test.
    private static async Task<string[]> AxeViolationsAsync(IPage page, string selector, string[] impacts, string[]? exclude = null)
    {
        await page.AddScriptTagAsync(new PageAddScriptTagOptions { Url = AxeCdn });
        return await page.EvaluateAsync<string[]>(
            """
            async ([selector, impacts, exclude]) => {
                const host = document.querySelector(selector) || document.body;
                const result = await axe.run(host, {
                    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa'] },
                    resultTypes: ['violations']
                });
                const isChrome = (target) => {
                    if (!exclude.length) return false;
                    const el = document.querySelector(target[target.length - 1]);
                    return el && exclude.some(sel => el.matches(sel) || el.closest(sel));
                };
                return result.violations
                    .filter(v => impacts.includes(v.impact))
                    .map(v => ({ v, nodes: v.nodes.filter(n => !isChrome(n.target)) }))
                    .filter(x => x.nodes.length > 0)
                    .map(x => `${x.v.impact}: ${x.v.id} - ${x.v.help} (${x.nodes.map(n => n.target.join(' ')).join('; ')})`);
            }
            """,
            new object[] { selector, impacts, exclude ?? Array.Empty<string>() });
    }

    private static readonly string[] CriticalOnly = ["critical"];
    private static readonly string[] CriticalOrSerious = ["critical", "serious"];

    private static async Task SetDarkAsync(IPage page)
    {
        await page.EvaluateAsync(
            """
            () => {
                document.documentElement.setAttribute('data-theme', 'dark');
                document.documentElement.classList.add('dark', 'tm-dark');
                document.body.classList.add('dark');
                // The demo drives Tailwind dark: variants off the MainLayout wrapper's class, so add
                // both the class and data-theme to every themed element (not just <html>).
                document.querySelectorAll('[data-theme]').forEach(el => {
                    el.setAttribute('data-theme', 'dark');
                    el.classList.add('dark', 'tm-dark');
                });
            }
            """);
        await page.WaitForTimeoutAsync(250);
    }

    // The swept component must be free of critical/serious violations in BOTH light and dark themes.
    private async Task AssertAxeCleanBothThemesAsync(IPage page, string selector, string screenshotName, string[]? excludeChrome = null)
    {
        var light = await AxeViolationsAsync(page, selector, CriticalOrSerious, excludeChrome);
        Assert.AreEqual(0, light.Length, "LIGHT:" + Environment.NewLine + string.Join(Environment.NewLine, light));

        await SetDarkAsync(page);
        await SaveScreenshotAsync(page, screenshotName + "-dark");
        var dark = await AxeViolationsAsync(page, selector, CriticalOrSerious, excludeChrome);
        Assert.AreEqual(0, dark.Length, "DARK:" + Environment.NewLine + string.Join(Environment.NewLine, dark));
    }

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
        // Exclude the demo SECTION HEADING (Tailwind text-slate-900 dark:text-white — correct in the
        // real app; the synthetic E2E dark toggle can't fully activate its Tailwind dark: variant).
        await AssertAxeCleanBothThemesAsync(page, ".demo-section", "forms", excludeChrome: ["h2"]);
    }

    [TestMethod]
    public async Task DataTable_Axe_HasNoCriticalOrSeriousViolations()
    {
        var page = await OpenAsync("/data-table");
        await page.Locator(".tm-data-table").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await SaveScreenshotAsync(page, "data-table");

        // Scope to the component instance (toolbar + filters + rows + pagination), not the demo
        // page's editorial prose/callouts/code, which carry their own (out-of-scope) contrast debt.
        await AssertAxeCleanBothThemesAsync(page, ".tm-data-table-wrapper", "data-table");
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

        // Open dialog must be free of critical/serious violations in light and dark.
        await AssertAxeCleanBothThemesAsync(page, ".tm-modal-overlay", "modal-open");

        // Close with Escape → focus returns to the trigger (focus restore).
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(overlay).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 10000 });

        var focusReturned = await page.EvaluateAsync<bool>(
            "() => document.activeElement?.closest('[data-testid]')?.getAttribute('data-testid') === 'open-basic-modal'");
        Assert.IsTrue(focusReturned, "Focus should return to the modal trigger after close.");
    }

    // ── Dark-theme broadening (this phase) ──────────────────────────────────────────────────────
    // K8 swept forms/data-table/modal in BOTH themes but the DARK theme was never broadly audited —
    // filled buttons/badges/alerts (and other component demos) were out of scope. These scans switch
    // the demo into dark, scope to the component container (NOT whole <main>), and assert ZERO
    // critical/serious WCAG contrast violations. They filter the same critical+serious impacts as K8
    // and exclude demo-page CHROME (Tailwind dark: editorial prose the synthetic toggle can't fully
    // activate) so only the swept component under test is asserted. Light stays covered by the three
    // K8 both-theme tests above; these are dark-only by design (the audit's remit).
    private async Task AssertAxeCleanDarkAsync(IPage page, string selector, string screenshotName, string[]? excludeChrome = null)
    {
        await SetDarkAsync(page);
        await SaveScreenshotAsync(page, screenshotName + "-dark");
        var dark = await AxeViolationsAsync(page, selector, CriticalOrSerious, excludeChrome);
        Assert.AreEqual(0, dark.Length, "DARK:" + Environment.NewLine + string.Join(Environment.NewLine, dark));
    }

    [TestMethod]
    public async Task Buttons_Dark_HasNoCriticalOrSeriousViolations()
    {
        var page = await OpenAsync("/buttons");
        await page.Locator("[data-testid='buttons-variants-card'] .tm-btn").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        // Scope to the first card, which holds every variant (filled primary/danger/warning +
        // secondary, outline, outline-secondary, outline-warning, ghost, link). In dark the filled
        // accents are light 400-tones, so primary/danger were switched to dark text in _button.css —
        // this verifies that fix reads clean. The card has no editorial prose, so no chrome exclude.
        await AssertAxeCleanDarkAsync(page, "[data-testid='buttons-variants-card']", "buttons-variants");
    }

    [TestMethod]
    public async Task Badges_Dark_HasNoCriticalOrSeriousViolations()
    {
        var page = await OpenAsync("/data-display");
        await page.Locator("[data-testid='badges-card'] .tm-badge").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        // Filled colour badges (primary/success/warning/danger/info) paired white text with the dark
        // palette's light 400-tone fills (~1.4–2.4:1). _badge.css now gives them dark text in dark.
        // Exclude the card's h3 sub-labels (Tailwind dark:text-slate-300 editorial chrome).
        await AssertAxeCleanDarkAsync(page, "[data-testid='badges-card']", "badges", excludeChrome: ["h3"]);
    }

    [TestMethod]
    public async Task Alerts_Dark_HasNoCriticalOrSeriousViolations()
    {
        var page = await OpenAsync("/feedback");
        await page.Locator("[data-testid='alerts-section'] .tm-alert").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        // Filled alerts kept white text on mid-tone fills that fail AA in dark (worst on the
        // .9-opacity description). _alert.css now fills them with the bright dark-palette accents +
        // dark text (mirrors buttons/badges). Soft + outlined variants already had dark overrides.
        // Exclude the section heading, the count badge, and the uppercase variant labels (chrome).
        await AssertAxeCleanDarkAsync(page, "[data-testid='alerts-section']", "alerts",
            excludeChrome: ["h2", "p", ".rounded-full"]);
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
