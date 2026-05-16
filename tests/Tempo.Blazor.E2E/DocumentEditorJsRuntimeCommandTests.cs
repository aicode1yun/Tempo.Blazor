using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for the JS-owned ribbon command dispatcher.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeCommandTests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase5_BoldRibbonCommandFormatsSelectionWithoutFullRenderRefresh()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        Assert.IsTrue(await SelectInlineContentsAsync(page, "contract-scope-approved"));

        var before = await ReadRenderStatsAsync(page);
        await page.Locator("[data-testid='document-bold']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-bold']")).ToHaveAttributeAsync("aria-pressed", "true");
        var after = await ReadRenderStatsAsync(page);
        var transaction = await ReadLastCommandTransactionAsync(page);

        Assert.AreEqual(before.FullRenderCount, after.FullRenderCount, "Bold command must mutate the JS-owned surface without a full content render.");
        Assert.AreEqual("toggleBold", transaction.Command);
        Assert.AreEqual(1, transaction.Operations.Length);
        Assert.AreEqual(1, transaction.InverseOperations.Length);
        StringAssert.Contains(transaction.InverseOperations[0].InverseOf, transaction.Operations[0].OperationId);
    }

    [TestMethod]
    public async Task Phase5_FontSizeRibbonCommandUpdatesToolbarState()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        Assert.IsTrue(await SelectInlineContentsAsync(page, "contract-scope-approved"));

        await page.Locator("[data-testid='document-font-size']").SelectOptionAsync("18");
        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const formatting = window.tmDocumentEditorRuntime?.getFormattingState?.(instanceId);
                return String(formatting?.FontSize || formatting?.fontSize || '') === '18pt';
            }
            """);

        var stateFontSize = await page.EvaluateAsync<string>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const formatting = window.tmDocumentEditorRuntime?.getFormattingState?.(instanceId) || {};
                return String(formatting.FontSize || formatting.fontSize || document.querySelector('[data-testid="document-font-size"]')?.value || '');
            }
            """);
        Assert.AreEqual("18pt", stateFontSize);
        var transaction = await ReadLastCommandTransactionAsync(page);
        Assert.AreEqual("setFontSize", transaction.Command);
    }

    [TestMethod]
    public async Task Phase5_ParagraphAlignmentRibbonCommandUpdatesSelectedBlock()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        Assert.IsTrue(await SelectInlineContentsAsync(page, "contract-scope-approved"));

        await page.Locator("[data-testid='document-align-right']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-align-right']")).ToHaveAttributeAsync("aria-pressed", "true");

        var textAlign = await page.EvaluateAsync<string>(
            """
            () => {
                const block = document.querySelector('[data-testid="document-wysiwyg-host"] [data-block-id="contract-scope"]');
                return block?.style.textAlign || getComputedStyle(block).textAlign || '';
            }
            """);
        Assert.AreEqual("right", textAlign);
        var transaction = await ReadLastCommandTransactionAsync(page);
        Assert.AreEqual("setParagraphAlignment", transaction.Command);
    }

    private static Task<bool> SelectInlineContentsAsync(IPage page, string inlineId)
    {
        return page.EvaluateAsync<bool>(
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

    private static Task<RenderStats> ReadRenderStatsAsync(IPage page)
    {
        return page.EvaluateAsync<RenderStats>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const stats = window.tmDocumentWysiwygDebug?.getRenderStats?.(instanceId) || {};
                return {
                    fullRenderCount: Number(stats.FullRenderCount || 0),
                    incrementalOperationCount: Number(stats.IncrementalOperationCount || 0),
                    lastRenderReason: String(stats.LastRenderReason || '')
                };
            }
            """);
    }

    private static Task<CommandTransaction> ReadLastCommandTransactionAsync(IPage page)
    {
        return page.EvaluateAsync<CommandTransaction>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentEditorRuntime?.getLastCommandTransaction?.(instanceId) || {};
            }
            """);
    }

    private sealed class RenderStats
    {
        [JsonPropertyName("fullRenderCount")]
        public int FullRenderCount { get; set; }

        [JsonPropertyName("incrementalOperationCount")]
        public int IncrementalOperationCount { get; set; }

        [JsonPropertyName("lastRenderReason")]
        public string LastRenderReason { get; set; } = string.Empty;
    }

    private sealed class CommandTransaction
    {
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("operations")]
        public CommandOperation[] Operations { get; set; } = [];

        [JsonPropertyName("inverseOperations")]
        public CommandOperation[] InverseOperations { get; set; } = [];
    }

    private sealed class CommandOperation
    {
        [JsonPropertyName("operationId")]
        public string OperationId { get; set; } = string.Empty;

        [JsonPropertyName("inverseOf")]
        public string InverseOf { get; set; } = string.Empty;
    }
}
