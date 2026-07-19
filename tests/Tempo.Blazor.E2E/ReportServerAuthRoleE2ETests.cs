using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Full-stack (Fáze 13 PASS B): role-based portal nav driven by the real Keycloak principal, plus the
/// BFF security invariants. author1 (report-author) sees the author nav and NOT the admin nav; viewer1
/// (report-viewer) sees only reports/favorites/history. No JWT is ever exposed to the browser
/// (no <c>eyJ</c> in web storage); the Api still requires the bearer the BFF holds (401 without,
/// 200 with); and logout bounces protected pages back to the Keycloak login.
/// </summary>
[TestClass]
[TestCategory("ReportServerFullStack")]
[DoNotParallelize]
public sealed class ReportServerAuthRoleE2ETests : ReportServerFullStackE2ETestBase
{
    [TestMethod]
    public async Task Author1_SeesAuthorNav_NotAdminNav()
    {
        var (_, page) = await LoginServerLegAsync("author1").ConfigureAwait(false);

        await Assertions.Expect(page.GetByTestId("signed-in-user")).ToContainTextAsync("author1").ConfigureAwait(false);

        // Author nav present.
        foreach (var testId in new[] { "nav-reports", "nav-favorites", "nav-history", "nav-designer", "nav-schedules", "nav-revisions" })
        {
            await Assertions.Expect(page.GetByTestId(testId)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 }).ConfigureAwait(false);
        }

        // Admin nav absent.
        foreach (var testId in new[] { "nav-datasources", "nav-permissions", "nav-apikeys" })
        {
            await Assertions.Expect(page.GetByTestId(testId)).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        await AssertNoJwtInStorageAsync(page).ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-fullstack-author-nav").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Viewer1_SeesOnlyViewerNav()
    {
        var (_, page) = await LoginServerLegAsync("viewer1").ConfigureAwait(false);

        await Assertions.Expect(page.GetByTestId("signed-in-user")).ToContainTextAsync("viewer1").ConfigureAwait(false);

        foreach (var testId in new[] { "nav-reports", "nav-favorites", "nav-history" })
        {
            await Assertions.Expect(page.GetByTestId(testId)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 }).ConfigureAwait(false);
        }

        foreach (var testId in new[] { "nav-designer", "nav-schedules", "nav-revisions", "nav-datasources", "nav-permissions", "nav-apikeys" })
        {
            await Assertions.Expect(page.GetByTestId(testId)).ToHaveCountAsync(0).ConfigureAwait(false);
        }

        await AssertNoJwtInStorageAsync(page).ConfigureAwait(false);
        await TakeScreenshotAsync(page, "rs-fullstack-viewer-nav").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task BearerLeg_ApiRequiresToken_AndBrowserHoldsNoToken()
    {
        // The Api rejects an unauthenticated catalog call…
        using (var anonymous = CreateAnonymousApiClient())
        using (var unauthorized = await anonymous.GetAsync($"/api/folders?tenantId={TenantId}").ConfigureAwait(false))
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode,
                "The protected Api must return 401 without a bearer.");
        }

        // …and accepts the very same short-lived bearer the WASM leg would obtain from /auth/token.
        var (_, page) = await LoginServerLegAsync("author1").ConfigureAwait(false);
        await AssertNoJwtInStorageAsync(page).ConfigureAwait(false);

        var token = await GetAuthTokenFromBrowserAsync(page).ConfigureAwait(false);
        Assert.IsFalse(string.IsNullOrWhiteSpace(token), "The signed-in session must mint a bearer via /auth/token.");
        StringAssert.StartsWith(token, "eyJ", "The minted token is a JWT.");

        using var apiClient = CreateAnonymousApiClient();
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var authorized = await apiClient.GetAsync($"/api/folders?tenantId={TenantId}").ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.OK, authorized.StatusCode,
            "The bearer minted for the browser session must be accepted by the protected Api (200).");
    }

    [TestMethod]
    public async Task Logout_ClearsSession_AndProtectedPagesRequireLogin()
    {
        var (context, page) = await LoginServerLegAsync("author1").ConfigureAwait(false);
        await Assertions.Expect(page.GetByTestId("nav-reports")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 }).ConfigureAwait(false);

        // While signed in the BFF mints a bearer.
        var before = await GetAuthTokenFromBrowserAsync(page).ConfigureAwait(false);
        StringAssert.StartsWith(before, "eyJ", "A signed-in session must serve a JWT from /auth/token.");

        // Sign out (full-browser navigation, same-origin GET → clears the BFF cookie + KC end-session).
        await page.GotoAsync(AbsoluteUrl("/account/logout"), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60_000 }).ConfigureAwait(false);

        // The BFF session is gone: /auth/token no longer hands out a token (an unauthenticated call is
        // challenged/redirected instead of returning JSON). This is the definitive proof the logout
        // cleared the server-side session (Keycloak's own SSO cookie may still silently re-authenticate a
        // fresh challenge, which is why we assert on the app session, not the raw KC redirect).
        await PollAsync(async () => string.IsNullOrEmpty(await GetAuthTokenFromBrowserAsync(page).ConfigureAwait(false)),
            "After logout /auth/token must no longer hand out a bearer — the BFF session is cleared.").ConfigureAwait(false);

        // An unauthenticated visitor (fresh cookies) hitting a protected page lands on the Keycloak login.
        await context.ClearCookiesAsync().ConfigureAwait(false);
        var freshPage = await context.NewPageAsync().ConfigureAwait(false);
        await freshPage.GotoAsync(AbsoluteUrl("/reports"), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60_000 }).ConfigureAwait(false);
        await Assertions.Expect(freshPage.Locator("#username")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 }).ConfigureAwait(false);
        await TakeScreenshotAsync(freshPage, "rs-fullstack-logout-redirect").ConfigureAwait(false);
    }

    private static async Task AssertNoJwtInStorageAsync(IPage page)
    {
        var hasJwt = await page.EvaluateAsync<bool>(
            """
            () => {
                const scan = (store) => {
                    for (let i = 0; i < store.length; i++) {
                        const k = store.key(i);
                        const v = store.getItem(k) ?? '';
                        if (k.includes('eyJ') || v.includes('eyJ')) return true;
                    }
                    return false;
                };
                return scan(window.localStorage) || scan(window.sessionStorage);
            }
            """).ConfigureAwait(false);
        Assert.IsFalse(hasJwt, "No JWT (eyJ) may be present in localStorage/sessionStorage — the BFF keeps tokens server-side.");
    }
}
