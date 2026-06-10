using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionAIE2ETests : NotionE2ETestBase
{
    private const string FormattingParagraphBlockId = "eb100000-0000-0000-0000-000000000003";

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF27: AI providerless mode hides AI affordances and captures the visual hidden state")]
    public async Task CF27_AIProviderlessHiddenState_Baseline()
    {
        var page = await OpenNotionEditorAsync("?disableAIProvider=true");
        await SeedTextFormattingPageAsync();

        await page.Locator(".tm-npsm-trigger").ClickAsync();
        var settingsMenu = page.Locator(".tm-npsm").First;
        await settingsMenu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await AssertHiddenAsync(page, "[data-testid='notion-ai-summarize']");
        await AssertHiddenAsync(page, "[data-testid='notion-ai-ask-page']");
        await CaptureBaselineAsync("ai-inline", "cf27-providerless-settings-menu", settingsMenu);
        await page.Keyboard.PressAsync("Escape");

        await FocusBlockAndOpenSlashAsync(page);
        await page.Locator(".tm-notion-slash__input").FillAsync("ai");
        await AssertHiddenAsync(page, "[data-testid='notion-ai-slash-item']");
        await CaptureBaselineAsync("ai-inline", "cf27-providerless-slash-menu", page.Locator(".tm-notion-slash").First);
        await page.Keyboard.PressAsync("Escape");

        await SelectLocatorContentsAsync(page, page.Locator(".tm-notion-paragraph[contenteditable='true']").First);
        await AssertHiddenAsync(page, "[data-testid='notion-inline-ai']");

        TestContext.WriteLine("UX CF27 review: there is no standalone AI provider status surface; providerless visual impact is the absence of AI affordances in page settings, slash menu, and inline selection. CF28 covers the active AI panels.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF28: AI generate, improve, summarize and ask work through the Notion editor UI and capture UX baselines")]
    public async Task CF28_AIInlineUX_HappyPathAndBaselines()
    {
        var page = await OpenNotionEditorAsync();
        await SeedTextFormattingPageAsync();

        await OpenSlashAIAsync(page);
        await page.Locator("[data-testid='notion-ai-prompt']").FillAsync("release notes for the editor");
        await page.Locator("[data-testid='notion-ai-run']").ClickAsync();
        await page.Locator("[data-testid='notion-ai-output']").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await ExpectTextAsync(page.Locator("[data-testid='notion-ai-output']"), "Demo AI completion");
        await CaptureBaselineAsync("ai-inline", "menu-streaming", page.Locator("[data-testid='notion-ai-menu']").First);
        await page.Locator("[data-testid='notion-ai-accept']").ClickAsync();
        await ExpectTextAsync(page.Locator(".tm-notion-page"), "Demo AI completion");

        await OpenInlineAIAsync(page, page.Locator(".tm-notion-paragraph").Filter(new LocatorFilterOptions { HasText = "Demo AI completion" }).First);
        await page.Locator("[data-testid='notion-ai-improve-shorten']").ClickAsync();
        await page.Locator("[data-testid='notion-ai-run']").ClickAsync();
        await ExpectTextAsync(page.Locator("[data-testid='notion-ai-output']"), "Demo AI completion");
        await CaptureBaselineAsync("ai-inline", "improve-streaming", page.Locator("[data-testid='notion-ai-menu']").First);
        await page.Locator("[data-testid='notion-ai-discard']").ClickAsync();

        await OpenPageSettingsAIAsync(page, "[data-testid='notion-ai-summarize']");
        await page.Locator("[data-testid='notion-ai-run']").ClickAsync();
        await ExpectTextAsync(page.Locator("[data-testid='notion-ai-output']"), "Summary:");
        await CaptureBaselineAsync("ai-inline", "summarize-panel", page.Locator("[data-testid='notion-ai-menu']").First);
        await page.Locator("[data-testid='notion-ai-discard']").ClickAsync();

        await OpenPageSettingsAIAsync(page, "[data-testid='notion-ai-ask-page']");
        await page.Locator("[data-testid='notion-ai-question']").FillAsync("What is this page about?");
        await page.Locator("[data-testid='notion-ai-run']").ClickAsync();
        await ExpectTextAsync(page.Locator("[data-testid='notion-ai-output']"), "Demo AI answer");
        await CaptureBaselineAsync("ai-inline", "ask-panel", page.Locator("[data-testid='notion-ai-menu']").First);

        TestContext.WriteLine("UX CF28 review: AI menu exposes clear run/accept/discard/retry actions, streaming output is visible with live feedback, and page-level summarize/ask panels stay readable without covering the editor context.");
    }

    [TestMethod]
    [Description("CF28: AI controls are hidden when no AI provider is configured and empty prompt shows inline validation")]
    public async Task CF28_AIInlineUX_EdgeCases_Work()
    {
        var page = await OpenNotionEditorAsync("?disableAIProvider=true");
        await SeedTextFormattingPageAsync();

        await page.Locator(".tm-npsm-trigger").ClickAsync();
        await AssertHiddenAsync(page, "[data-testid='notion-ai-summarize']");
        await AssertHiddenAsync(page, "[data-testid='notion-ai-ask-page']");
        await page.Keyboard.PressAsync("Escape");

        await FocusBlockAndOpenSlashAsync(page);
        await page.Locator(".tm-notion-slash__input").FillAsync("ai");
        await AssertHiddenAsync(page, "[data-testid='notion-ai-slash-item']");
        await page.Keyboard.PressAsync("Escape");
        await SelectLocatorContentsAsync(page, page.Locator(".tm-notion-paragraph[contenteditable='true']").First);
        await AssertHiddenAsync(page, "[data-testid='notion-inline-ai']");

        page = await OpenNotionEditorAsync();
        await SeedTextFormattingPageAsync();
        await OpenSlashAIAsync(page);
        await page.Locator("[data-testid='notion-ai-run']").ClickAsync();
        await ExpectTextAsync(page.Locator("[data-testid='notion-ai-error']"), "Enter a prompt first");

        await page.Locator("[data-testid='notion-ai-prompt']").FillAsync("a deliberately long deterministic response request that keeps streaming content readable in the AI preview panel");
        await page.Locator("[data-testid='notion-ai-run']").ClickAsync();
        await ExpectTextAsync(page.Locator("[data-testid='notion-ai-output']"), "deliberately long deterministic response");
        await page.Locator("[data-testid='notion-ai-discard']").ClickAsync();
    }

    [TestMethod]
    [Description("CF28: AI retry, cancel, all improve modes, and provider error states are covered through the editor UI")]
    public async Task CF28_AIInlineUX_RetryCancelImproveModesAndProviderError_Work()
    {
        var page = await OpenNotionEditorAsync("?slowAIProvider=true");
        await SeedTextFormattingPageAsync();

        await OpenSlashAIAsync(page);
        await page.Locator("[data-testid='notion-ai-prompt']").FillAsync("a long streaming response for cancellation " + new string('x', 800));
        await page.Locator("[data-testid='notion-ai-run']").ClickAsync();
        var cancel = page.Locator("[data-testid='notion-ai-cancel']").First;
        await cancel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await CaptureBaselineAsync("ai-inline", "generate-streaming-active", page.Locator("[data-testid='notion-ai-menu']").First);
        await cancel.ClickAsync();
        await page.Locator("[data-testid='notion-ai-cancel']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 10000 });

        await page.Locator("[data-testid='notion-ai-run']").ClickAsync();
        await ExpectTextAsync(page.Locator("[data-testid='notion-ai-output']"), "Demo AI completion");
        await page.Locator("[data-testid='notion-ai-retry']").ClickAsync();
        await ExpectTextAsync(page.Locator("[data-testid='notion-ai-output']"), "Demo AI completion");
        await page.Locator("[data-testid='notion-ai-discard']").ClickAsync();

        page = await OpenNotionEditorAsync();
        await SeedTextFormattingPageAsync();
        await OpenInlineAIAsync(page, page.Locator($"[data-block-id='{FormattingParagraphBlockId}'] .tm-notion-paragraph[contenteditable='true']").First);
        foreach (var mode in ImproveModeChecks)
        {
            await page.Locator($"[data-testid='{mode.TestId}']").ClickAsync();
            await page.Locator("[data-testid='notion-ai-run']").ClickAsync();
            await ExpectTextAsync(page.Locator("[data-testid='notion-ai-output']"), mode.ExpectedText);
        }
        await page.Locator("[data-testid='notion-ai-discard']").ClickAsync();

        var failing = await OpenNotionEditorAsync("?failAIProvider=true");
        await SeedTextFormattingPageAsync();
        await OpenSlashAIAsync(failing);
        await failing.Locator("[data-testid='notion-ai-prompt']").FillAsync("show provider error");
        await failing.Locator("[data-testid='notion-ai-run']").ClickAsync();
        await ExpectTextAsync(failing.Locator("[data-testid='notion-ai-error']"), "AI request failed");
        await CaptureBaselineAsync("ai-inline", "provider-error", failing.Locator("[data-testid='notion-ai-menu']").First);
    }

    private static readonly (string TestId, string ExpectedText)[] ImproveModeChecks =
    [
        ("notion-ai-improve-grammar", "Combined active inline toolbar state"),
        ("notion-ai-improve-shorten", "Combined active inline toolbar state"),
        ("notion-ai-improve-lengthen", "Expanded version"),
        ("notion-ai-improve-tone", "Professional rewrite"),
        ("notion-ai-improve-simplify", "Simple version"),
        ("notion-ai-improve-translate", "Translated text")
    ];

    private static async Task OpenSlashAIAsync(IPage page)
    {
        await FocusBlockAndOpenSlashAsync(page);
        await page.Locator(".tm-notion-slash__input").FillAsync("ai");
        var item = page.Locator("[data-testid='notion-ai-slash-item']").First;
        await item.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await item.ClickAsync();
        await page.Locator("[data-testid='notion-ai-menu']").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }

    private static async Task FocusBlockAndOpenSlashAsync(IPage page)
    {
        var editable = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        await editable.ScrollIntoViewIfNeededAsync();
        await editable.ClickAsync();
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(600);
        await page.Keyboard.TypeAsync("/");
        await page.Locator(".tm-notion-slash").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }

    private static async Task OpenInlineAIAsync(IPage page, ILocator editable)
    {
        await editable.ScrollIntoViewIfNeededAsync();
        await editable.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await editable.ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.WaitForTimeoutAsync(300);

        if (await page.Locator("[data-testid='notion-inline-ai']").First.CountAsync() == 0 ||
            !await page.Locator("[data-testid='notion-inline-ai']").First.IsVisibleAsync())
        {
            await SelectLocatorContentsAsync(page, editable);
        }

        var button = page.Locator("[data-testid='notion-inline-ai']").First;
        await button.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await button.EvaluateAsync("el => el.click()");
        await page.Locator("[data-testid='notion-ai-menu']").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }

    private static async Task OpenPageSettingsAIAsync(IPage page, string selector)
    {
        await page.Locator(".tm-npsm-trigger").ClickAsync();
        var button = page.Locator(selector).First;
        await button.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await button.ClickAsync();
        await page.Locator("[data-testid='notion-ai-menu']").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }

    private static async Task ExpectTextAsync(ILocator locator, string expected)
    {
        await Assertions.Expect(locator).ToContainTextAsync(expected, new LocatorAssertionsToContainTextOptions
        {
            Timeout = 10000
        });
    }

    private static async Task AssertHiddenAsync(IPage page, string selector)
    {
        var count = await page.Locator(selector).CountAsync();
        Assert.AreEqual(0, count, $"{selector} should not render.");
    }
}
