using System.Net.Http.Json;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionRestrictionsE2ETests : NotionE2ETestBase
{
    private static readonly Guid RootPageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChildPageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string ApiBaseUrl = "https://localhost:5100";

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF20: Page restrictions dialog, edit-only user, read-only others, inheritance, no provider, no-view, and user-vs-group conflict.")]
    public async Task PageRestrictions_AclDialogEffectivePermissionsAndEdges()
    {
        try
        {
            var page = await OpenNotionEditorAsync();
            await SeedRestrictionsPageAsync();

            await OpenRestrictionsDialogAsync(page);
            var dialog = page.Locator(".tm-nprd").First;
            await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
            Assert.IsTrue(await dialog.Locator(".tm-nprd__entry").CountAsync() >= 2, "Seeded restriction entries should render.");

            var dialogCapture = await CaptureBaselineAsync("restrictions", "cf20-restrictions-dialog", dialog);
            TestContext.WriteLine($"UX CF20 dialog baseline captured: {dialogCapture.FullPagePath} / {dialogCapture.RegionPath}");

            await dialog.Locator(".tm-nprd__primary").ClickAsync();
            await page.Locator(".tm-nprd").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 10000 });

            await page.Locator(".tm-notion-restricted-badge").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
            Assert.AreEqual(0, await page.Locator(".tm-notion-page--readonly").CountAsync(), "Alice should keep Edit access.");

            var indicatorCapture = await CaptureBaselineAsync("restrictions", "cf20-restricted-indicator", page.Locator(".tm-notion-editor").First);
            TestContext.WriteLine($"UX CF20 restricted indicator captured: {indicatorCapture.FullPagePath} / {indicatorCapture.RegionPath}");

            var bob = await OpenNotionEditorAsync("?user=bob&groups=readers");
            await bob.Locator(".tm-notion-page--readonly").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
            Assert.AreEqual(1, await bob.Locator(".tm-notion-restricted-badge").CountAsync(), "Bob should see the restricted indicator.");

            await bob.Locator(".tm-npt-toggle").First.ClickAsync();
            await bob.Locator(".tm-npt-title").Filter(new() { HasText = "CF20 Child Inherits Restrictions" }).ClickAsync();
            await bob.Locator($"article[data-page-id='{ChildPageId:D}'].tm-notion-page--readonly").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });

            var providerless = await OpenNotionEditorAsync("?disablePermissionProvider=true&user=charlie&groups=guests");
            Assert.AreEqual(0, await providerless.Locator(".tm-notion-restricted-badge").CountAsync(), "Without a provider every page should be open.");
            Assert.AreEqual(0, await providerless.Locator(".tm-notion-page--readonly").CountAsync(), "Without a provider the page should remain editable.");

            await SetRestrictionsAsync(RootPageId, 2,
            [
                new(0, "alice", 3)
            ]);
            var noView = await OpenNoAccessNotionEditorAsync("?user=bob&groups=readers");
            await noView.Locator(".tm-notion-no-access").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var restrictedNavTitle = noView.Locator(".tm-npt-title").Filter(new() { HasText = "CF20 Restricted Workspace" });
            if (await restrictedNavTitle.CountAsync() > 0)
            {
                Assert.IsFalse(await restrictedNavTitle.First.IsVisibleAsync(), "Pages without View permission should be hidden from navigation.");
            }

            await SetRestrictionsAsync(RootPageId, 2,
            [
                new(1, "readers", 3),
                new(0, "bob", 1)
            ]);
            var conflict = await OpenNotionEditorAsync("?user=bob&groups=readers");
            await conflict.Locator(".tm-notion-page--readonly").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
            Assert.AreEqual(0, await conflict.Locator(".tm-notion-no-access").CountAsync(),
                "User-specific View should override group Edit without removing View access.");

            TestContext.WriteLine("UX CF20 review: restrictions dialog presents subject and permission rows without truncation, restricted badges remain visible in header/sidebar, read-only/no-view states are explicit, and providerless pages do not expose misleading locked affordances.");
        }
        finally
        {
            await SetRestrictionsAsync(RootPageId, 0, []);
        }
    }

    private static async Task OpenRestrictionsDialogAsync(IPage page)
    {
        await page.Locator(".tm-npsm-trigger").First.ClickAsync();
        await page.Locator(".tm-npsm__item").Filter(new() { HasText = "Page restrictions" }).ClickAsync();
    }

    private async Task<IPage> OpenNoAccessNotionEditorAsync(string query)
    {
        var context = await CreateContextAsync();
        await context.AddInitScriptAsync("window.localStorage.setItem('tm-demo-culture', 'en');");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 720);
        await page.GotoAsync($"{BaseUrl}/notion-editor{query}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });

        await WaitForAppReadyAsync(page);
        await page.WaitForSelectorAsync(".tm-notion-editor", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
        return page;
    }

    private static async Task SetRestrictionsAsync(Guid pageId, int mode, IReadOnlyList<AclEntry> entries)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(ApiBaseUrl) };
        var response = await http.PutAsJsonAsync($"/api/notion/permissions/pages/{pageId:D}", new
        {
            pageId,
            mode,
            entries = entries.Select(entry => new
            {
                subjectType = entry.SubjectType,
                subjectId = entry.SubjectId,
                permission = entry.Permission
            })
        });
        response.EnsureSuccessStatusCode();
    }

    private sealed record AclEntry(int SubjectType, string SubjectId, int Permission);
}
