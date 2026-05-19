using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class DocumentEditorPhase8FloatingFocusE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase8_MiniToolbarStaysInsideDesktopViewport()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        await SelectFirstInlineRangeAsync(page, 0, 8);
        await Assertions.Expect(page.Locator("[data-testid='document-mini-toolbar']")).ToBeVisibleAsync();

        var bounds = await ReadElementBoundsAsync(page, "[data-testid='document-mini-toolbar']");
        Assert.IsTrue(bounds.Left >= 0, "Mini toolbar should not overflow the left viewport edge.");
        Assert.IsTrue(bounds.Right <= 1440, "Mini toolbar should not overflow the right viewport edge.");
        Assert.IsTrue(bounds.Top >= 0, "Mini toolbar should not overflow the top viewport edge.");
        Assert.IsTrue(bounds.Bottom <= 900, "Mini toolbar should not overflow the bottom viewport edge.");
    }

    [TestMethod]
    public async Task Phase8_MiniToolbarStaysInsideNarrowViewport()
    {
        var page = await OpenDocumentEditorAsync(width: 390, height: 760);
        await WaitForWysiwygBodyAsync(page);

        await SelectFirstInlineRangeAsync(page, 0, 8);
        await Assertions.Expect(page.Locator("[data-testid='document-mini-toolbar']")).ToBeVisibleAsync();

        var bounds = await ReadElementBoundsAsync(page, "[data-testid='document-mini-toolbar']");
        Assert.IsTrue(bounds.Left >= 0, "Mini toolbar should not overflow the left narrow viewport edge.");
        Assert.IsTrue(bounds.Right <= 390, "Mini toolbar should not overflow the right narrow viewport edge.");
    }

    [TestMethod]
    public async Task Phase8_FindUpdatesEditorLiveRegion()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);
        var body = await WaitForWysiwygBodyAsync(page);

        await body.ClickAsync();
        await page.Keyboard.PressAsync("Control+f");
        await Assertions.Expect(page.Locator("[data-testid='document-find-panel']")).ToBeVisibleAsync();
        await page.Locator("[data-testid='document-find-input']").FillAsync("agreement");

        await Assertions.Expect(page.Locator("[data-testid='document-editor-live-region']")).ToContainTextAsync("1 of");
    }

    [TestMethod]
    public async Task Phase8_EscapeClosesFindBeforeSidePanelAndRestoresEditorFocus()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);
        var body = await WaitForWysiwygBodyAsync(page);

        await body.ClickAsync();
        await page.Keyboard.PressAsync("Control+f");
        await Assertions.Expect(page.Locator("[data-testid='document-find-panel']")).ToBeVisibleAsync();

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("[data-testid='document-find-panel']")).ToHaveCountAsync(0, new() { Timeout = 3000 });

        if (await page.Locator("[data-testid='document-side-panel']").CountAsync() > 0)
        {
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(page.Locator("[data-testid='document-side-panel']")).ToHaveCountAsync(0, new() { Timeout = 3000 });
        }

        Assert.IsTrue(await ActiveElementIsInWysiwygAsync(page), "Focus should return to the WYSIWYG surface after Escape closes floating UI.");
    }

    private static Task SelectFirstInlineRangeAsync(IPage page, int startOffset, int endOffset)
        => page.EvaluateAsync(
            """
            ({ startOffset, endOffset }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const inline = host?.querySelector('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-block[data-block-id] [data-inline-id]');
                const text = inline && Array.from(inline.childNodes).find(node => node.nodeType === Node.TEXT_NODE);
                if (!text) return false;

                const range = document.createRange();
                const max = (text.textContent || '').length;
                range.setStart(text, Math.min(startOffset, max));
                range.setEnd(text, Math.min(endOffset, max));
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                inline.dispatchEvent(new MouseEvent('mouseup', { bubbles: true }));
                document.dispatchEvent(new Event('selectionchange'));
                return true;
            }
            """,
            new { startOffset, endOffset });

    private static Task<ElementBounds> ReadElementBoundsAsync(IPage page, string selector)
        => page.EvaluateAsync<ElementBounds>(
            """
            selector => {
                const rect = document.querySelector(selector)?.getBoundingClientRect();
                return rect
                    ? { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom }
                    : { left: -1, top: -1, right: -1, bottom: -1 };
            }
            """,
            selector);

    private static Task<bool> ActiveElementIsInWysiwygAsync(IPage page)
        => page.EvaluateAsync<bool>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                return !!host && (document.activeElement === host || host.contains(document.activeElement));
            }
            """);

    private sealed class ElementBounds
    {
        [JsonPropertyName("left")]
        public double Left { get; set; }

        [JsonPropertyName("top")]
        public double Top { get; set; }

        [JsonPropertyName("right")]
        public double Right { get; set; }

        [JsonPropertyName("bottom")]
        public double Bottom { get; set; }
    }
}
