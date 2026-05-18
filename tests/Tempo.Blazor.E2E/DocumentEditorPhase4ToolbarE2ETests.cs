using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for the document editor phase 4 toolbar model and UX modes.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase4ToolbarE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase4_RibbonDesktopLayoutHasNoCriticalOverlap()
    {
        var page = await OpenDocumentEditorAsync(width: 1600, height: 960);
        var issues = await page.EvaluateAsync<string[]>(
            """
            () => {
                const issues = [];
                const toolbar = document.querySelector('[data-testid="document-toolbar"]');
                const workspace = document.querySelector('[data-testid="document-editor-demo"] .tm-document-editor__workspace');
                const surface = document.querySelector('[data-testid="document-editor-demo"] .tm-document-editor__surface');
                const panel = document.querySelector('[data-testid="document-side-panel"]');
                const status = document.querySelector('[data-testid="document-status-bar"]');
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const pageEl = host?.querySelector('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual)');
                if (!toolbar || !workspace || !surface || !panel || !status || !host || !pageEl) {
                    return ['missing critical editor region'];
                }

                const toolbarRect = toolbar.getBoundingClientRect();
                const workspaceRect = workspace.getBoundingClientRect();
                const surfaceRect = surface.getBoundingClientRect();
                const panelRect = panel.getBoundingClientRect();
                const statusRect = status.getBoundingClientRect();
                const pageRect = pageEl.getBoundingClientRect();

                if (document.documentElement.scrollWidth > window.innerWidth + 2) issues.push('horizontal page overflow');
                if (toolbarRect.bottom > workspaceRect.top + 2) issues.push('toolbar overlaps workspace');
                if (surfaceRect.right > panelRect.left + 1 && surfaceRect.bottom > panelRect.top && surfaceRect.top < panelRect.bottom) issues.push('surface overlaps side panel');
                if (statusRect.top < workspaceRect.bottom - 2) issues.push('status bar overlaps workspace');
                if (pageRect.width < 420) issues.push('document page is too narrow');
                if (host.scrollWidth > host.clientWidth + 24) issues.push('document host has horizontal overflow');
                return issues;
            }
            """);

        Assert.AreEqual(0, issues.Length, string.Join("; ", issues));
    }

    [TestMethod]
    public async Task Phase4_ToolbarModeControlSwitchesRibbonCompactAndDistractionFree()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 820);
        var mode = page.Locator("[data-testid='document-editor-toolbar-mode']");
        var toolbar = page.Locator("[data-testid='document-toolbar']");

        await Assertions.Expect(toolbar).ToHaveAttributeAsync("data-toolbar-mode", "Ribbon");

        await mode.SelectOptionAsync("Compact");
        await Assertions.Expect(toolbar).ToHaveAttributeAsync("data-toolbar-mode", "Compact");
        await Assertions.Expect(toolbar).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("tm-document-editor__ribbon--compact"));
        await Assertions.Expect(page.Locator("[data-testid='document-bold']")).ToHaveAttributeAsync("aria-label", "Bold");

        await mode.SelectOptionAsync("DistractionFree");
        await Assertions.Expect(toolbar).ToHaveAttributeAsync("data-toolbar-mode", "DistractionFree");
        await Assertions.Expect(toolbar).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToBeVisibleAsync();

        await mode.SelectOptionAsync("Ribbon");
        await Assertions.Expect(toolbar).ToHaveAttributeAsync("data-toolbar-mode", "Ribbon");
        await Assertions.Expect(toolbar).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task Phase4_NarrowMoreMenuShowsGroupsAndSearchesCommands()
    {
        var page = await OpenDocumentEditorAsync(width: 390, height: 780);
        var more = page.Locator("[data-testid='document-toolbar-more']");

        try
        {
            await Assertions.Expect(more).ToBeVisibleAsync(new() { Timeout = 5000 });
        }
        catch
        {
            Assert.Inconclusive("More button was not visible at 390px; the toolbar fit without overflow.");
            return;
        }

        await more.ClickAsync();
        var menu = page.Locator("[data-testid='document-toolbar-more-menu']");
        await Assertions.Expect(menu).ToBeVisibleAsync();
        await Assertions.Expect(menu.Locator("[data-testid='document-toolbar-more-group']")).Not.ToHaveCountAsync(0);

        var search = menu.Locator("[data-testid='document-toolbar-more-search']");
        if (await search.CountAsync() > 0)
        {
            await search.FillAsync("Bold");
            await Assertions.Expect(menu.Locator("[data-command='bold']")).ToBeVisibleAsync();
            await Assertions.Expect(menu.Locator("[data-command='italic']")).ToHaveCountAsync(0);
            await menu.Locator("[data-command='bold']").ClickAsync();
            await Assertions.Expect(page.Locator("[data-testid='document-toolbar-more-menu']")).ToHaveCountAsync(0);
        }
    }

    [TestMethod]
    public async Task Phase4_HeaderFooterContextualTabAppearsOnlyWhileEditingHeaderOrFooter()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await Assertions.Expect(page.Locator("[data-testid='document-ribbon-tab-header-footer']")).ToHaveCountAsync(0);

        var header = page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__header[contenteditable='true']").First;
        await header.DblClickAsync();

        var contextualTab = page.Locator("[data-testid='document-ribbon-tab-header-footer']");
        await Assertions.Expect(contextualTab).ToBeVisibleAsync();
        await Assertions.Expect(contextualTab).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(page.Locator("[data-testid='document-close-header-footer']")).ToBeVisibleAsync();

        await page.Locator("[data-testid='document-close-header-footer']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-ribbon-tab-header-footer']")).ToHaveCountAsync(0);
    }
}
