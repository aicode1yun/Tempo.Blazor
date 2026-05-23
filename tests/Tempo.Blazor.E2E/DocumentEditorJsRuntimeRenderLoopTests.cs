using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.E2E;

[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeRenderLoopTests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase3_RuntimeRenderLoop_RendersVisibleDocumentTextAndStableNodeIds()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        var text = await ReadEditorPlainTextAsync(page);
        StringAssert.Contains(text, "Service agreement");

        var hasStableNodeAttributes = await page.EvaluateAsync<bool>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const pageEl = host?.querySelector('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual)[data-node-id]');
                const body = pageEl?.querySelector('[data-testid="document-wysiwyg-body"][data-node-id]');
                const block = body?.querySelector('.tm-wysiwyg-block[data-node-id][data-block-id]');
                const inline = block?.querySelector('[data-node-id][data-inline-id]');
                return !!(host && pageEl && body && block && inline);
            }
            """);

        Assert.IsTrue(hasStableNodeAttributes, "Runtime render loop must stamp stable node ids for selection and incremental rendering.");
    }

    [TestMethod]
    public async Task Phase3_ClickingSurfaceDoesNotTriggerFullContentRender()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);
        await page.WaitForTimeoutAsync(300);

        var before = await ReadRenderStatsAsync(page);
        var body = await WaitForWysiwygBodyAsync(page);
        await body.ClickAsync();
        await page.WaitForTimeoutAsync(300);
        var after = await ReadRenderStatsAsync(page);

        Assert.AreEqual(before.FullRenderCount, after.FullRenderCount, "Plain focus/click must not cause a Blazor-like full content render.");
    }

    [TestMethod]
    public async Task Phase16_ThirtyPageDocumentUsesVirtualizedVisiblePages()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await LoadThirtyPageRuntimeDocumentAsync(page);
        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const stats = window.tmDocumentEditorDebug?.getRenderStats?.(instanceId) || {};
                return stats.VirtualizationEnabled && Number(stats.TotalPages || 0) >= 30;
            }
            """);

        var stats = await ReadRenderStatsAsync(page);

        Assert.IsTrue(stats.VirtualizationEnabled, "A 30 page document should enable page virtualization.");
        Assert.AreEqual(30, stats.TotalPages);
        Assert.IsTrue(stats.RenderedPages < stats.TotalPages, $"Expected only visible pages to render. Rendered {stats.RenderedPages}/{stats.TotalPages}.");
        Assert.IsTrue(stats.VirtualizedPages > 0, "Invisible pages should stay as placeholders.");
    }

    [TestMethod]
    public async Task Phase16_RemoteOperationOnVirtualPageUpdatesModelWithoutRenderingPage()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await LoadThirtyPageRuntimeDocumentAsync(page);
        var before = await ReadRenderStatsAsync(page);

        var result = await page.EvaluateAsync<RemoteVirtualOperationResult>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const beforeStats = window.tmDocumentEditorDebug?.getRenderStats?.(instanceId) || {};
                const applied = window.tmDocumentEditorRuntime?.applyRemoteOperation?.(instanceId, {
                    OperationId: 'phase16-remote-virtual-insert',
                    Type: 0,
                    Target: { BlockId: 'phase16-p29', InlineId: 'phase16-i29', Offset: 0 },
                    Text: 'REMOTE-VIRTUAL '
                });
                const raw = window.tmDocumentEditorRuntime?.getDocument?.(instanceId);
                const parsed = raw ? JSON.parse(raw) : {};
                const runtimeDocument = parsed.Document || parsed.document || {};
                const blocks = runtimeDocument.Blocks || runtimeDocument.blocks || [];
                const target = blocks.find(block => (block.Id || block.id) === 'phase16-p29');
                const inlines = (target?.Content || target?.content || {}).Inlines || (target?.Content || target?.content || {}).inlines || [];
                const text = String(inlines[0]?.Text || inlines[0]?.text || '');
                const afterStats = window.tmDocumentEditorDebug?.getRenderStats?.(instanceId) || {};
                const visibleTarget = !!host?.querySelector('[data-block-id="phase16-p29"]');
                return {
                    applied: !!applied,
                    modelText: text,
                    visibleTarget,
                    fullRenderCountBefore: Number(beforeStats.FullRenderCount || 0),
                    fullRenderCountAfter: Number(afterStats.FullRenderCount || 0),
                    renderedPagesBefore: Number(beforeStats.RenderedPages || 0),
                    renderedPagesAfter: Number(afterStats.RenderedPages || 0)
                };
            }
            """);

        Assert.IsTrue(before.VirtualizationEnabled, "The setup document must be virtualized.");
        Assert.IsTrue(result.Applied, "Remote text insert on a virtual page should be accepted.");
        StringAssert.StartsWith(result.ModelText, "REMOTE-VIRTUAL ");
        Assert.IsFalse(result.VisibleTarget, "The offscreen page should not be rendered just because its model changed.");
        Assert.AreEqual(result.FullRenderCountBefore, result.FullRenderCountAfter, "Remote operation on a virtual page must not force a full render.");
        Assert.AreEqual(result.RenderedPagesBefore, result.RenderedPagesAfter, "Remote operation on a virtual page must not expand rendered page count.");
    }

    [TestMethod]
    public async Task Phase16_SelectionOnVirtualPageRestoresAfterScrollingBack()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await LoadThirtyPageRuntimeDocumentAsync(page);
        await ScrollVirtualizedHostAsync(page, toEnd: true);
        await page.WaitForFunctionAsync(
            """
            () => !!document.querySelector('[data-testid="document-wysiwyg-host"] [data-block-id="phase16-p29"]')
            """);

        await PlaceCaretInBlockAsync(page, "phase16-p29", 10);
        var selectedAtEnd = await ReadSelectionAsync(page);
        Assert.AreEqual("phase16-p29", selectedAtEnd.BlockId);

        await ScrollVirtualizedHostAsync(page, toEnd: false);
        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const stats = window.tmDocumentEditorDebug?.getRenderStats?.(instanceId) || {};
                return !host?.querySelector('[data-block-id="phase16-p29"]')
                    && stats.VirtualizationEnabled
                    && Number(stats.RenderedPages || 0) < Number(stats.TotalPages || 0);
            }
            """);

        await ScrollVirtualizedHostAsync(page, toEnd: true);
        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const selection = window.tmDocumentEditorRuntime?.getSelectionSnapshot?.(instanceId) || {};
                return !!host?.querySelector('[data-block-id="phase16-p29"]')
                    && (selection.AnchorBlockId || selection.anchorBlockId) === 'phase16-p29';
            }
            """);

        var restored = await ReadSelectionAsync(page);
        Assert.AreEqual("phase16-p29", restored.BlockId, "Selection from a virtualized page should be restored when that page is rendered again.");
        Assert.AreEqual(10, restored.Offset);
    }

    private static Task<RenderStats> ReadRenderStatsAsync(IPage page)
    {
        return page.EvaluateAsync<RenderStats>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const stats = window.tmDocumentEditorDebug?.getRenderStats?.(instanceId) || {};
                return {
                    fullRenderCount: Number(stats.FullRenderCount || 0),
                    incrementalOperationCount: Number(stats.IncrementalOperationCount || 0),
                    lastRenderReason: String(stats.LastRenderReason || ''),
                    virtualizationEnabled: !!stats.VirtualizationEnabled,
                    totalPages: Number(stats.TotalPages || 0),
                    renderedPages: Number(stats.RenderedPages || 0),
                    virtualizedPages: Number(stats.VirtualizedPages || 0)
                };
            }
            """);
    }

    private static Task LoadThirtyPageRuntimeDocumentAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const blocks = [];
                for (let pageIndex = 0; pageIndex < 30; pageIndex++) {
                    blocks.push({
                        Id: `phase16-p${pageIndex}`,
                        Type: 0,
                        Order: pageIndex * 20,
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                {
                                    $type: 'text',
                                    Id: `phase16-i${pageIndex}`,
                                    Text: `Phase 16 virtualized page ${pageIndex + 1}.`
                                }
                            ]
                        }
                    });

                    if (pageIndex < 29) {
                        blocks.push({
                            Id: `phase16-break-${pageIndex}`,
                            Type: 6,
                            Order: pageIndex * 20 + 10,
                            Content: { $type: 'pageBreak' }
                        });
                    }
                }

                window.tmDocumentEditorRuntime?.loadDocument?.(instanceId, {
                    ProtocolVersion: 1,
                    Document: {
                        DocumentId: 'phase16-virtual-document',
                        SchemaVersion: 1,
                        Metadata: { Title: 'Phase 16 virtual document' },
                        PageSettings: { Size: 'A4', Width: '210mm', Height: '297mm' },
                        Blocks: blocks,
                        HeadersFooters: [],
                        Sections: [],
                        Notes: []
                    }
                }, true);
            }
            """);
    }

    private static Task ScrollVirtualizedHostAsync(IPage page, bool toEnd)
    {
        return page.EvaluateAsync(
            """
            toEnd => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                if (!host) {
                    throw new Error('WYSIWYG host was not found.');
                }

                const hostTarget = toEnd ? Math.max(0, host.scrollHeight - host.clientHeight) : 0;
                host.scrollTop = hostTarget;
                const hostTop = host.getBoundingClientRect().top + (window.scrollY || window.pageYOffset || 0);
                const windowTarget = toEnd
                    ? Math.max(0, hostTop + host.scrollHeight - window.innerHeight)
                    : Math.max(0, hostTop - 16);
                window.scrollTo(0, windowTarget);
                window.tmDocumentEditorEngine?.refreshVirtualization?.(instanceId);
            }
            """,
            toEnd);
    }

    private static Task PlaceCaretInBlockAsync(IPage page, string blockId, int offset)
    {
        return page.EvaluateAsync(
            """
            ({ blockId, offset }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const block = host?.querySelector(`[data-block-id="${CSS.escape(blockId)}"]`);
                if (!block) {
                    throw new Error(`Block ${blockId} was not rendered.`);
                }

                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                let current = 0;
                let textNode = null;
                let localOffset = 0;
                while ((textNode = walker.nextNode())) {
                    const length = textNode.textContent.length;
                    if (offset <= current + length) {
                        localOffset = Math.max(0, Math.min(offset - current, length));
                        break;
                    }

                    current += length;
                }

                if (!textNode) {
                    throw new Error(`Text node for ${blockId} was not found.`);
                }

                const range = document.createRange();
                range.setStart(textNode, localOffset);
                range.collapse(true);
                block.closest('[contenteditable="true"]')?.focus({ preventScroll: true });
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """,
            new { blockId, offset });
    }

    private static Task<SelectionSnapshot> ReadSelectionAsync(IPage page)
    {
        return page.EvaluateAsync<SelectionSnapshot>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const selection = window.tmDocumentEditorRuntime?.getSelectionSnapshot?.(instanceId) || {};
                return {
                    blockId: String(selection.AnchorBlockId || selection.anchorBlockId || ''),
                    offset: Number(selection.AnchorOffset ?? selection.anchorOffset ?? 0)
                };
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

        [JsonPropertyName("virtualizationEnabled")]
        public bool VirtualizationEnabled { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("renderedPages")]
        public int RenderedPages { get; set; }

        [JsonPropertyName("virtualizedPages")]
        public int VirtualizedPages { get; set; }
    }

    private sealed class RemoteVirtualOperationResult
    {
        [JsonPropertyName("applied")]
        public bool Applied { get; set; }

        [JsonPropertyName("modelText")]
        public string ModelText { get; set; } = string.Empty;

        [JsonPropertyName("visibleTarget")]
        public bool VisibleTarget { get; set; }

        [JsonPropertyName("fullRenderCountBefore")]
        public int FullRenderCountBefore { get; set; }

        [JsonPropertyName("fullRenderCountAfter")]
        public int FullRenderCountAfter { get; set; }

        [JsonPropertyName("renderedPagesBefore")]
        public int RenderedPagesBefore { get; set; }

        [JsonPropertyName("renderedPagesAfter")]
        public int RenderedPagesAfter { get; set; }
    }

    private sealed class SelectionSnapshot
    {
        [JsonPropertyName("blockId")]
        public string BlockId { get; set; } = string.Empty;

        [JsonPropertyName("offset")]
        public int Offset { get; set; }
    }
}
