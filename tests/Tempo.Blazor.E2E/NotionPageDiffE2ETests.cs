using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class NotionPageDiffE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF23: page history captures inline, side-by-side, large, no-change, and mobile responsive diff states.")]
    public async Task CF23_PageHistoryDiffViewer_InlineSideBySideAndNoChange()
    {
        await OpenNotionEditorAsync(1280, 900);
        await SeedHistoryDiffPageAsync();
        var panel = await OpenLargeDiffAsync(Page);

        var diffViewer = panel.Locator("[data-testid='notion-diff-viewer']").First;

        Assert.IsTrue(await panel.Locator(".tm-ndv__entry--added").CountAsync() >= 12, "The seeded large diff should render many added entries.");
        Assert.IsTrue(await panel.Locator(".tm-ndv__entry--removed").CountAsync() >= 1, "The diff should include a removed block.");
        Assert.IsTrue(await panel.Locator(".tm-ndv__entry--modified").CountAsync() >= 1, "The diff should include a modified block.");
        Assert.IsTrue(await panel.Locator(".tm-ndv__entry--moved").CountAsync() >= 1, "The diff should include a moved block.");

        await CaptureBaselineAsync("page-diff", "cf23-inline-diff", panel);
        await CaptureBaselineAsync("page-diff", "cf23-inline-large-diff", panel);

        await diffViewer.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
        await panel.Locator(".tm-ndv__entry--added").Last.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await CaptureBaselineAsync("page-diff", "cf23-large-diff-scrolled", panel);

        await diffViewer.EvaluateAsync("el => el.scrollTop = 0");
        await panel.Locator("[data-testid='notion-diff-mode-side-by-side']").ClickAsync();
        await panel.Locator(".tm-ndv--sidebyside .tm-ndv__pane--before").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await panel.Locator(".tm-ndv--sidebyside .tm-ndv__pane--after").First.IsVisibleAsync(), "Side-by-side diff should render the after pane.");

        await CaptureBaselineAsync("page-diff", "cf23-side-by-side-diff", panel);

        await panel.Locator(".tm-nph__toolbar-btn--secondary").Filter(new LocatorFilterOptions { HasText = "Exit comparison" }).First.ClickAsync();
        await panel.Locator(".tm-nph__preview").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var versionItems = panel.Locator(".tm-nph__version-item");
        await panel.Locator(".tm-nph__toolbar-btn--secondary").Filter(new LocatorFilterOptions { HasText = "Compare" }).First.ClickAsync();
        await versionItems.Nth(3).ClickAsync();
        await diffViewer.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await panel.Locator(".tm-ndv__empty").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        Assert.AreEqual(0, await panel.Locator(".tm-ndv__entry").CountAsync(), "Equivalent snapshots should not render diff entries.");
        await Expect(panel.Locator(".tm-ndv__empty")).ToContainTextAsync("No differences found");

        await CaptureBaselineAsync("page-diff", "cf23-no-change-diff", panel);

        var mobile = await OpenNotionEditorAsync(390, 844);
        await SeedHistoryDiffPageAsync();
        var mobilePanel = await OpenLargeDiffAsync(mobile);
        await mobilePanel.Locator("[data-testid='notion-diff-mode-side-by-side']").ClickAsync();
        await mobilePanel.Locator(".tm-ndv--sidebyside .tm-ndv__pane--before").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await CaptureBaselineAsync("page-diff", "cf23-side-by-side-mobile", mobilePanel);
    }

    private static async Task<ILocator> OpenLargeDiffAsync(IPage page)
    {
        var panel = await OpenPageHistoryAsync(page);
        var versionItems = panel.Locator(".tm-nph__version-item");
        await versionItems.Nth(3).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await versionItems.Nth(2).ClickAsync();
        await panel.Locator(".tm-nph__toolbar-btn--secondary").Filter(new LocatorFilterOptions { HasText = "Compare" }).First.ClickAsync();
        await versionItems.Nth(0).ClickAsync();

        var diffViewer = panel.Locator("[data-testid='notion-diff-viewer']").First;
        await diffViewer.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await panel.Locator(".tm-ndv__entry--added").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        return panel;
    }

    private static async Task<ILocator> OpenPageHistoryAsync(IPage page)
    {
        var trigger = page.Locator(".tm-npsm-trigger").First;
        await trigger.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await trigger.ClickAsync();

        var menu = page.Locator(".tm-npsm").First;
        await menu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var historyItem = menu.Locator(".tm-npsm__item")
            .Filter(new LocatorFilterOptions { HasText = "Page history" })
            .First;
        await historyItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await historyItem.ClickAsync();

        var panel = page.Locator(".tm-nph").First;
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        return panel;
    }

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
}
