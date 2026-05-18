using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class DocumentEditorPhase14AutocompleteE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task TokenTrigger_TypedInEditor_InsertsSelectedTokenAndRemovesQuery()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await EditorTypeAsync(page, " {{client");
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-token-popover']", new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await page.Keyboard.PressAsync("Enter");

        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-token[data-inline-atomic='true']", new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        var text = await ReadEditorPlainTextAsync(page);
        Assert.IsFalse(text.Contains("{{client", StringComparison.Ordinal), "Token query should be removed before inserting the token chip.");
    }

    [TestMethod]
    public async Task SlashTableCommand_TypedInEditor_InsertsTableAndRemovesQuery()
    {
        var page = await OpenDocumentEditorAsync();

        await EditorTypeAsync(page, " /table");
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-autocomplete-popover']", new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await page.Keyboard.PressAsync("Enter");

        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-host'] table[data-block-id]", new()
        {
            State = WaitForSelectorState.Attached,
            Timeout = 10000
        });
        var text = await ReadEditorPlainTextAsync(page);
        Assert.IsFalse(text.Contains("/table"), "Slash query should be removed before inserting the table.");
    }

    [TestMethod]
    public async Task MentionTrigger_TypedInEditor_InsertsSelectedMentionAndRemovesQuery()
    {
        var page = await OpenDocumentEditorAsync();

        await EditorTypeAsync(page, " @ali");
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-autocomplete-popover']", new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-autocomplete-popover']")).ToHaveCountAsync(0);
        await page.WaitForFunctionAsync(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body'))
                .map(body => body.innerText || body.textContent || '')
                .join('\n')
                .includes('@alice')
            """,
            new PageWaitForFunctionOptions { Timeout = 10000 });
        var text = await ReadEditorPlainTextAsync(page);
        Assert.IsTrue(text.Contains("@alice"), "Selected mention username should be inserted.");
        Assert.IsFalse(text.Contains("@ali "), "Raw mention query should not remain before the inserted mention.");
    }

    [TestMethod]
    public async Task AutocompleteMenu_OnMobileViewport_StaysInsideViewport()
    {
        var page = await OpenDocumentEditorAsync(width: 390, height: 760);

        await EditorTypeAsync(page, " @ali");
        var popover = page.Locator("[data-testid='document-wysiwyg-autocomplete-popover']");
        await Assertions.Expect(popover).ToBeVisibleAsync(new() { Timeout = 10000 });

        var isInsideViewport = await popover.EvaluateAsync<bool>(
            """
            element => {
                const rect = element.getBoundingClientRect();
                return rect.left >= 0
                    && rect.top >= 0
                    && rect.right <= window.innerWidth
                    && rect.bottom <= window.innerHeight;
            }
            """);

        Assert.IsTrue(isInsideViewport, "Autocomplete menu should stay fully visible on a narrow mobile viewport.");
    }
}
