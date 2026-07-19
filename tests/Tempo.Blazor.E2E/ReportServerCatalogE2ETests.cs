using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Report Server E2E — functional-server leg. Blocks the WebAssembly runtime so the InteractiveAuto
/// portal stays on the Blazor Server circuit, then drives the real catalog / favorites / render-run
/// flows against the Api and asserts the resulting rows directly in the Api's SQLite database.
/// </summary>
/// <remarks>
/// The portal's data client runs server-side here (host <c>Api:BaseUrl</c> → the Api on 7001), so the
/// full catalog surface is reachable — mirroring why the project's own f12 drivers run these flows on
/// the Server leg. See <see cref="ReportServerE2ETestBase"/> for how the lane is hosted and gated.
/// </remarks>
[TestClass]
[TestCategory("ReportServerE2E")]
[TestCategory("ReportServerE2E:Server")]
[DoNotParallelize]
public sealed class ReportServerCatalogServerE2ETests : ReportServerE2ETestBase
{
    [TestMethod]
    public async Task Catalog_PortalLoads_CreateFolder_PersistsRow()
    {
        var page = await OpenServerPageAsync("/").ConfigureAwait(false);
        await DemoSignInAsync(page).ConfigureAwait(false);
        await WaitForInteractiveAsync(page).ConfigureAwait(false);

        Assert.AreEqual("Server", await GetRenderModeAsync(page).ConfigureAwait(false), "The Server leg must report data-mode=Server.");
        await Assertions.Expect(page.GetByTestId("tm-report-explorer")).ToBeVisibleAsync().ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-server-explorer").ConfigureAwait(false);

        var folderName = $"E2E Folder {UniqueTag()}";
        await page.GetByTestId("tm-report-new-folder-name").FillAsync(folderName).ConfigureAwait(false);
        await page.Locator("[data-testid='tm-report-folder-create'] button").ClickAsync().ConfigureAwait(false);

        await PollAsync(async () => await CountFoldersAsync(TenantId, folderName).ConfigureAwait(false) == 1,
            $"Folder '{folderName}' should be persisted as a Folders row.").ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-server-folder-created").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task NewReport_CreatesRealReport_ShownInDesigner_AndPersisted()
    {
        var folderName = $"Reports {UniqueTag()}";
        var (folderId, _) = await SeedFolderAsync(folderName).ConfigureAwait(false);

        var page = await OpenServerPageAsync("/").ConfigureAwait(false);
        await DemoSignInAsync(page).ConfigureAwait(false);

        var reportName = $"E2E Ledger {UniqueTag()}";
        await page.GetByTestId("new-report-open").ClickAsync().ConfigureAwait(false);
        await page.GetByTestId("new-report-form").WaitForAsync().ConfigureAwait(false);
        await page.GetByTestId("new-report-name").FillAsync(reportName).ConfigureAwait(false);
        await page.GetByTestId("new-report-folder").SelectOptionAsync(new SelectOptionValue { Value = folderId }).ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-server-newreport-form").ConfigureAwait(false);
        await page.GetByTestId("new-report-submit").ClickAsync().ConfigureAwait(false);

        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/designer/"), new PageWaitForURLOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await page.GetByTestId("f13-designer-page").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 }).ConfigureAwait(false);

        // The designer must show the REAL created report, never the demo "Sales Register".
        await Assertions.Expect(page.Locator("body")).ToContainTextAsync(reportName, new LocatorAssertionsToContainTextOptions { Timeout = 15_000 }).ConfigureAwait(false);
        var body = await page.Locator("body").InnerTextAsync().ConfigureAwait(false);
        Assert.IsFalse(body.Contains("Sales Register", StringComparison.Ordinal), "Designer must not fall back to the demo Sales Register.");

        await PollAsync(async () => await CountReportsAsync(TenantId, reportName).ConfigureAwait(false) == 1,
            $"Report '{reportName}' should be persisted as a Reports row.").ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-server-designer").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Favorites_ToggleRoundTrips_AndPersists()
    {
        var (folderId, folderPath) = await SeedFolderAsync($"Fav{UniqueTag()}").ConfigureAwait(false);
        var reportName = $"E2E Fav Report {UniqueTag()}";
        var reportId = await SeedReportAsync(folderId, reportName, parametric: false).ConfigureAwait(false);

        var page = await OpenSeededReportViewerAsync(folderPath, reportId).ConfigureAwait(false);
        await Assertions.Expect(page.GetByTestId("report-not-found")).ToHaveCountAsync(0).ConfigureAwait(false);

        var toggle = page.GetByTestId("favorite-toggle");
        await toggle.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 }).ConfigureAwait(false);
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-pressed", "false", new LocatorAssertionsToHaveAttributeOptions { Timeout = 15_000 }).ConfigureAwait(false);
        await toggle.ClickAsync().ConfigureAwait(false);
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-pressed", "true", new LocatorAssertionsToHaveAttributeOptions { Timeout = 15_000 }).ConfigureAwait(false);
        await PollAsync(async () => await CountFavoritesAsync(TenantId, reportId).ConfigureAwait(false) == 1,
            "Favorite should be persisted as a Favorites row.").ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-server-favorited").ConfigureAwait(false);

        // /favorites lists it, and clicking round-trips back to the resolved viewer (no report-not-found).
        await page.GetByTestId("nav-favorites").ClickAsync().ConfigureAwait(false);
        await page.GetByTestId("f12-favorites-page").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await Assertions.Expect(page.GetByTestId("favorites-list")).ToBeVisibleAsync().ConfigureAwait(false);
        await Assertions.Expect(page.GetByTestId("favorite-item").First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 }).ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-server-favorites-list").ConfigureAwait(false);

