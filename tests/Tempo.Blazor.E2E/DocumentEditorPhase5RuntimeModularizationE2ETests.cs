using System.Diagnostics;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 5 runtime modularization.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase5RuntimeModularizationE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase5_RuntimeModulesKeepCoreEditingTableImageCommentsAndRevisionsWorking()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        var moduleNames = await page.EvaluateAsync<string[]>(
            """
            () => window.tmDocumentEditorRuntime?.__internal?.getModuleNames?.() || []
            """);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "core",
                "selection",
                "rendering",
                "input",
                "formatting",
                "clipboard",
                "image",
                "table",
                "comments",
                "revisions",
                "serialization",
                "watchdog"
            },
            moduleNames);

        var typingMarker = $"phase5-type-{DateTimeOffset.UtcNow:HHmmssfff}";
        await PlaceCaretAtEndOfBodyAsync(page);
        var stopwatch = Stopwatch.StartNew();
        await page.Keyboard.InsertTextAsync(typingMarker);
        stopwatch.Stop();
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(typingMarker);
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Typing smoke took {stopwatch.Elapsed}.");

        var undoMarker = $"phase5-undo-{DateTimeOffset.UtcNow:HHmmssfff}";
        await page.Keyboard.InsertTextAsync(undoMarker);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(undoMarker);
        await page.Keyboard.PressAsync("Control+Z");
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).Not.ToContainTextAsync(undoMarker, new() { Timeout = 5000 });
        await page.Keyboard.PressAsync("Control+Y");
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(undoMarker, new() { Timeout = 5000 });

        await SelectTextAsync(page, typingMarker);
        await page.Keyboard.PressAsync("Control+B");
        await WaitForSelectedTextBoldAsync(page, typingMarker);

        await PlaceCaretAtEndOfBodyAsync(page);
        await ExecuteRuntimeCommandAsync(page, "insertTable", new { rows = 2, columns = 2 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table").First)
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        var imageAlt = $"phase5-image-{DateTimeOffset.UtcNow:HHmmssfff}";
        await PlaceCaretAtEndOfBodyAsync(page);
        await ExecuteRuntimeCommandAsync(page, "insertImageUrl", new
        {
            url = "data:image/gif;base64,R0lGODlhAQABAAAAACw=",
            altText = imageAlt
        });
        await Assertions.Expect(page.Locator($"[data-testid='document-wysiwyg-host'] img[alt='{imageAlt}']").First)
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        var commentText = $"phase5-comment-{DateTimeOffset.UtcNow:HHmmssfff}";
        await SelectTextAsync(page, typingMarker);
        await page.Locator("[data-testid='document-ribbon-tab-review']").ClickAsync();
        await page.Locator("[data-testid='document-add-comment']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-comment-new-composer']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.Locator("[data-testid='document-comment-input']").FillAsync(commentText);
        await page.Locator("[data-testid='document-comment-submit']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-comment-thread']").Filter(new() { HasText = commentText }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        var revisionMarker = $"phase5-revision-{DateTimeOffset.UtcNow:HHmmssfff}";
        var trackChanges = page.Locator("[data-testid='document-track-changes']");
        if (await trackChanges.GetAttributeAsync("aria-pressed") != "true")
        {
            await trackChanges.ClickAsync();
        }
        await PlaceCaretAtEndOfBodyAsync(page);
        await page.Keyboard.InsertTextAsync(revisionMarker);
        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = revisionMarker }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
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

    private static Task SelectTextAsync(IPage page, string text)
    {
        return page.EvaluateAsync(
            """
            text => {
                const body = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page__body[contenteditable="true"]');
                if (!body) throw new Error('Editable body was not found.');
                const walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT);
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    const index = (node.textContent || '').indexOf(text);
                    if (index >= 0) {
                        const range = document.createRange();
                        range.setStart(node, index);
                        range.setEnd(node, index + text.length);
                        body.focus();
                        const selection = window.getSelection();
                        selection.removeAllRanges();
                        selection.addRange(range);
                        document.dispatchEvent(new Event('selectionchange'));
                        return;
                    }
                }
                throw new Error(`Text was not found: ${text}`);
            }
            """,
            text);
    }

    private static Task WaitForSelectedTextBoldAsync(IPage page, string text)
    {
        return page.WaitForFunctionAsync(
            """
            text => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const candidates = Array.from(host?.querySelectorAll('[data-inline-id], strong, b, span') || [])
                    .filter(element => (element.textContent || '').includes(text));
                return candidates.some(element => {
                    const style = getComputedStyle(element);
                    return style.fontWeight === 'bold' || Number(style.fontWeight) >= 600 || element.closest('strong,b');
                });
            }
            """,
            text);
    }
}
