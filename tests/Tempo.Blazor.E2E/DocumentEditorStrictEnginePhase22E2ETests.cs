using System.Diagnostics;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict E2E tests for document editor performance and virtualization contracts.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase22E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_VirtualizesLongDocumentAndMaterializesCaretPage()
    {
        var page = await OpenDocumentEditorAsync(1280, 900);

        var result = await page.EvaluateAsync<Phase22VirtualizationProbe>(
            """
            () => {
                const sandbox = document.createElement('div');
                sandbox.setAttribute('data-testid', 'phase22-virtual-host');
                sandbox.style.cssText = 'position:absolute;left:0;top:0;width:760px;height:620px;overflow:auto;background:white;z-index:-1;';
                document.body.appendChild(sandbox);

                const engine = window.tmDocumentEditorEngine;
                const instanceId = engine.create(sandbox, {
                    InstanceId: 'phase22-e2e-virtual',
                    VirtualizationBlocksPerPage: 1,
                    VirtualizationThresholdPages: 10,
                    VirtualizationRenderedPageRadius: 0
                }, null);

                engine.loadDocument(instanceId, {
                    Document: {
                        DocumentId: 'phase22-e2e-doc',
                        Blocks: Array.from({ length: 100 }, (_, index) => ({
                            Id: `p${index}`,
                            Type: 'Paragraph',
                            Content: {
                                Type: 'Paragraph',
                                Inlines: [{ Id: `r${index}`, Text: `Virtualized page paragraph ${index}` }]
                            }
                        }))
                    }
                });

                const before = engine.getPageMetrics(instanceId);
                const renderedBlockCountBefore = sandbox.querySelectorAll('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body .tm-wysiwyg-block[data-block-id]').length;
                const placeholderCountBefore = sandbox.querySelectorAll('[data-testid="document-wysiwyg-virtual-page"]').length;

                const scrollPage = engine.scrollToPage(instanceId, 50);
                const afterScroll = engine.getPageMetrics(instanceId);
                const pageFiftyRendered = !!sandbox.querySelector('.tm-wysiwyg-page[data-page-index="50"]:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-block[data-block-id="p50"]');
                const pageZeroVirtualAfterScroll = sandbox.querySelector('.tm-wysiwyg-page[data-page-index="0"]')?.classList.contains('tm-wysiwyg-page--virtual') === true;

                const selectionResult = engine.restoreSelection(instanceId, {
                    anchor: { region: 'Body', blockId: 'p75', offset: 2 },
                    focus: { region: 'Body', blockId: 'p75', offset: 2 },
                    isCollapsed: true
                });
                const scrollBlock = engine.scrollToBlock(instanceId, 'p75');
                const afterSelection = engine.getPageMetrics(instanceId);
                const selectedBlockRendered = !!sandbox.querySelector('.tm-wysiwyg-page[data-page-index="75"]:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-block[data-block-id="p75"]');
                const selection = engine.getSelectionSnapshot(instanceId);
                const debug = engine.getDebugMetrics(instanceId);
                const dispose = engine.dispose(instanceId);
                sandbox.remove();

                return {
                    instanceId,
                    totalPages: before.TotalPages,
                    virtualizationEnabled: before.VirtualizationEnabled === true,
                    activePageBefore: before.ActivePageIndex,
                    virtualizedPagesBefore: before.VirtualizedPages,
                    renderedBlockCountBefore,
                    placeholderCountBefore,
                    scrollPageOk: scrollPage.ok === true,
                    activePageAfterScroll: afterScroll.ActivePageIndex,
                    pageFiftyRendered,
                    pageZeroVirtualAfterScroll,
                    selectionOk: selectionResult.ok === true,
                    scrollBlockOk: scrollBlock.ok === true,
                    activePageAfterSelection: afterSelection.ActivePageIndex,
                    selectedBlockRendered,
                    selectedBlockId: selection.blockId || selection.BlockId || '',
                    selectedOffset: selection.offset ?? selection.Offset ?? -1,
                    maxLiveDomBlockCount: debug.MaxLiveDomBlockCount || 0,
                    disposeOk: dispose.ok === true && dispose.cleanup?.instanceRemoved === true
                };
            }
            """);

        result.InstanceId.Should().Be("phase22-e2e-virtual");
        result.TotalPages.Should().Be(100);
        result.VirtualizationEnabled.Should().BeTrue();
        result.ActivePageBefore.Should().Be(0);
        result.VirtualizedPagesBefore.Should().BeGreaterThan(90);
        result.RenderedBlockCountBefore.Should().Be(1);
        result.PlaceholderCountBefore.Should().BeGreaterThan(90);
        result.ScrollPageOk.Should().BeTrue();
        result.ActivePageAfterScroll.Should().Be(50);
        result.PageFiftyRendered.Should().BeTrue();
        result.PageZeroVirtualAfterScroll.Should().BeTrue();
        result.SelectionOk.Should().BeTrue();
        result.ScrollBlockOk.Should().BeTrue();
        result.ActivePageAfterSelection.Should().Be(75);
        result.SelectedBlockRendered.Should().BeTrue();
        result.SelectedBlockId.Should().Be("p75");
        result.SelectedOffset.Should().Be(2);
        result.MaxLiveDomBlockCount.Should().BeLessThanOrEqualTo(1);
        result.DisposeOk.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Phase22_HeaderTypingWithImageStaysGranularAndFast()
    {
        var page = await OpenDocumentEditorAsync(1280, 900);
        var instanceId = await GetInstanceIdAsync(page);
        var payload = new string('h', 100);

        await LoadHeaderPerformanceDocumentAsync(page, instanceId);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__header").First)
            .ToContainTextAsync("Header before", new() { Timeout = 10000 });
        await ActivateHeaderCaretAtEndAsync(page);
        await page.WaitForFunctionAsync(
            "instanceId => window.tmDocumentEditorEngine.getDebugMetrics(instanceId)?.ActiveRegion === 'Header'",
            instanceId);
        await page.WaitForTimeoutAsync(250);
        await ClearDebugMetricsAsync(page, instanceId);

        var stopwatch = Stopwatch.StartNew();
        await page.Keyboard.TypeAsync(payload, new() { Delay = 0 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__header").First)
            .ToContainTextAsync(payload[..60], new() { Timeout = 10000 });
        stopwatch.Stop();

        var metrics = await WaitForMetricAsync(page, instanceId, metric => metric.InputOperationCount > 0 || metric.KeyDownCount >= payload.Length);
        var bodyText = await page.EvaluateAsync<string>(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body'))
                .map(body => body.innerText || body.textContent || '')
                .join('\n')
            """);

        metrics.ActiveRegion.Should().Be("Header");
        bodyText.Should().Contain("Body text must not be recalculated while header typing is active.");
        metrics.FullRenderCount.Should().Be(0, "header typing should stay in the JS-owned hot path");
        metrics.FullDocumentLayoutCount.Should().Be(0, "header typing beside an image must not trigger whole-document relayout");
        metrics.BodyRenderSwapCount.Should().Be(0, "header typing should not swap body page DOM");
        metrics.MaxInputLatencyMs.Should().BeLessThan(600);
        (stopwatch.Elapsed.TotalMilliseconds / payload.Length).Should().BeLessThan(80);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Phase22_ResizePointerMovesStayPreviewOnlyUntilSingleCommit()
    {
        var page = await OpenDocumentEditorAsync(1280, 900);

        var result = await page.EvaluateAsync<Phase22ObjectTrackProbe>(
            """
            () => {
                const harness = window.tmDocumentEditorEngine.__testHooks.createImageResizeTrackHarness({ threshold: 0 });
                const before = harness.state().modelJson;
                harness.begin(0, 0);
                let afterMoves = null;
                for (let index = 1; index <= 20; index++) {
                    afterMoves = harness.move(index * 5, index * 4);
                }
                const afterCommit = harness.up(120, 88);
                return {
                    unchangedDuringMoves: afterMoves.modelJson === before,
                    commitCountDuringMoves: afterMoves.commitCount,
                    frameCount: afterMoves.performance.objectTrackFrameCount || 0,
                    resizeFrameCount: afterMoves.performance.objectTrackResizeFrameCount || 0,
                    commitCount: afterCommit.commitCount,
                    objectTrackCommitCount: afterCommit.performance.objectTrackCommitCount || 0,
                    resizeCommitCount: afterCommit.performance.objectTrackResizeCommitCount || 0,
                    modelCommitCount: afterCommit.performance.modelCommitCount || 0,
                    modelChangedAfterCommit: afterCommit.modelJson !== before
                };
            }
            """);

        result.UnchangedDuringMoves.Should().BeTrue();
        result.CommitCountDuringMoves.Should().Be(0);
        result.FrameCount.Should().BeGreaterThanOrEqualTo(20);
        result.ResizeFrameCount.Should().BeGreaterThanOrEqualTo(20);
        result.CommitCount.Should().Be(1);
        result.ObjectTrackCommitCount.Should().Be(1);
        result.ResizeCommitCount.Should().Be(1);
        result.ModelCommitCount.Should().Be(1);
        result.ModelChangedAfterCommit.Should().BeTrue();
    }

    private static Task<string> GetInstanceIdAsync(IPage page)
        => page.Locator(DocumentEditorHostSelector).GetAttributeAsync("data-instance-id")
            .ContinueWith(task => task.Result ?? throw new InvalidOperationException("WYSIWYG instance id was not found."));

    private static Task ClearDebugMetricsAsync(IPage page, string instanceId)
        => page.EvaluateAsync("instanceId => window.tmDocumentEditorEngine.clearDebugMetrics(instanceId)", instanceId);

    private static Task<Phase22DebugMetrics> GetDebugMetricsAsync(IPage page, string instanceId)
        => page.EvaluateAsync<Phase22DebugMetrics>("instanceId => window.tmDocumentEditorEngine.getDebugMetrics(instanceId)", instanceId);

    private static async Task<Phase22DebugMetrics> WaitForMetricAsync(IPage page, string instanceId, Func<Phase22DebugMetrics, bool> predicate)
    {
        Phase22DebugMetrics? last = null;
        for (var index = 0; index < 48; index++)
        {
            last = await GetDebugMetricsAsync(page, instanceId);
            if (predicate(last))
            {
                return last;
            }

            await page.WaitForTimeoutAsync(125);
        }

        return last ?? new Phase22DebugMetrics();
    }

    private static Task LoadHeaderPerformanceDocumentAsync(IPage page, string instanceId)
    {
        return page.EvaluateAsync(
            """
            (instanceId) => {
                const dataUrl = 'data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%2272%22 height=%2232%22 viewBox=%220 0 72 32%22%3E%3Crect width=%2272%22 height=%2232%22 rx=%224%22 fill=%22%232563eb%22/%3E%3Ctext x=%2236%22 y=%2221%22 text-anchor=%22middle%22 font-size=%2212%22 fill=%22white%22%3EIMG%3C/text%3E%3C/svg%3E';
                const snapshot = {
                    Document: {
                        DocumentId: 'phase22-header-image-doc',
                        Blocks: [
                            { Id: 'phase22-body-p', Type: 'Paragraph', Content: { Inlines: [
                                { Id: 'phase22-body-r', Text: 'Body text must not be recalculated while header typing is active.' }
                            ] } }
                        ],
                        HeadersFooters: [
                            { Id: 'phase22-header-primary', Region: 'Header', Type: 'Header', Blocks: [
                                { Id: 'phase22-header-p', Type: 'Paragraph', Content: { Inlines: [
                                    { Id: 'phase22-header-before', Text: 'Header before ' },
                                    {
                                        $type: 'drawing',
                                        Id: 'phase22-header-image-run',
                                        ObjectId: 'phase22-header-image',
                                        Kind: 0,
                                        Source: 0,
                                        Url: dataUrl,
                                        AltText: 'Header performance image',
                                        Size: { Width: 72, Height: 32 },
                                        Layout: {
                                            Kind: 1,
                                            Wrap: { Mode: 1, DistanceLeft: 6, DistanceRight: 8, DistanceTop: 0, DistanceBottom: 0 },
                                            Anchor: { BlockId: 'phase22-header-p', Offset: 14, InlineIndex: 1, Region: 'Header', HeaderFooterId: 'phase22-header-primary', MoveWithText: true },
                                            Position: { HorizontalRelativeTo: 2, VerticalRelativeTo: 3, HorizontalAlignment: 0, VerticalAlignment: 1, X: 160, Y: 0 },
                                            Transform: { Width: 72, Height: 32 },
                                            Stacking: { ZIndex: 0, AllowOverlap: false }
                                        }
                                    },
                                    { Id: 'phase22-header-after', Text: ' after image' }
                                ] } }
                            ] },
                            { Id: 'phase22-footer-primary', Region: 'Footer', Type: 'Footer', Blocks: [
                                { Id: 'phase22-footer-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'phase22-footer-r', Text: 'Footer' }] } }
                            ] }
                        ]
                    }
                };

                if (window.tmDocumentEditorRuntime && typeof window.tmDocumentEditorRuntime.loadDocument === 'function') {
                    window.tmDocumentEditorRuntime.loadDocument(instanceId, snapshot, true);
                } else {
                    window.tmDocumentEditorEngine.loadDocument(instanceId, snapshot);
                }
            }
            """,
            instanceId);
    }

    private static Task ActivateHeaderCaretAtEndAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const header = Array.from(host?.querySelectorAll('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__header[contenteditable="true"]') || [])
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0;
                    });
                if (!header) throw new Error('Header region was not found.');
                header.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true }));
                const block = header.querySelector('[data-block-id="phase22-header-p"]') || header;
                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                let lastText = null;
                while (walker.nextNode()) {
                    if ((walker.currentNode.textContent || '').length > 0) lastText = walker.currentNode;
                }

                const range = document.createRange();
                if (lastText) {
                    range.setStart(lastText, lastText.textContent.length);
                } else {
                    range.selectNodeContents(block);
                    range.collapse(false);
                }
                range.collapse(true);
                header.focus({ preventScroll: true });
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """);
    }

    private sealed class Phase22VirtualizationProbe
    {
        [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
        [JsonPropertyName("totalPages")] public int TotalPages { get; set; }
        [JsonPropertyName("virtualizationEnabled")] public bool VirtualizationEnabled { get; set; }
        [JsonPropertyName("activePageBefore")] public int ActivePageBefore { get; set; }
        [JsonPropertyName("virtualizedPagesBefore")] public int VirtualizedPagesBefore { get; set; }
        [JsonPropertyName("renderedBlockCountBefore")] public int RenderedBlockCountBefore { get; set; }
        [JsonPropertyName("placeholderCountBefore")] public int PlaceholderCountBefore { get; set; }
        [JsonPropertyName("scrollPageOk")] public bool ScrollPageOk { get; set; }
        [JsonPropertyName("activePageAfterScroll")] public int ActivePageAfterScroll { get; set; }
        [JsonPropertyName("pageFiftyRendered")] public bool PageFiftyRendered { get; set; }
        [JsonPropertyName("pageZeroVirtualAfterScroll")] public bool PageZeroVirtualAfterScroll { get; set; }
        [JsonPropertyName("selectionOk")] public bool SelectionOk { get; set; }
        [JsonPropertyName("scrollBlockOk")] public bool ScrollBlockOk { get; set; }
        [JsonPropertyName("activePageAfterSelection")] public int ActivePageAfterSelection { get; set; }
        [JsonPropertyName("selectedBlockRendered")] public bool SelectedBlockRendered { get; set; }
        [JsonPropertyName("selectedBlockId")] public string SelectedBlockId { get; set; } = string.Empty;
        [JsonPropertyName("selectedOffset")] public int SelectedOffset { get; set; }
        [JsonPropertyName("maxLiveDomBlockCount")] public int MaxLiveDomBlockCount { get; set; }
        [JsonPropertyName("disposeOk")] public bool DisposeOk { get; set; }
    }

    private sealed class Phase22DebugMetrics
    {
        public int KeyDownCount { get; set; }
        public int InputOperationCount { get; set; }
        public int FullRenderCount { get; set; }
        public int FullDocumentLayoutCount { get; set; }
        public int BodyRenderSwapCount { get; set; }
        public double MaxInputLatencyMs { get; set; }
        public string ActiveRegion { get; set; } = string.Empty;
    }

    private sealed class Phase22ObjectTrackProbe
    {
        [JsonPropertyName("unchangedDuringMoves")] public bool UnchangedDuringMoves { get; set; }
        [JsonPropertyName("commitCountDuringMoves")] public int CommitCountDuringMoves { get; set; }
        [JsonPropertyName("frameCount")] public int FrameCount { get; set; }
        [JsonPropertyName("resizeFrameCount")] public int ResizeFrameCount { get; set; }
        [JsonPropertyName("commitCount")] public int CommitCount { get; set; }
        [JsonPropertyName("objectTrackCommitCount")] public int ObjectTrackCommitCount { get; set; }
        [JsonPropertyName("resizeCommitCount")] public int ResizeCommitCount { get; set; }
        [JsonPropertyName("modelCommitCount")] public int ModelCommitCount { get; set; }
        [JsonPropertyName("modelChangedAfterCommit")] public bool ModelChangedAfterCommit { get; set; }
    }
}
