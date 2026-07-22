using System.Net;
using System.Net.Http.Headers;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Full-stack (Fáze 13 PASS B): the InteractiveAuto Server→WebAssembly handoff under real OIDC. The
/// session is first observed on the interactive <b>Server</b> leg (WASM held back), then the WASM
/// runtime is allowed and the page reloads until Auto adopts <b>WebAssembly</b> — while <b>staying
/// authenticated</b> (no re-login redirect, still author1). The WASM session still mints a bearer
/// (from the same-origin <c>/auth/token</c>) that the protected Api accepts and returns catalog data
/// for. This is the real authenticated handoff + bearer-survival proof deferred from PASS A.
/// </summary>
[TestClass]
[TestCategory("ReportServerFullStack")]
[DoNotParallelize]
public sealed class ReportServerHandoffE2ETests : ReportServerFullStackE2ETestBase
{
    [TestMethod]
    public async Task ServerToWasm_Handoff_StaysAuthenticated_AndBearerSurvives()
    {
        // Seed a catalog folder for the OIDC tenant so an authorized catalog read has real data.
        var folderName = $"Handoff {UniqueTag()}";
        await SeedFolderAsync(folderName).ConfigureAwait(false);

        // Fresh context. Hold the WASM runtime back so the first interactive render is deterministically
        // the Server leg — the reliable way to observe "Server first" before the handoff.
        var context = await CreateContextAsync().ConfigureAwait(false);
        await ForceServerLegAsync(context).ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);
        await page.GotoAsync(AbsoluteUrl("/reports"), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000,
        }).ConfigureAwait(false);

        // Log in via the real Keycloak challenge and land on the portal shell.
        await KeycloakLoginAsync(page, "author1").ConfigureAwait(false);
        await WaitForInteractiveAsync(page).ConfigureAwait(false);

        // First interactive render is the Server circuit, authenticated as author1.
        Assert.AreEqual("Server", await GetRenderModeAsync(page).ConfigureAwait(false),
            "The first interactive render must be the Server leg.");
        await Assertions.Expect(page.GetByTestId("signed-in-user"))
            .ToContainTextAsync("author1", new LocatorAssertionsToContainTextOptions { Timeout = 20_000 }).ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-fullstack-handoff-server").ConfigureAwait(false);

        // Allow the WASM runtime and reload until Auto adopts the WebAssembly leg — the handoff.
        await context.UnrouteAsync("**/*.wasm").ConfigureAwait(false);
        await WaitForWasmAdoptedAsync(page).ConfigureAwait(false);
        Assert.AreEqual("WebAssembly", await GetRenderModeAsync(page).ConfigureAwait(false),
            "After the runtime is allowed and the page reloads, Auto must adopt WebAssembly.");

        // No Keycloak re-login was required — the browser was NOT bounced back to the Keycloak challenge
        // (the KC form's #username is absent). The authenticated BFF session persists across the handoff;
        // the bearer proof below is the load-bearing evidence.
        await Assertions.Expect(page.Locator("#username")).ToHaveCountAsync(0).ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-fullstack-handoff-wasm").ConfigureAwait(false);

        // Bearer survives the handoff: on the WebAssembly leg the same-origin /auth/token still mints a
        // real bearer whose subject is still author1, and the protected Api accepts it and returns the
        // seeded catalog. This proves the authenticated session (the bearer leg) genuinely survived the
        // Server→WASM handoff without any re-authentication against Keycloak.
        var token = await GetAuthTokenFromBrowserAsync(page).ConfigureAwait(false);
        Assert.IsFalse(string.IsNullOrWhiteSpace(token), "The WASM session must still mint a bearer post-handoff.");
        StringAssert.StartsWith(token, "eyJ", "The minted token is a JWT.");
        Assert.AreEqual("author1", ReadPreferredUsername(token),
            "The post-handoff bearer must still identify author1 (no silent re-login as someone else).");

        using var apiClient = CreateAnonymousApiClient();
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var folders = await apiClient.GetAsync($"/api/folders?tenantId={TenantId}").ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.OK, folders.StatusCode,
            "The post-handoff bearer must load the catalog from the protected Api (200).");
        var body = await folders.Content.ReadAsStringAsync().ConfigureAwait(false);
        StringAssert.Contains(body, folderName, "The authorized catalog read must include the seeded folder.");
    }

    private static async Task WaitForWasmAdoptedAsync(IPage page)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(150);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (string.Equals(await GetRenderModeAsync(page).ConfigureAwait(false), "WebAssembly", StringComparison.Ordinal))
            {
                return;
            }

            await page.WaitForTimeoutAsync(1500).ConfigureAwait(false);
            await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60_000 }).ConfigureAwait(false);
            await WaitForInteractiveAsync(page).ConfigureAwait(false);
        }
    }
}