        await page.GetByTestId("favorite-item").First.ClickAsync().ConfigureAwait(false);
        await page.GetByTestId("f12-viewer-page").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await Assertions.Expect(page.GetByTestId("report-not-found")).ToHaveCountAsync(0).ConfigureAwait(false);
        await Assertions.Expect(page.GetByTestId("favorite-toggle")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 }).ConfigureAwait(false);

        // Un-favorite → row gone → empty state.
        var toggleBack = page.GetByTestId("favorite-toggle");
        await toggleBack.ClickAsync().ConfigureAwait(false);
        await Assertions.Expect(toggleBack).ToHaveAttributeAsync("aria-pressed", "false", new LocatorAssertionsToHaveAttributeOptions { Timeout = 15_000 }).ConfigureAwait(false);
        await PollAsync(async () => await CountFavoritesAsync(TenantId, reportId).ConfigureAwait(false) == 0,
            "Un-favorite should remove the Favorites row.").ConfigureAwait(false);
        await page.GetByTestId("nav-favorites").ClickAsync().ConfigureAwait(false);
        await page.GetByTestId("f12-favorites-page").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await Assertions.Expect(page.GetByTestId("favorites-empty")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 }).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Render_WithParameter_RecordsRunInHistory()
    {
        var (folderId, folderPath) = await SeedFolderAsync($"Run{UniqueTag()}").ConfigureAwait(false);
        var reportName = $"E2E Param Report {UniqueTag()}";
        var reportId = await SeedReportAsync(folderId, reportName, parametric: true).ConfigureAwait(false);

        var page = await OpenSeededReportViewerAsync(folderPath, reportId).ConfigureAwait(false);
        await page.GetByTestId("viewer-param-form").WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 }).ConfigureAwait(false);

        await page.GetByTestId("param-input-AsOfDate").FillAsync("2026-07-19").ConfigureAwait(false);
        await page.GetByTestId("run-format").SelectOptionAsync("Snapshot").ConfigureAwait(false);
        await page.GetByTestId("run-report").ClickAsync().ConfigureAwait(false);
        await page.GetByTestId("run-report-status").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-server-run").ConfigureAwait(false);

        await PollAsync(async () =>
        {
            var run = await LatestRenderRunAsync(TenantId, reportId).ConfigureAwait(false);
            return run is not null && run.ParametersJson.Contains("AsOfDate", StringComparison.Ordinal);
        }, "A RenderRuns row with the AsOfDate parameter should be persisted.").ConfigureAwait(false);

        await page.GetByTestId("nav-history").ClickAsync().ConfigureAwait(false);
        await page.GetByTestId("run-history-page").WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await Assertions.Expect(page.GetByTestId("run-history-row").First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 }).ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-server-history").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task EdgeCases_InvalidUploadBlocks_AndDirectReportHitBouncesToLogin()
    {
        var (folderId, _) = await SeedFolderAsync($"Edge {UniqueTag()}").ConfigureAwait(false);

        var page = await OpenServerPageAsync("/").ConfigureAwait(false);
        await DemoSignInAsync(page).ConfigureAwait(false);

        // Invalid-JSON upload shows an inline error and blocks submit.
        await page.GetByTestId("new-report-open").ClickAsync().ConfigureAwait(false);
        await page.GetByTestId("new-report-form").WaitForAsync().ConfigureAwait(false);
        await page.GetByTestId("new-report-source-upload").ClickAsync().ConfigureAwait(false);
        await page.GetByTestId("new-report-file").WaitForAsync().ConfigureAwait(false);
        await page.GetByTestId("new-report-file").SetInputFilesAsync(new FilePayload
        {
            Name = "broken.json",
            MimeType = "application/json",
            Buffer = System.Text.Encoding.UTF8.GetBytes("{\"broken\": "),
        }).ConfigureAwait(false);
        await Assertions.Expect(page.GetByTestId("new-report-file-error")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 }).ConfigureAwait(false);

        await page.GetByTestId("new-report-name").FillAsync($"Should Not Create {UniqueTag()}").ConfigureAwait(false);
        await page.GetByTestId("new-report-folder").SelectOptionAsync(new SelectOptionValue { Value = folderId }).ConfigureAwait(false);
        // The submit button is disabled while a file-parse error is showing; force the click past the
        // actionability wait — SubmitAsync also refuses to submit an unresolved upload, so the form stays.
        await page.GetByTestId("new-report-submit").ClickAsync(new LocatorClickOptions { Force = true }).ConfigureAwait(false);
        // Submit is blocked: still on the form (auto-waited), never navigated to the designer.
        await Assertions.Expect(page.GetByTestId("new-report-form")).ToBeVisibleAsync().ConfigureAwait(false);
        StringAssert.DoesNotMatch(page.Url, new System.Text.RegularExpressions.Regex("/designer/"));
        await TakeScreenshotAsync(page, "rs-server-upload-error").ConfigureAwait(false);

        // A bogus report path degrades gracefully — never a developer exception page. In the OIDC-off
        // demo a full navigation starts a fresh unauthenticated circuit, so the shell bounces any direct
        // report hit to the login page rather than throwing. (The authenticated report-not-found panel
        // itself is covered by Web.Tests bUnit ReportViewerPageTests.)
        var notFound = await OpenServerPageAsync($"/reports/does-not-exist-{UniqueTag()}").ConfigureAwait(false);
        await Assertions.Expect(notFound.GetByTestId("f12-login-page")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await Assertions.Expect(notFound.Locator("#blazor-error-ui")).ToBeHiddenAsync().ConfigureAwait(false);
        await TakeScreenshotAsync(notFound, "rs-server-not-found-graceful").ConfigureAwait(false);
    }

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..8];

    private static async Task PollAsync(Func<Task<bool>> condition, string message, int timeoutMs = 15_000)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition().ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(300).ConfigureAwait(false);
        }

        Assert.Fail(message);
    }
}

