using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Full-stack (Fáze 13 PASS B): server prerender fidelity. The initial (authenticated) HTML response
/// for the reports landing already contains real portal content — the shell and role-based nav are
/// server-rendered, not an empty loading shell — and the interactive-Server circuit issues no
/// browser-side catalog GET (its catalog HttpClient calls run server-to-server over the circuit).
/// </summary>
[TestClass]
[TestCategory("ReportServerFullStack")]
[DoNotParallelize]
public sealed class ReportServerPrerenderE2ETests : ReportServerFullStackE2ETestBase
{
    [TestMethod]
    public async Task Reports_PrerenderedHtml_ContainsRealPortalContent()
    {
        // Establish an authenticated session (server leg) so the protected page can be fetched raw.
        var (context, _) = await LoginServerLegAsync("author1").ConfigureAwait(false);

        // Fetch the protected page raw, reusing the browser session cookies. No JS runs here, so any
        // portal content in the body is genuinely server-prerendered (not injected by the interactive
        // runtime).
        var response = await context.APIRequest.GetAsync(AbsoluteUrl("/reports")).ConfigureAwait(false);
        Assert.AreEqual(200, response.Status, "The authenticated reports page must return 200 (not a login redirect).");
        var html = await response.TextAsync().ConfigureAwait(false);

        StringAssert.Contains(html, "report-server-shell", "Prerendered HTML must contain the portal shell.");
        StringAssert.Contains(html, "nav-reports", "Prerendered HTML must contain the server-rendered role-based nav.");
        StringAssert.Contains(html, "author1", "Prerendered HTML must reflect the signed-in Keycloak user.");
    }

    [TestMethod]
    public async Task Reports_ServerCircuit_EmitsNoBrowserSideCatalogFetch()
    {
        // Seed data so the landing page has a real catalog to render server-side.
        await SeedFolderAsync($"Prerender {UniqueTag()}").ConfigureAwait(false);

        var context = await CreateContextAsync().ConfigureAwait(false);
        await ForceServerLegAsync(context).ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);

        // Count browser-side catalog data requests across the whole load + interactive takeover.
        var catalogRequests = 0;
        page.Request += (_, request) =>
        {
            if (request.Url.Contains("/api/folders", StringComparison.OrdinalIgnoreCase) ||
                request.Url.Contains("/api/reports", StringComparison.OrdinalIgnoreCase) ||
                request.Url.Contains("/api/catalog", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref catalogRequests);
            }
        };

        await page.GotoAsync(AbsoluteUrl("/reports"), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000,
        }).ConfigureAwait(false);
        await KeycloakLoginAsync(page, "author1").ConfigureAwait(false);
        await WaitForInteractiveAsync(page).ConfigureAwait(false);

        // Let any client-side re-fetch settle out.
        await page.WaitForTimeoutAsync(2500).ConfigureAwait(false);

        // On the interactive-Server circuit the portal's catalog HttpClient calls run server-to-server
        // (BFF → Api) over the circuit, so the browser emits zero catalog GETs. This confirms the
        // Server circuit issues no browser-side catalog fetch — it is NOT, on its own, proof of
        // prerender-state reuse (a real double-fetch would only be observable on the WASM leg, which
        // currently has the auth-rehydration gap documented on the handoff spec).
        Assert.AreEqual(0, catalogRequests,
            "The interactive-Server circuit must issue no browser-side catalog GET (catalog runs server-to-server).");
        await TakeScreenshotAsync(page, "rs-fullstack-server-no-browser-catalog-fetch").ConfigureAwait(false);
    }
}
