using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for the document editor phase 3 command registry migration.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase3CommandRegistryE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase3_KeyboardShortcuts_RunThroughCommandRegistry()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var host = page.Locator("[data-testid='document-wysiwyg-host']");
        await WaitForWysiwygBodyAsync(page);

        Assert.IsTrue(await SelectInlineContentsAsync(page, "contract-scope-approved"));
        await page.Keyboard.PressAsync("Control+b");
        await WaitForInlineBoldAsync(page, "contract-scope-approved");

        Assert.IsTrue(await SelectInlineContentsAsync(page, "contract-scope-approved"));
        await page.Keyboard.PressAsync("Control+i");
        await WaitForInlineItalicAsync(page, "contract-scope-approved");

        Assert.IsTrue(await SelectInlineContentsAsync(page, "contract-scope-approved"));
        await page.Keyboard.PressAsync("Control+u");
        await WaitForInlineUnderlineAsync(page, "contract-scope-approved");

        var body = await WaitForWysiwygBodyAsync(page);
        var marker = $" phase3-shortcuts-{DateTimeOffset.UtcNow:HHmmssfff}";
        await body.ClickAsync(new() { Position = new() { X = 24, Y = 24 } });
        await page.Keyboard.InsertTextAsync(marker);
        await Assertions.Expect(host).ToContainTextAsync(marker.Trim());

        await page.Keyboard.PressAsync("Control+Z");
        await Assertions.Expect(host).Not.ToContainTextAsync(marker.Trim(), new() { Timeout = 5000 });

        await page.Keyboard.PressAsync("Control+Y");
        await Assertions.Expect(host).ToContainTextAsync(marker.Trim(), new() { Timeout = 5000 });

        await page.Keyboard.PressAsync("Control+S");
        await Assertions.Expect(page.Locator("[data-testid='document-save-message']"))
            .ToContainTextAsync("Saved", new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase3_CommandPalette_SearchesAndExecutesBold()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);
        Assert.IsTrue(await SelectInlineContentsAsync(page, "contract-scope-approved"));

        await page.Keyboard.PressAsync("Control+Shift+P");
        var palette = page.Locator("[data-testid='document-command-palette']");
        await Assertions.Expect(palette).ToBeVisibleAsync();
        await palette.Locator("[data-testid='document-command-palette-search']").FillAsync("Bold");
        await Assertions.Expect(palette.Locator("[data-command='bold']")).ToBeVisibleAsync();
        await palette.Locator("[data-command='bold'] button").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-command-palette']")).ToHaveCountAsync(0);
        await WaitForInlineBoldAsync(page, "contract-scope-approved");
    }

    [TestMethod]
    public async Task Phase3_CommandPalette_DoesNotExecuteDisabledSaveInReadOnlyDocument()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        await page.Locator("[data-testid='document-editor-readonly']").CheckAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-editor-demo']"))
            .ToHaveClassAsync(new System.Text.RegularExpressions.Regex("tm-document-editor--readonly"));

        await page.Locator("[data-testid='document-editor-demo']").FocusAsync();
        await page.Keyboard.PressAsync("Control+Shift+P");
        var palette = page.Locator("[data-testid='document-command-palette']");
        await Assertions.Expect(palette).ToBeVisibleAsync();
        await palette.Locator("[data-testid='document-command-palette-search']").FillAsync("Save");

        var save = palette.Locator("[data-command='save']");
        await Assertions.Expect(save).ToBeVisibleAsync();
        await Assertions.Expect(save.Locator("button")).ToBeDisabledAsync();
        await Assertions.Expect(save.Locator("[data-testid='document-command-palette-disabled-reason']"))
            .ToContainTextAsync("Read-only document");
        await Assertions.Expect(page.Locator("[data-testid='document-save-message']")).ToHaveCountAsync(0);
    }

    [TestMethod]
    public async Task Phase3_ImportDocxToolbarCommandOpensRealInputFlow()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        await page.Locator("[data-testid='document-ribbon-tab-references']").ClickAsync();
        var importButton = page.Locator("[data-testid='document-import-docx-label']");
        await Assertions.Expect(importButton).ToBeVisibleAsync();
        await Assertions.Expect(importButton).ToBeEnabledAsync();

        await importButton.ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-import-docx-panel']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-import-docx']")).ToHaveCountAsync(1);
    }

    private static async Task<bool> SelectInlineContentsAsync(IPage page, string inlineId)
    {
        return await page.EvaluateAsync<bool>(
            """
            inlineId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const isVisible = el => {
                    if (!el || el.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual')) return false;
                    const rect = el.getBoundingClientRect();
                    const style = getComputedStyle(el);
                    return rect.width > 0
                        && rect.height > 0
                        && style.visibility !== 'hidden'
                        && style.display !== 'none';
                };
                const inline = Array.from(host?.querySelectorAll(`[data-inline-id="${CSS.escape(inlineId)}"]`) || [])
                    .find(isVisible);
                if (!inline) {
                    return false;
                }

                const resolve = absoluteOffset => {
                    const walker = document.createTreeWalker(inline, NodeFilter.SHOW_TEXT);
                    let current = 0;
                    let node;
                    while ((node = walker.nextNode())) {
                        const length = node.textContent.length;
                        if (absoluteOffset <= current + length) {
                            return { node, offset: Math.max(0, Math.min(absoluteOffset - current, length)) };
                        }
                        current += length;
                    }
                    return null;
                };
                const start = resolve(0);
                const end = resolve(inline.textContent.length);
                if (!start || !end) {
                    return false;
                }

                const range = document.createRange();
                range.setStart(start.node, start.offset);
                range.setEnd(end.node, end.offset);
                inline.closest('[contenteditable="true"]')?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
                return true;
            }
            """,
            inlineId);
    }

    private static Task WaitForInlineBoldAsync(IPage page, string inlineId)
    {
        return page.WaitForFunctionAsync(
            """
            inlineId => {
                const inline = Array.from(document.querySelectorAll(`[data-inline-id="${CSS.escape(inlineId)}"]`))
                    .find(el => {
                        const rect = el.getBoundingClientRect();
                        const style = getComputedStyle(el);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden'
                            && !el.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual');
                    });
                const weight = inline ? getComputedStyle(inline).fontWeight : '';
                return weight === 'bold' || Number(weight) >= 600;
            }
            """,
            inlineId);
    }

    private static Task WaitForInlineItalicAsync(IPage page, string inlineId)
    {
        return page.WaitForFunctionAsync(
            """
            inlineId => {
                const inline = Array.from(document.querySelectorAll(`[data-inline-id="${CSS.escape(inlineId)}"]`))
                    .find(el => {
                        const rect = el.getBoundingClientRect();
                        const style = getComputedStyle(el);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden'
                            && !el.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual');
                    });
                return inline && getComputedStyle(inline).fontStyle === 'italic';
            }
            """,
            inlineId);
    }

    private static Task WaitForInlineUnderlineAsync(IPage page, string inlineId)
    {
        return page.WaitForFunctionAsync(
            """
            inlineId => {
                const inline = Array.from(document.querySelectorAll(`[data-inline-id="${CSS.escape(inlineId)}"]`))
                    .find(el => {
                        const rect = el.getBoundingClientRect();
                        const style = getComputedStyle(el);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden'
                            && !el.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual');
                    });
                return inline && getComputedStyle(inline).textDecorationLine.includes('underline');
            }
            """,
            inlineId);
    }
}
