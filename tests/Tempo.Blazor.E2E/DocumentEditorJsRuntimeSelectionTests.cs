using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for the JS-owned selection authority migration.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeSelectionTests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase4_SelectingBoldInlineTextUpdatesToolbarFromJsSelection()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        var selected = await SelectInlineContentsAsync(page, "contract-intro-prefix");

        Assert.IsTrue(selected, "The seeded contract intro prefix must be selectable.");
        await Assertions.Expect(page.Locator("[data-testid='document-bold']")).ToHaveAttributeAsync("aria-pressed", "true");
    }

    [TestMethod]
    public async Task Phase4_SelectingMixedBoldPlainTextReportsMixedToolbarState()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        var selected = await SelectRangeAcrossInlinesAsync(page, "contract-intro-prefix", "contract-intro-suffix");

        Assert.IsTrue(selected, "The seeded contract intro inlines must be selectable as one range.");
        await Assertions.Expect(page.Locator("[data-testid='document-bold']")).ToHaveAttributeAsync("aria-pressed", "mixed");
    }

    private static Task<bool> SelectInlineContentsAsync(IPage page, string inlineId)
    {
        return page.EvaluateAsync<bool>(
            """
            inlineId => {
                const inline = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-inline-id="${CSS.escape(inlineId)}"]`);
                if (!inline) {
                    return false;
                }

                const range = document.createRange();
                range.selectNodeContents(inline);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
                return true;
            }
            """,
            inlineId);
    }

    private static Task<bool> SelectRangeAcrossInlinesAsync(IPage page, string startInlineId, string endInlineId)
    {
        return page.EvaluateAsync<bool>(
            """
            ids => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const start = host?.querySelector(`[data-inline-id="${CSS.escape(ids.startInlineId)}"]`);
                const end = host?.querySelector(`[data-inline-id="${CSS.escape(ids.endInlineId)}"]`);
                if (!start || !end) {
                    return false;
                }

                const startText = firstTextNode(start);
                const endText = lastTextNode(end);
                if (!startText || !endText) {
                    return false;
                }

                const range = document.createRange();
                range.setStart(startText, 0);
                range.setEnd(endText, endText.textContent.length);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
                return true;

                function firstTextNode(root) {
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
                    return walker.nextNode();
                }

                function lastTextNode(root) {
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
                    let current = null;
                    let next = null;
                    while ((next = walker.nextNode())) {
                        current = next;
                    }

                    return current;
                }
            }
            """,
            new { startInlineId, endInlineId });
    }
}
