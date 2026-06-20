using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionSmartLinksE2ETests : NotionE2ETestBase
{
    private const string KnownUrl = "https://docs.tempo.local/notion/special-blocks";
    private const string LongTitleUrl = "https://docs.tempo.local/notion/very-long-smart-link-title";
    private const string ResolverFailUrl = "https://resolver-fail.tempo.local/failure";

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Smart Links paste menu inserts inline previews, creates bookmark cards, and falls back to plain links for invalid/providerless/failing resolver cases.")]
    public async Task SmartLinks_PasteInlineCardAndFallbacks()
    {
        var page = await OpenNotionEditorAsync();
        await SeedSmartLinksPageAsync();
        await DelaySmartLinkResolverAsync(page);

        var inlineBlock = page.Locator("[data-block-id='cf800000-0000-0000-0000-000000000010'] .tm-notion-editable").First;
        await PasteTextAsync(inlineBlock, KnownUrl);
        var pasteMenu = page.Locator(".tm-notion-smart-link-menu").First;
        await pasteMenu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await pasteMenu.EvaluateAsync<bool>("menu => menu.parentElement === document.body"),
            "Paste-as menu should be rendered outside the contenteditable block.");
        await CaptureBaselineAndAssertAsync("cf8-paste-menu", pasteMenu);

        await page.Locator(".tm-notion-smart-link-menu__item").Filter(new() { HasText = "Paste as inline preview" }).ClickAsync();
        await page.Locator(".tm-notion-smart-link-menu--loading").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await CaptureBaselineAndAssertAsync("cf8-paste-menu-loading", pasteMenu);

        var inlineChip = inlineBlock.Locator(".tm-notion-smart-link").Filter(new() { HasText = "Tempo Notion special blocks" });
        await inlineChip.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await page.UnrouteAsync("**/api/notion/smart-links/resolve");
        Assert.AreEqual(1, await inlineChip.Locator(".tm-notion-smart-link__favicon").CountAsync());
        Assert.IsTrue(await inlineChip.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Inline smart link should truncate instead of overflowing.");
        Assert.AreEqual(0, await page.Locator(".tm-notion-smart-link-menu").CountAsync());
        await CaptureBaselineAndAssertAsync("cf8-inline-preview-chip", inlineBlock);

        var inlineHtml = await inlineBlock.EvaluateAsync<string>("el => el.innerHTML");
        Assert.IsFalse(inlineHtml.Contains("tm-notion-smart-link-menu", StringComparison.Ordinal),
            "Smart-link menu markup must not be persisted inside the editable block.");

        var cardBlock = page.Locator("[data-block-id='cf800000-0000-0000-0000-000000000020'] .tm-notion-editable").First;
        await PasteTextAsync(cardBlock, KnownUrl);
        await page.Locator(".tm-notion-smart-link-menu__item").Filter(new() { HasText = "Paste as card" }).ClickAsync();
        var card = page.Locator(".tm-notion-bookmark-block__card").Filter(new() { HasText = "Tempo Notion special blocks" }).First;
        await card.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await card.Locator(".tm-notion-bookmark-block__favicon").IsVisibleAsync());
        await CaptureBaselineAndAssertAsync("cf8-card-preview", card);

        var fallbackBlock = page.Locator("[data-block-id='cf800000-0000-0000-0000-000000000030'] .tm-notion-editable").First;
        await PasteTextAsync(fallbackBlock, "not a valid url");
        await ExpectTextAsync(fallbackBlock, "not a valid url");

        await PasteTextAsync(fallbackBlock, ResolverFailUrl);
        await page.Locator(".tm-notion-smart-link-menu__item").Filter(new() { HasText = "Paste as inline preview" }).ClickAsync();
        var resolverFallback = fallbackBlock.Locator($"a[href='{ResolverFailUrl}']");
        await resolverFallback.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await Assertions.Expect(fallbackBlock).ToContainTextAsync($"not a valid url {ResolverFailUrl}", new LocatorAssertionsToContainTextOptions
        {
            Timeout = 10000
        });
        await CaptureBaselineAndAssertAsync("cf8-resolver-error-fallback", fallbackBlock);

        await PasteTextAsync(fallbackBlock, LongTitleUrl);
        await page.Locator(".tm-notion-smart-link-menu").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await TakeScreenshotAsync(page, "notion_smart_links_paste_menu");
        await page.Locator(".tm-notion-smart-link-menu__item").Filter(new() { HasText = "Paste as inline preview" }).ClickAsync();
        var longChip = fallbackBlock.Locator(".tm-notion-smart-link").Filter(new() { HasText = "Tempo Notion Smart Link Preview" }).First;
        await longChip.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await longChip.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Long smart link title should be visually truncated.");

        await TakeScreenshotAsync(page, "notion_smart_links_inline_card");
    }

    [TestMethod]
    [Description("Smart Link paste-as menu closes via Escape and outside click without leaking menu markup or handlers.")]
    public async Task SmartLinks_PasteMenu_ClosesWithoutPersistingMenuMarkup()
    {
        var page = await OpenNotionEditorAsync();
        await SeedSmartLinksPageAsync();

        var block = page.Locator("[data-block-id='cf800000-0000-0000-0000-000000000010'] .tm-notion-editable").First;

        await PasteTextAsync(block, KnownUrl);
        await page.Locator(".tm-notion-smart-link-menu").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await page.Keyboard.PressAsync("Escape");
        await page.Locator(".tm-notion-smart-link-menu").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 10000 });

        await PasteTextAsync(block, KnownUrl);
        await page.Locator(".tm-notion-smart-link-menu").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await page.Mouse.ClickAsync(12, 12);
        await page.Locator(".tm-notion-smart-link-menu").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 10000 });

        await PasteTextAsync(block, KnownUrl);
        await page.Locator(".tm-notion-smart-link-menu").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await page.Locator(".tm-notion-smart-link-menu__item").Filter(new() { HasText = "Paste as plain link" }).ClickAsync();
        await block.Locator($"a[href='{KnownUrl}']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        Assert.AreEqual(0, await page.Locator(".tm-notion-smart-link-menu").CountAsync());
        var html = await block.EvaluateAsync<string>("el => el.innerHTML");
        Assert.IsFalse(html.Contains("tm-notion-smart-link-menu", StringComparison.Ordinal),
            "Smart-link menu markup must not be persisted inside the editable block.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("When Smart Link provider is disabled, pasted URLs are inserted as plain links without opening the paste-as menu.")]
    public async Task SmartLinks_WithoutProvider_PastesPlainLink()
    {
        var page = await OpenNotionEditorAsync("?disableSmartLinkProvider=true");
        await SeedSmartLinksPageAsync();

        var block = page.Locator("[data-block-id='cf800000-0000-0000-0000-000000000010'] .tm-notion-editable").First;
        await PasteTextAsync(block, KnownUrl);

        await block.Locator($"a[href='{KnownUrl}']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.AreEqual(0, await page.Locator(".tm-notion-smart-link-menu").CountAsync());
        Assert.AreEqual(0, await block.Locator(".tm-notion-smart-link").CountAsync());
        await CaptureBaselineAndAssertAsync("cf8-no-provider-plain-link", block);
    }

    private static async Task DelaySmartLinkResolverAsync(IPage page)
    {
        await page.RouteAsync("**/api/notion/smart-links/resolve", async route =>
        {
            await Task.Delay(5000);
            await route.ContinueAsync();
        });
    }

    private async Task CaptureBaselineAndAssertAsync(string state, ILocator region)
    {
        await region.ScrollIntoViewIfNeededAsync();
        var capture = await CaptureBaselineAsync("smart-links", state, region);
        Assert.IsTrue(File.Exists(capture.FullPagePath), $"CF8 full-page baseline should be written for {state}.");
        Assert.IsTrue(File.Exists(capture.RegionPath), $"CF8 region baseline should be written for {state}.");
    }

    private static async Task PasteTextAsync(ILocator editable, string text)
    {
        await editable.FocusAsync();
        await editable.EvaluateAsync(
            @"(el, value) => {
                const range = document.createRange();
                range.selectNodeContents(el);
                range.collapse(false);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);

                const data = new DataTransfer();
                data.setData('text/plain', value);
                el.dispatchEvent(new ClipboardEvent('paste', {
                    clipboardData: data,
                    bubbles: true,
                    cancelable: true
                }));
            }",
            text);
    }

    private static async Task ExpectTextAsync(ILocator locator, string expected)
    {
        await Assertions.Expect(locator).ToContainTextAsync(expected, new LocatorAssertionsToContainTextOptions
        {
            Timeout = 10000
        });
    }
}