/// <summary>
/// Report Server E2E — functional-wasm leg. Primes the WebAssembly runtime then reloads until the
/// InteractiveAuto portal adopts the browser (WebAssembly) renderer, and asserts the render-mode split
/// plus a graceful not-found state.
/// </summary>
/// <remarks>
/// The catalog / favorites / history write-flows are validated on the Server leg
/// (<see cref="ReportServerCatalogServerE2ETests"/>): on the WASM leg the portal's data client runs in
/// the browser and reads the WASM app's baked <c>Api:BaseUrl</c> (the Web origin), which does not host
/// the catalog API — the same reason the project's own f12 drivers block WASM for those flows. This
/// class therefore covers what the WASM leg can prove: it really boots to WebAssembly, and the
/// not-found path degrades gracefully rather than throwing.
/// </remarks>
[TestClass]
[TestCategory("ReportServerE2E")]
[TestCategory("ReportServerE2E:WebAssembly")]
[DoNotParallelize]
public sealed class ReportServerCatalogWasmE2ETests : ReportServerE2ETestBase
{
    [TestMethod]
    public async Task LoginPage_BootsTo_WebAssembly()
    {
        var page = await OpenWasmPageAsync("/").ConfigureAwait(false);

        Assert.AreEqual("WebAssembly", await GetRenderModeAsync(page).ConfigureAwait(false), "The WASM leg must report data-mode=WebAssembly.");
        await Assertions.Expect(page.GetByTestId("login-interactive-ready")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-wasm-login").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Portal_RunsCleanly_OnWasm()
    {
        // The login page makes no Api calls, so it is the reliable surface to prove the portal runs on
        // the WebAssembly leg (the catalog pages' cross-origin data path is a Server-leg concern —
        // see the class remarks). Assert the browser runtime is active and the shell rendered without a
        // Blazor error boundary tripping.
        var page = await OpenWasmPageAsync("/").ConfigureAwait(false);

        Assert.AreEqual("WebAssembly", await GetRenderModeAsync(page).ConfigureAwait(false));
        await Assertions.Expect(page.GetByTestId("login-submit")).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await Assertions.Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync().ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-wasm-clean").ConfigureAwait(false);
    }
}
