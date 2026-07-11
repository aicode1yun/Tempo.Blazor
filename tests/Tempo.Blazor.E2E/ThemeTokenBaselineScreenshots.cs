using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Full-page light/dark screenshots of every demo route that renders a component whose stylesheet
/// was migrated off the phantom <c>--tm-color-neutral-*</c> family onto the real semantic tokens.
/// These are the visual guard for that migration: the semantic tokens invert themselves in dark
/// mode, so the per-component dark overrides had to go, and only a rendered page shows whether the
/// result still looks right.
///
/// Screenshots land in <c>__screenshots__/theme-tokens/{light,dark}/</c>. Override the output
/// folder with the <c>TM_SCREENSHOT_PHASE</c> environment variable to capture a before/after pair.
/// </summary>
[TestClass]
public class ThemeTokenBaselineScreenshots : WasmTestBase
{
    /// <summary>Every demo route that renders at least one migrated component.</summary>
    private static readonly (string Route, string Name)[] Routes =
    [
        ("/feedback",          "feedback"),          // accordion, tabs, tooltip, popover, drawer, chips,
                                                     // context-menu, split-button, copy-button, progress,
                                                     // dynamic-form, number-input
        ("/forms",             "forms"),             // decimal-input, expression-editor
        ("/charts",            "charts"),            // chart, stock-chart, gauge, sparkline
        ("/kanban",            "kanban"),            // kanban
        ("/new-components",    "new-components"),    // slider, range-slider, rating
        ("/pickers",           "pickers"),           // calendar-view, file-drop-zone
        ("/file-manager",      "file-manager"),      // tabs, file-drop-zone
        ("/query-input",       "query-input"),       // query-input
        ("/workflow-designer", "workflow-designer"), // workflow-designer
    ];

    public static IEnumerable<object[]> RouteData => Routes.Select(r => new object[] { r.Route, r.Name });

    private static string OutputRoot
    {
        get
        {
            var phase = Environment.GetEnvironmentVariable("TM_SCREENSHOT_PHASE");
            var dir = Path.Combine(FindRepoRoot().FullName, "tests", "Tempo.Blazor.E2E", "__screenshots__", "theme-tokens");
            return string.IsNullOrWhiteSpace(phase) ? dir : Path.Combine(dir, phase);
        }
    }

    private static DirectoryInfo FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TempoBlazor.slnx")))
            {
                return current;
            }
            current = current.Parent!;
        }

        throw new DirectoryNotFoundException("Could not locate TempoBlazor.slnx.");
    }

    private static async Task CaptureAsync(IPage page, string theme, string name)
    {
        var dir = Path.Combine(OutputRoot, theme);
        Directory.CreateDirectory(dir);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(dir, $"{name}.png"),
            Type = ScreenshotType.Png,
            FullPage = true,
        });
    }

    [TestMethod]
    [DynamicData(nameof(RouteData))]
    [Description("Captures the light and dark rendering of a demo route for the token migration review")]
    public async Task Capture_LightAndDark(string route, string name)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await page.GotoAsync($"{BaseUrl}{route}",
            new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForAppReadyAsync(page);
        await page.WaitForTimeoutAsync(800);   // let charts/canvases settle

        await CaptureAsync(page, "light", name);

        // The demo marks the theme on the layout wrapper and renders two toggles (mobile + desktop
        // shell), so click the visible one and wait for data-theme rather than for a class on <html>.
        await page.Locator("button[title*='dark' i]:visible").First.ClickAsync();
        await page.WaitForSelectorAsync("[data-theme='dark']", new PageWaitForSelectorOptions { Timeout = 15000 });
        await page.WaitForTimeoutAsync(800);

        await CaptureAsync(page, "dark", name);
    }
}
