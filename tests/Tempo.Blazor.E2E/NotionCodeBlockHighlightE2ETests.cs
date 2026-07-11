using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// The language selector has to change what the reader sees. Prism paints the code behind a
/// transparent textarea; when the host page does not load Prism the code still renders, just plain.
/// Runs against the self-hosted HTTPS demo API (5100) and HTTPS demo WASM (7106).
/// </summary>
[TestClass]
public class NotionCodeBlockHighlightE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Picking a language paints the code with Prism tokens")]
    public async Task PickingALanguage_HighlightsTheCode()
    {
        var page = await OpenNotionEditorAsync();
        var block = await FirstCodeBlockAsync(page);

        await SelectLanguageAsync(page, block, "C#");

        // Give the block C# to chew on so the keyword token is unambiguous.
        var textarea = block.Locator("textarea").First;
        await textarea.ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("var x = 1;");
        await page.WaitForTimeoutAsync(700);

        var code = block.Locator(".tm-notion-code-block__code").First;
        await code.Locator(".token.keyword").First.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 20000 });

        var keyword = code.Locator(".token.keyword").First;
        Assert.IsTrue(await code.Locator(".token.keyword").CountAsync() > 0,
            "Prism must have produced token spans; without them the language selector does nothing.");
        StringAssert.Contains(await code.GetAttributeAsync("class") ?? string.Empty, "language-csharp");

        // The tokens live inside a MarkupString, so without ::deep the colour rules never reach them
        // and every keyword renders in the default text colour.
        var keywordColor = await keyword.EvaluateAsync<string>("el => getComputedStyle(el).color");
        var bodyColor = await code.EvaluateAsync<string>("el => getComputedStyle(el).color");
        Assert.AreNotEqual(bodyColor, keywordColor, "A keyword must be painted a different colour from plain code.");

        await CaptureBaselineAsync("code-highlight", "csharp", block);
        TestContext.WriteLine("UX: the selector now changes what you see — keywords, strings and numbers read apart at a glance.");
    }

    [TestMethod]
    [Description("Switching the language repaints with the new grammar")]
    public async Task SwitchingTheLanguage_Repaints()
    {
        var page = await OpenNotionEditorAsync();
        var block = await FirstCodeBlockAsync(page);

        await SelectLanguageAsync(page, block, "C#");
        var code = block.Locator(".tm-notion-code-block__code").First;
        StringAssert.Contains(await code.GetAttributeAsync("class") ?? string.Empty, "language-csharp");

        await SelectLanguageAsync(page, block, "Python");
        StringAssert.Contains(await code.GetAttributeAsync("class") ?? string.Empty, "language-python");
    }

    [TestMethod]
    [Description("Plain Text renders the code without any token markup")]
    public async Task PlainTextIsNotTokenised()
    {
        var page = await OpenNotionEditorAsync();
        var block = await FirstCodeBlockAsync(page);

        await SelectLanguageAsync(page, block, "Plain Text");

        var code = block.Locator(".tm-notion-code-block__code").First;
        Assert.AreEqual(0, await code.Locator(".token").CountAsync());
        Assert.IsFalse((await code.GetAttributeAsync("class") ?? string.Empty).Contains("language-"),
            "No grammar means no language- class.");
        Assert.IsTrue((await code.InnerTextAsync()).Trim().Length > 0, "The code must still be readable.");
    }

    [TestMethod]
    [Description("The highlight layer stays aligned with the text the user types")]
    public async Task TypingRepaintsTheHighlightLayer()
    {
        var page = await OpenNotionEditorAsync();
        var block = await FirstCodeBlockAsync(page);
        await SelectLanguageAsync(page, block, "C#");

        var textarea = block.Locator("textarea").First;
        await textarea.ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("while (true) { }");
        await page.WaitForTimeoutAsync(800);

        var code = block.Locator(".tm-notion-code-block__code").First;
        StringAssert.Contains(await code.InnerTextAsync(), "while",
            "The layer behind the textarea must follow every keystroke.");
        Assert.IsTrue(await code.Locator(".token.keyword").CountAsync() > 0);
    }

    [TestMethod]
    [Description("A markdown code block still offers the preview and renders a styled table")]
    public async Task MarkdownPreviewTableHasBorders()
    {
        var page = await OpenNotionEditorAsync();
        var block = await FirstCodeBlockAsync(page);

        await SelectLanguageAsync(page, block, "Markdown");
        var textarea = block.Locator("textarea").First;
        await textarea.ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.TypeAsync("| A | B |\n| --- | --- |\n| 1 | 2 |");
        await BlurAsync(page);
        await page.WaitForTimeoutAsync(900);

        await block.Locator("[data-testid='notion-code-preview-toggle']").First.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        var cell = block.Locator("[data-testid='notion-code-preview'] td").First;
        await cell.WaitForAsync(Visible());

        // Scoped CSS never reaches a MarkupString without ::deep, so the table used to render bare.
        var borderWidth = await cell.EvaluateAsync<string>(
            "el => getComputedStyle(el).borderBottomWidth");
        Assert.AreNotEqual("0px", borderWidth, "The preview table must have visible borders.");

        await CaptureBaselineAsync("code-highlight", "markdown-preview-table", block);
        TestContext.WriteLine("UX: the rendered markdown table now reads like a table, not like naked text.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LocatorWaitForOptions Visible() =>
        new() { State = WaitForSelectorState.Visible, Timeout = 20000 };

    private static async Task BlurAsync(IPage page)
    {
        await page.EvaluateAsync("() => document.activeElement?.blur()");
        await page.WaitForTimeoutAsync(400);
    }

    private static async Task<ILocator> FirstCodeBlockAsync(IPage page)
    {
        var block = page.Locator(".tm-notion-code-block").First;
        await block.WaitForAsync(Visible());
        await block.ScrollIntoViewIfNeededAsync();
        return block;
    }

    private static async Task SelectLanguageAsync(IPage page, ILocator block, string language)
    {
        await block.Locator("select").First.SelectOptionAsync(new SelectOptionValue { Value = language });
        await page.WaitForTimeoutAsync(900);
    }
}
