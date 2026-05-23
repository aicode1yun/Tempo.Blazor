using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 6 schema, insertion policy, and post-fixer guards.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase6SchemaPolicyE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase6_PageBreakWorksInBodyButIsDisabledInHeader()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        var bridge = await page.EvaluateAsync<bool>(
            """
            () => window.tmDocumentEditorEngine?.__testHooks?.schemaAllowsBlock?.(6, 'Body') === true
                && window.tmDocumentEditorEngine?.__testHooks?.schemaAllowsBlock?.(6, 'Header') === false
            """);
        Assert.IsTrue(bridge, "Runtime schema bridge must expose the same page-break placement rule.");

        await PlaceCaretAtEndOfBodyAsync(page);
        await ExecuteRuntimeCommandAsync(page, "insertPageBreak", new { });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page-break"))
            .ToHaveCountAsync(1, new() { Timeout = 5000 });

        var header = page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__header[contenteditable='true']").First;
        await header.DblClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-ribbon-tab-header-footer']")).ToBeVisibleAsync(new() { Timeout = 5000 });

        await page.Locator("[data-testid='document-ribbon-tab-insert']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-insert-page-break']")).ToBeDisabledAsync(new() { Timeout = 5000 });
    }

    [TestMethod]
    public async Task Phase6_InvalidNestedTablePasteDoesNotCorruptDocumentSurface()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);
        await PlaceCaretAtEndOfBodyAsync(page);

        await page.EvaluateAsync(
            """
            html => {
                const body = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page__body[contenteditable="true"]');
                body.focus();
                const data = new DataTransfer();
                data.setData('text/html', html);
                data.setData('text/plain', 'Outer Nested');
                body.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
            }
            """,
            "<table><tr><td>Outer<table><tr><td>Nested</td></tr></table></td></tr></table>");

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table").First)
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        var pageBreakStillBodyOnly = await page.EvaluateAsync<bool>(
            """
            () => window.tmDocumentEditorEngine.__testHooks.normalizeInsertionBlocksForSchema(
                [{ Id: 'pb', Type: 6, Content: { $type: 'pageBreak' } }],
                'Footer'
            ).blocks.length === 0
            """);
        Assert.IsTrue(pageBreakStillBodyOnly);
    }

    private static Task ExecuteRuntimeCommandAsync(IPage page, string command, object payload)
    {
        return page.EvaluateAsync(
            """
            ({ command, payload }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorRuntime?.executeCommand?.(instanceId, command, payload);
            }
            """,
            new { command, payload });
    }

    private static Task PlaceCaretAtEndOfBodyAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const body = Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page__body[contenteditable="true"]'))
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        const style = getComputedStyle(element);
                        return rect.width > 0
                            && rect.height > 0
                            && style.display !== 'none'
                            && style.visibility !== 'hidden'
                            && !element.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual');
                    });
                if (!body) throw new Error('Editable body was not found.');

                const blocks = Array.from(body.children)
                    .filter(block =>
                        block.matches('p[data-block-id], h1[data-block-id], h2[data-block-id], h3[data-block-id], h4[data-block-id], h5[data-block-id], h6[data-block-id], blockquote[data-block-id], li[data-block-id]')
                        && block.textContent.trim().length > 0);
                const target = blocks.at(-1) || body;
                target.closest('[contenteditable="true"]')?.focus();
                const walker = document.createTreeWalker(target, NodeFilter.SHOW_TEXT);
                let last = null;
                while (walker.nextNode()) {
                    if ((walker.currentNode.textContent || '').trim().length > 0) last = walker.currentNode;
                }
                const range = document.createRange();
                if (last) {
                    range.setStart(last, last.textContent.length);
                } else {
                    range.selectNodeContents(body);
                    range.collapse(false);
                }
                range.collapse(true);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """);
    }
}
