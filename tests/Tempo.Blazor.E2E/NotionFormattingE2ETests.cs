using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E tests covering the inline text formatting toolbar in the Notion editor:
/// bold, italic, underline, strikethrough, inline code, links, colors, and Turn Into.
/// </summary>
[TestClass]
public class NotionFormattingE2ETests : WasmTestBase
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<IPage> OpenNotionEditorAsync()
    {
        // Reset API mock data so each test starts with a clean slate
        using var http = new HttpClient();
        try { await http.PostAsync("https://localhost:5100/api/notion/reset", null); }
        catch { /* API may not be running; ignore */ }

        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/notion-editor");
        await WaitForAppReadyAsync(page);
        await page.WaitForSelectorAsync(".tm-notion-editor", new PageWaitForSelectorOptions { Timeout = 30000 });
        await page.WaitForTimeoutAsync(2000);
        return page;
    }

    private async Task SelectTextInBlockAsync(IPage page, ILocator block)
    {
        await block.ClickAsync();
        await block.EvaluateAsync("el => { el.focus(); document.execCommand('selectAll', false, null); }");
        await page.WaitForTimeoutAsync(300);
    }

    private async Task WaitForInlineToolbarAsync(IPage page)
    {
        var toolbar = page.Locator(".tm-notion-inline-toolbar").First;
        await toolbar.WaitForAsync(new LocatorWaitForOptions
            { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    private async Task OpenInlineToolbarAsync(IPage page)
    {
        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        await block.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await SelectTextInBlockAsync(page, block);
        await WaitForInlineToolbarAsync(page);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Visibility
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Selecting text in a block shows the inline formatting toolbar")]
    public async Task InlineToolbar_SelectText_Shows()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var toolbar = page.Locator(".tm-notion-inline-toolbar").First;
        Assert.IsTrue(await toolbar.IsVisibleAsync(), "Inline toolbar should be visible after text selection");
        await TakeScreenshotAsync(page, "inline_toolbar_show");
    }

    [TestMethod]
    [Description("Clicking outside the inline toolbar hides it")]
    public async Task InlineToolbar_ClickOutside_Hides()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        // Click on another block (e.g. the page title or second paragraph)
        var otherBlock = page.Locator(".tm-notion-paragraph[contenteditable='true']").Nth(1);
        if (await otherBlock.CountAsync() == 0)
            otherBlock = page.Locator(".tm-notion-page").First;
        await otherBlock.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var toolbar = page.Locator(".tm-notion-inline-toolbar").First;
        Assert.IsFalse(await toolbar.IsVisibleAsync(), "Inline toolbar should hide after clicking outside");
        await TakeScreenshotAsync(page, "inline_toolbar_hide");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Basic formatting (click)
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Clicking the Bold button wraps selected text in a bold tag")]
    public async Task InlineToolbar_Bold_AppliesBold()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var boldBtn = page.Locator("button[title='Bold']").First;
        await boldBtn.ClickAsync();
        await page.WaitForTimeoutAsync(400);

        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        var html = await block.InnerHTMLAsync();
        Assert.IsTrue(
            html.Contains("<b>") || html.Contains("<strong>"),
            $"Selected text should be wrapped in bold tag. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_bold");
    }

    [TestMethod]
    [Description("Clicking the Italic button wraps selected text in an italic tag")]
    public async Task InlineToolbar_Italic_AppliesItalic()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var italicBtn = page.Locator("button[title='Italic']").First;
        await italicBtn.ClickAsync();
        await page.WaitForTimeoutAsync(400);

        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        var html = await block.InnerHTMLAsync();
        Assert.IsTrue(
            html.Contains("<i>") || html.Contains("<em>"),
            $"Selected text should be wrapped in italic tag. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_italic");
    }

    [TestMethod]
    [Description("Clicking the Underline button applies underline style to selected text")]
    public async Task InlineToolbar_Underline_AppliesUnderline()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var underlineBtn = page.Locator("button[title='Underline']").First;
        await underlineBtn.ClickAsync();
        await page.WaitForTimeoutAsync(400);

        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        var html = await block.InnerHTMLAsync();
        Assert.IsTrue(
            html.Contains("<u>") || html.Contains("text-decoration: underline"),
            $"Selected text should have underline style. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_underline");
    }

    [TestMethod]
    [Description("Clicking the Strikethrough button applies strikethrough style to selected text")]
    public async Task InlineToolbar_Strikethrough_AppliesStrike()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var strikeBtn = page.Locator("button[title='Strikethrough']").First;
        await strikeBtn.ClickAsync();
        await page.WaitForTimeoutAsync(400);

        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        var html = await block.InnerHTMLAsync();
        Assert.IsTrue(
            html.Contains("<s>") || html.Contains("<strike>") || html.Contains("<del>") || html.Contains("line-through"),
            $"Selected text should have strikethrough style. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_strikethrough");
    }

    [TestMethod]
    [Description("Clicking the Code button wraps selected text in a code element")]
    public async Task InlineToolbar_InlineCode_AppliesCode()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var codeBtn = page.Locator("button[title='Inline code']").First;
        await codeBtn.ClickAsync();
        await page.WaitForTimeoutAsync(400);

        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        var html = await block.InnerHTMLAsync();
        Assert.IsTrue(
            html.Contains("<code>"),
            $"Selected text should be wrapped in <code> tag. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_code");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Keyboard shortcuts
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Pressing Ctrl+B applies bold formatting to selected text")]
    public async Task InlineToolbar_Bold_Keyboard_Ctrl_B()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        await page.Keyboard.PressAsync("Control+b");
        await page.WaitForTimeoutAsync(400);

        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        var html = await block.InnerHTMLAsync();
        Assert.IsTrue(
            html.Contains("<b>") || html.Contains("<strong>"),
            $"Selected text should be bold after Ctrl+B. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_bold_kb");
    }

    [TestMethod]
    [Description("Pressing Ctrl+I applies italic formatting to selected text")]
    public async Task InlineToolbar_Italic_Keyboard_Ctrl_I()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        await page.Keyboard.PressAsync("Control+i");
        await page.WaitForTimeoutAsync(400);

        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        var html = await block.InnerHTMLAsync();
        Assert.IsTrue(
            html.Contains("<i>") || html.Contains("<em>"),
            $"Selected text should be italic after Ctrl+I. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_italic_kb");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Link
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Clicking the Link button opens the URL input panel")]
    public async Task InlineToolbar_Link_OpensInput()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var linkBtn = page.Locator("button[title='Link']").First;
        await linkBtn.ClickAsync();

        var input = page.Locator(".tm-notion-inline-toolbar__link-input").First;
        await input.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        Assert.IsTrue(await input.IsVisibleAsync(), "Link URL input should appear");
        await TakeScreenshotAsync(page, "inline_toolbar_link_open");
    }

    [TestMethod]
    [Description("Entering a URL and pressing Enter wraps selected text in an anchor tag")]
    public async Task InlineToolbar_Link_InsertUrl_WrapsWithAnchor()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var linkBtn = page.Locator("button[title='Link']").First;
        await linkBtn.ClickAsync();

        var input = page.Locator(".tm-notion-inline-toolbar__link-input").First;
        await input.FillAsync("https://example.com");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(500);

        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        var html = await block.InnerHTMLAsync();
        Assert.IsTrue(
            html.Contains("<a ") && html.Contains("href="),
            $"Selected text should be wrapped in anchor tag. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_link_insert");
    }

    [TestMethod]
    [Description("Clicking Remove link unwraps the anchor tag")]
    public async Task InlineToolbar_Link_Remove_UnwrapsAnchor()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        // Insert link first
        var linkBtn = page.Locator("button[title='Link']").First;
        await linkBtn.ClickAsync();
        var input = page.Locator(".tm-notion-inline-toolbar__link-input").First;
        await input.FillAsync("https://example.com");
        await page.Keyboard.PressAsync("Enter");

        // Re-select the linked text and open toolbar again
        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        await SelectTextInBlockAsync(page, block);
        await WaitForInlineToolbarAsync(page);

        // Open link panel again
        var linkBtn2 = page.Locator("button[title='Link']").First;
        await linkBtn2.ClickAsync();

        var input2 = page.Locator(".tm-notion-inline-toolbar__link-input").First;
        await input2.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });

        // Click Remove link
        var removeBtn = page.Locator("button[title='Remove link']").First;
        await removeBtn.ClickAsync();

        // Wait for unlink to finish and the anchor to disappear
        await page.WaitForFunctionAsync(
            "() => document.querySelectorAll('a[href=\"https://example.com\"]').length === 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var html = await block.InnerHTMLAsync();
        Assert.IsFalse(
            html.Contains("<a ") && html.Contains("href="),
            $"Anchor tag should be removed after clicking Remove link. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_link_remove");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Color
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Clicking the Color button opens the color picker panel")]
    public async Task InlineToolbar_TextColor_OpensPanel()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var colorBtn = page.Locator("button[title='Color']").First;
        await colorBtn.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        var panel = page.Locator(".tm-notion-inline-toolbar__color-panel").First;
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await panel.IsVisibleAsync(), "Color picker panel should appear");
        await TakeScreenshotAsync(page, "inline_toolbar_color_panel");
    }

    [TestMethod]
    [Description("Selecting the Red text color applies a red color style to the text")]
    public async Task InlineToolbar_TextColor_Select_AppliesColor()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var colorBtn = page.Locator("button[title='Color']").First;
        await colorBtn.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        // Click the Red swatch in the Text color row (first row)
        var redSwatch = page.Locator(".tm-notion-inline-toolbar__color-panel .tm-notion-inline-toolbar__color-row").First
            .Locator("button[title='Red']").First;
        await redSwatch.ClickAsync();
        await page.WaitForTimeoutAsync(400);

        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        var html = await block.InnerHTMLAsync();
        Assert.IsTrue(
            html.Contains("color=") || html.Contains("color:") || html.Contains("style="),
            $"Text should have a color style applied. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_text_color");
    }

    [TestMethod]
    [Description("Selecting a background color applies a background-color style to the text")]
    public async Task InlineToolbar_BgColor_Select_AppliesBackground()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var colorBtn = page.Locator("button[title='Color']").First;
        await colorBtn.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        // Click the Yellow swatch in the Background color row (second row) via JS to bypass viewport issues
        await page.EvaluateAsync("""
            const panel = document.querySelector('.tm-notion-inline-toolbar__color-panel');
            const row = panel?.querySelectorAll('.tm-notion-inline-toolbar__color-row')[1];
            const swatch = row?.querySelector('button[title="Yellow"]');
            swatch?.click();
            """);
        await page.WaitForTimeoutAsync(400);

        var block = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        var html = await block.InnerHTMLAsync();
        Assert.IsTrue(
            html.Contains("background-color:") || html.Contains("background:"),
            $"Text should have a background-color style applied. HTML: {html}");
        await TakeScreenshotAsync(page, "inline_toolbar_bg_color");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Turn Into
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Clicking the Turn Into button opens the block type panel")]
    public async Task InlineToolbar_TurnInto_OpensPanel()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var turnIntoBtn = page.Locator("button[title='Turn into']").First;
        await turnIntoBtn.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        var panel = page.Locator(".tm-notion-inline-toolbar__turninto-panel").First;
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await panel.IsVisibleAsync(), "Turn Into panel should appear");
        await TakeScreenshotAsync(page, "inline_toolbar_turninto_panel");
    }

    [TestMethod]
    [Description("Selecting Heading 1 from Turn Into converts the block to H1")]
    public async Task InlineToolbar_TurnInto_Heading1_Converts()
    {
        var page = await OpenNotionEditorAsync();
        await OpenInlineToolbarAsync(page);

        var turnIntoBtn = page.Locator("button[title='Turn into']").First;
        await turnIntoBtn.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        var h1Item = page.Locator(".tm-notion-inline-toolbar__turninto-item").Filter(new() { HasText = "Heading 1" }).First;
        await h1Item.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await h1Item.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        var h1 = page.Locator(".tm-notion-heading--h1").First;
        await h1.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await h1.IsVisibleAsync(), "Block should be converted to H1 after Turn Into");
        await TakeScreenshotAsync(page, "inline_toolbar_turninto_h1");
    }
}
