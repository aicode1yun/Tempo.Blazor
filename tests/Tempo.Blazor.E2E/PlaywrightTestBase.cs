using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Base class for Playwright E2E tests with support for multiple demo applications.
/// </summary>
[TestClass]
public abstract class PlaywrightTestBase
{
    private static IBrowser? _browser;
    private static IPlaywright? _playwright;
    private static readonly SemaphoreSlim BrowserLock = new(1, 1);

    // Per-test list of contexts created via CreatePageAsync / CreateContextAsync.
    // They MUST be disposed after each test, otherwise the shared static browser
    // process accumulates WebSocket/SignalR connections, service workers, and
    // IndexedDB handles for every sample page loaded, which eventually
    // exhausts Chromium and the OS kills the browser (manifests as cascade of
    // `TargetClosedException: Process exited` in later tests).
    private readonly List<IBrowserContext> _contextsToDispose = new();

    /// <summary>
    /// Gets the base URL for the demo application under test.
    /// </summary>
    protected abstract string BaseUrl { get; }

    /// <summary>
    /// Gets the browser instance for tests.
    /// </summary>
    protected static IBrowser Browser => _browser!;

    /// <summary>
    /// Gets or sets the test context from MSTest.
    /// </summary>
    public TestContext TestContext { get; set; } = default!;

    /// <summary>
    /// One-time setup for all tests - initializes Playwright and browser.
    /// </summary>
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task ClassInitialize(TestContext context)
    {
        await BrowserLock.WaitAsync();
        try
        {
            if (_playwright == null)
            {
                _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = !context.Properties.Contains("Headless") || context.Properties["Headless"]?.ToString() != "false",
                    SlowMo = 100 // Add small delay between actions for stability
                });
            }
        }
        finally
        {
            BrowserLock.Release();
        }
    }

    /// <summary>
    /// One-time cleanup for all tests - disposes browser and Playwright.
    /// </summary>
    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task ClassCleanup()
    {
        await BrowserLock.WaitAsync();
        try
        {
            if (_browser != null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }
            _playwright?.Dispose();
            _playwright = null;
        }
        finally
        {
            BrowserLock.Release();
        }
    }

    /// <summary>
    /// Creates a new browser context for a test. The context is automatically
    /// closed in <see cref="BaseTestCleanup"/> after the test finishes.
    /// </summary>
    protected async Task<IBrowserContext> CreateContextAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
            Locale = "en-US",
            IgnoreHTTPSErrors = true
        });
        _contextsToDispose.Add(context);
        return context;
    }

    /// <summary>
    /// Creates a new page and navigates to the base URL.
    /// </summary>
    protected async Task<IPage> CreatePageAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(BaseUrl);
        await WaitForAppReadyAsync(page);
        return page;
    }

    /// <summary>
    /// Closes all browser contexts that were created by the test via
    /// <see cref="CreatePageAsync"/> or <see cref="CreateContextAsync"/>.
    /// </summary>
    [TestCleanup]
    public async Task BaseTestCleanup()
    {
        foreach (var context in _contextsToDispose)
        {
            try { await context.CloseAsync(); }
            catch { /* best-effort: browser may already be gone */ }
        }
        _contextsToDispose.Clear();
    }

    /// <summary>
    /// Waits for the Blazor application to be ready.
    /// </summary>
    protected async Task WaitForAppReadyAsync(IPage page)
    {
        // Wait for the app to be fully loaded
        await page.WaitForSelectorAsync(".tm-app-loaded, main, [data-testid='app-ready']", new PageWaitForSelectorOptions
        {
            Timeout = 30000
        });

        // Additional wait for WASM to boot in InteractiveAuto mode
        await page.WaitForTimeoutAsync(1000);
    }

    /// <summary>
    /// Takes a screenshot and attaches it to the test result.
    /// </summary>
    protected async Task TakeScreenshotAsync(IPage page, string name)
    {
        var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Type = ScreenshotType.Png,
            FullPage = true
        });

        var path = Path.Combine(TestContext.TestResultsDirectory ?? ".", $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        await File.WriteAllBytesAsync(path, screenshot);
        TestContext.AddResultFile(path);
    }

    /// <summary>
    /// Clicks on a navigation menu item by its text.
    /// </summary>
    protected async Task NavigateToPageAsync(IPage page, string menuText)
    {
        var menuItem = page.Locator($"nav:has-text('{menuText}'), a:has-text('{menuText}'), button:has-text('{menuText}')").First;
        await menuItem.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Toggles dark mode and verifies the theme changed.
    /// </summary>
    protected async Task ToggleDarkModeAsync(IPage page)
    {
        // Find and click the theme toggle button
        var themeToggle = page.Locator("[data-testid='theme-toggle'], button[aria-label*='theme' i], button[title*='dark' i]").First;
        await themeToggle.ClickAsync();

        // Wait for the dark class to be applied/removed
        await page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Switches language to the specified culture.
    /// </summary>
    protected async Task SwitchLanguageAsync(IPage page, string culture)
    {
        // Find language switcher
        var langSwitcher = page.Locator("[data-testid='language-switcher'], select[name='culture']").First;
        await langSwitcher.ClickAsync();

        // Select the culture
        var option = page.Locator($"option[value='{culture}']").First;
        await option.ClickAsync();

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Performs memory heap snapshot for memory leak detection.
    /// </summary>
    protected async Task<long> GetHeapSizeAsync(IPage page)
    {
        var metrics = await page.EvaluateAsync<Dictionary<string, object>>("() => { return { usedJSHeapSize: performance.memory?.usedJSHeapSize || 0 }; }");
        return Convert.ToInt64(metrics["usedJSHeapSize"]);
    }
}

/// <summary>
/// Base class for WASM demo tests.
/// </summary>
[TestCategory("WASM")]
public abstract class WasmTestBase : PlaywrightTestBase
{
    protected override string BaseUrl => "https://localhost:7106";
}

/// <summary>
/// Base class for Server demo tests.
/// </summary>
[TestCategory("Server")]
public abstract class ServerTestBase : PlaywrightTestBase
{
    protected override string BaseUrl => "https://localhost:7107";
}

/// <summary>
/// Base class for InteractiveAuto demo tests.
/// </summary>
[TestCategory("InteractiveAuto")]
public abstract class InteractiveAutoTestBase : PlaywrightTestBase
{
    protected override string BaseUrl => "https://localhost:7108";
}
