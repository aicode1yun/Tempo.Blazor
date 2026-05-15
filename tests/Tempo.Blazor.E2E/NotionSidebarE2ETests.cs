using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E tests for the Notion editor sidebar and page-tree navigation.
/// Pre-seeded data: 4 root pages (2 favorites), "Engineering Wiki" has 2 sub-pages.
/// </summary>
[TestClass]
public class NotionSidebarE2ETests : WasmTestBase
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<IPage> OpenNotionEditorAsync()
    {
        using var http = new HttpClient();
        try { await http.PostAsync("https://localhost:5100/api/notion/reset", null); }
        catch { /* ignore if API unavailable or cert untrusted */ }

        var context = await CreateContextAsync();
        var page    = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/notion-editor");
        await WaitForAppReadyAsync(page);
        await page.WaitForSelectorAsync(".tm-notion-editor", new PageWaitForSelectorOptions { Timeout = 30000 });
        await page.WaitForTimeoutAsync(2000);
        return page;
    }

    /// <summary>Waits until the sidebar page tree is fully loaded and at least one item is visible.</summary>
    private async Task WaitForSidebarReadyAsync(IPage page)
    {
        var firstItem = page.Locator(".tm-npt-item").First;
        await firstItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Sidebar visibility & toggle
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Sidebar is visible on initial load when ShowSidebar=true")]
    public async Task Sidebar_IsVisible_WhenShowSidebarTrue()
    {
        var page    = await OpenNotionEditorAsync();
        var sidebar = page.Locator(".tm-notion-sidebar").First;
        await sidebar.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await sidebar.IsVisibleAsync(), "Sidebar should be visible on load");

        await TakeScreenshotAsync(page, "sidebar_visible");
    }

    [TestMethod]
    [Description("Clicking the sidebar toggle button hides the sidebar")]
    public async Task Sidebar_Toggle_HidesSidebar()
    {
        var page   = await OpenNotionEditorAsync();
        var toggle = page.Locator(".tm-notion-sidebar-toggle").First;
        await toggle.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await toggle.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        // After toggle, sidebar should have --hidden class (not --visible)
        var visibleCount = await page.Locator(".tm-notion-sidebar--visible").CountAsync();
        Assert.AreEqual(0, visibleCount, "Sidebar --visible class should be gone after toggling");

        await TakeScreenshotAsync(page, "sidebar_hidden");
    }

    [TestMethod]
    [Description("Clicking the sidebar toggle twice shows the sidebar again")]
    public async Task Sidebar_Toggle_ShowsSidebar()
    {
        var page   = await OpenNotionEditorAsync();
        var toggle = page.Locator(".tm-notion-sidebar-toggle").First;
        await toggle.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        // First click → hide
        await toggle.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        // Second click → show again
        await toggle.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var visibleCount = await page.Locator(".tm-notion-sidebar--visible").CountAsync();
        Assert.IsTrue(visibleCount > 0, "Sidebar should be visible again after clicking toggle a second time");

        await TakeScreenshotAsync(page, "sidebar_reshown");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Page tree
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("The page tree lists at least two root pages")]
    public async Task Sidebar_PageTree_ListsPages()
    {
        var page = await OpenNotionEditorAsync();
        await WaitForSidebarReadyAsync(page);

        var itemCount = await page.Locator(".tm-npt-item").CountAsync();
        Assert.IsTrue(itemCount >= 2, $"Page tree should have at least 2 items, found {itemCount}");

        await TakeScreenshotAsync(page, "sidebar_page_tree");
    }

    [TestMethod]
    [Description("Clicking a page title in the tree navigates the editor to that page")]
    public async Task Sidebar_PageTree_ClickPage_NavigatesToPage()
    {
        var page = await OpenNotionEditorAsync();
        await WaitForSidebarReadyAsync(page);

        // Click "Product Roadmap" — different from the initial "Getting Started" page
        var roadmapTitle = page.Locator(".tm-npt-title")
                               .Filter(new LocatorFilterOptions { HasText = "Product Roadmap" })
                               .First;
        await roadmapTitle.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        await roadmapTitle.ClickAsync();

        // Topbar title should update
        var topbarTitle = page.Locator(".tm-notion-topbar__title").First;
        await topbarTitle.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        await page.WaitForTimeoutAsync(1000);
        var titleText = await topbarTitle.InnerTextAsync();
        Assert.IsTrue(
            titleText.Contains("Product Roadmap", StringComparison.OrdinalIgnoreCase),
            $"Topbar title should be 'Product Roadmap', got '{titleText}'");

        await TakeScreenshotAsync(page, "sidebar_navigate_to_page");
    }

    [TestMethod]
    [Description("Clicking the expand toggle on Engineering Wiki reveals its sub-pages")]
    public async Task Sidebar_PageTree_ExpandCollapse_SubPages()
    {
        var page = await OpenNotionEditorAsync();
        await WaitForSidebarReadyAsync(page);

        // Find the Engineering Wiki tree item
        var engWikiItem = page.Locator(".tm-npt-item")
                              .Filter(new LocatorFilterOptions { HasText = "Engineering Wiki" })
                              .First;
        await engWikiItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });

        // Click the expand/collapse toggle
        var expandToggle = engWikiItem.Locator(".tm-npt-toggle").First;
        await expandToggle.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        // Children list should appear
        var children = engWikiItem.Locator(".tm-npt-children").First;
        await children.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        Assert.IsTrue(await children.IsVisibleAsync(), "Sub-pages should be visible after expanding Engineering Wiki");

        var childCount = await children.Locator(".tm-npt-item").CountAsync();
        Assert.IsTrue(childCount >= 1, $"Engineering Wiki should have at least 1 child page, found {childCount}");

        await TakeScreenshotAsync(page, "sidebar_subtree_expanded");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Favorites
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Favorites section shows the favorited pages (Getting Started & Product Roadmap)")]
    public async Task Sidebar_Favorites_ShowsFavoritePages()
    {
        var page = await OpenNotionEditorAsync();

        // Favorites section renders when _favorites.Count > 0
        var favSection = page.Locator(".tm-nsf").First;
        await favSection.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await favSection.IsVisibleAsync(), "Favorites section should be visible");

        // Items are expanded by default (_isExpanded = true)
        var favItems = favSection.Locator(".tm-nsf-item");
        await favItems.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var itemCount = await favItems.CountAsync();
        Assert.IsTrue(itemCount >= 1, $"Favorites should have at least 1 item, found {itemCount}");

        await TakeScreenshotAsync(page, "sidebar_favorites");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Sidebar search
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Typing in the sidebar search filters pages and shows matching results")]
    public async Task Sidebar_Search_FiltersPageTree()
    {
        var page = await OpenNotionEditorAsync();
        await WaitForSidebarReadyAsync(page);

        // Open the sidebar search panel
        var searchBtn = page.Locator(".tm-ns-btn-search").First;
        await searchBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await searchBtn.ClickAsync();

        // Type a query
        var searchInput = page.Locator(".tm-ns-search__input").First;
        await searchInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await searchInput.FillAsync("Engineering");
        await page.WaitForTimeoutAsync(1500);

        // Results should include "Engineering Wiki"
        var results = page.Locator(".tm-ns-search__result");
        await results.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        var resultCount = await results.CountAsync();
        Assert.IsTrue(resultCount >= 1, $"Search for 'Engineering' should return at least 1 result, got {resultCount}");

        await TakeScreenshotAsync(page, "sidebar_search_results");
    }
}
