using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionShortcutsE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF18: Keyboard shortcuts panel opens with ?, closes with Escape, ignores ? typed inside editable blocks, and captures a UX baseline.")]
    public async Task ShortcutsPanel_OpenCloseAndTypingEdge()
    {
        var page = await OpenNotionEditorAsync();
        await SeedRichPageAsync();

        Assert.IsTrue(await page.EvaluateAsync<bool>("() => typeof window.tmNotionEditor?.registerShortcuts === 'function'"),
            "The Notion shortcuts JS registration function must be available.");
        Assert.AreEqual(1, await page.Locator(".tm-notion-topbar__shortcuts").CountAsync(),
            "The shortcuts trigger should render in the Notion editor top bar.");

        await page.Locator(".tm-notion-topbar__title").First.ClickAsync();
        await DispatchQuestionShortcutAsync(page);

        var panel = page.Locator(".tm-nsp__dialog").First;
        await panel.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await panel.Locator("[data-shortcut-action='OpenShortcuts']").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await panel.Locator("[data-shortcut-action='SlashMenu']").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.IsTrue(await panel.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Shortcuts panel should not overflow horizontally.");

        var capture = await CaptureBaselineAsync("shortcuts", "cf18-shortcuts-panel-baseline", panel);
        TestContext.WriteLine($"UX CF18 baseline captured: {capture.FullPagePath} / {capture.RegionPath}");

        await page.Keyboard.PressAsync("Escape");
        await page.Locator(".tm-nsp__dialog").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10000
        });

        var editable = page.Locator(".tm-notion-editable").First;
        await editable.ClickAsync();
        await editable.EvaluateAsync("""
            el => el.dispatchEvent(new KeyboardEvent('keydown', {
                key: '?',
                bubbles: true,
                cancelable: true
            }))
            """);
        await page.WaitForTimeoutAsync(500);

        Assert.AreEqual(0, await page.Locator(".tm-nsp__dialog").CountAsync(),
            "Typing ? inside a Notion editable block must not open the shortcuts panel.");
    }

    private static async Task DispatchQuestionShortcutAsync(IPage page)
        => await page.EvaluateAsync("""
            () => document.dispatchEvent(new KeyboardEvent('keydown', {
                key: '?',
                bubbles: true,
                cancelable: true
            }))
            """);
}
