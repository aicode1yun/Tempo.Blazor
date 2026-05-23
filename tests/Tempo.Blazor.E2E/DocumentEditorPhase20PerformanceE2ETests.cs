using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 20 performance and rendering quality.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase20PerformanceE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase20_TypingPerformanceSmoke_CoversCommentsSearchTrackChangesAndTableCell()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);
        var instanceId = await GetInstanceIdAsync(page);

        await TypeAndAssertLatencyAsync(page, instanceId, "phase20-base-" + new string('a', 100));

        await page.Locator("[data-testid='document-side-panel-tab-comments']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-comment-rail']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await TypeAndAssertLatencyAsync(page, instanceId, "phase20-comments-" + new string('b', 100));

        await ClearDebugMetricsAsync(page, instanceId);
        await SetSearchMarkerOnFirstBlockAsync(page, instanceId);
        var markerMetrics = await GetDebugMetricsAsync(page, instanceId);
        markerMetrics.MarkerRenderCount.Should().BeGreaterThan(0);
        await TypeAndAssertLatencyAsync(page, instanceId, "phase20-search-" + new string('c', 100));

        await SetTrackChangesAsync(page, instanceId, true);
        await TypeAndAssertLatencyAsync(page, instanceId, "phase20-track-" + new string('d', 100));
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-revision-insert']").Last)
            .ToContainTextAsync("phase20-track", new() { Timeout = 10000 });

        await LoadSingleTableDocumentAsync(page, instanceId);
        await page.Locator("[data-testid='document-wysiwyg-host'] td").First.ClickAsync(new() { Position = new() { X = 16, Y = 16 } });
        await TypeAndAssertLatencyAsync(page, instanceId, "phase20-cell-" + new string('e', 100), placeCaret: false);
    }

    [TestMethod]
    public async Task Phase20_ClipboardNormalizationMetric_IsRecordedDuringRichPaste()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);
        var instanceId = await GetInstanceIdAsync(page);
        await ClearDebugMetricsAsync(page, instanceId);
        await PlaceCaretAtEndOfBodyAsync(page);

        await page.EvaluateAsync(
            """
            () => {
                const body = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page__body[contenteditable="true"]');
                const data = new DataTransfer();
                data.setData('text/html', '<p><strong>Phase20 rich paste</strong></p>');
                data.setData('text/plain', 'Phase20 rich paste');
                const event = new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true });
                body.dispatchEvent(event);
            }
            """);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync("Phase20 rich paste", new() { Timeout = 10000 });
        var metrics = await WaitForMetricAsync(page, instanceId, metric => metric.ClipboardNormalizationCount > 0);
        metrics.ClipboardNormalizationCount.Should().BeGreaterThan(0);
        metrics.LastClipboardNormalizationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    public async Task Phase20_LayoutStabilitySmoke_CoversDesktopMobileCompactFloatingAndInspectors()
    {
        var desktop = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await AssertNoViewportOverflowAsync(desktop, "phase20-desktop-overflow");

        await desktop.Locator("[data-testid='document-editor-toolbar-mode']").SelectOptionAsync("Compact");
        await Assertions.Expect(desktop.Locator("[data-testid='document-toolbar']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await AssertToolbarDoesNotOverflowAsync(desktop, "phase20-compact-toolbar-overflow");

        await SelectFirstVisibleTextAsync(desktop);
        await Assertions.Expect(desktop.Locator("[data-testid='document-mini-toolbar']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await AssertElementWithinViewportAsync(desktop, "[data-testid='document-mini-toolbar']", "phase20-floating-toolbar-overflow");

        var figure = desktop.Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image").First;
        await figure.ClickAsync();
        await Assertions.Expect(desktop.Locator("[data-testid='document-image-inspector']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await AssertElementWithinViewportAsync(desktop, "[data-testid='document-image-inspector']", "phase20-image-inspector-overflow");

        var instanceId = await GetInstanceIdAsync(desktop);
        await InsertTableFromRibbonAsync(desktop);
        await FocusFirstTableCellAsync(desktop);
        await Assertions.Expect(desktop.Locator("[data-testid='document-table-toolbar-table-properties']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await desktop.Locator("[data-testid='document-table-toolbar-table-properties']").ClickAsync();
        await Assertions.Expect(desktop.Locator("[data-testid='document-table-properties-panel']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await AssertElementWithinViewportAsync(desktop, "[data-testid='document-table-properties-panel']", "phase20-table-properties-overflow");

        await desktop.SetViewportSizeAsync(390, 844);
        await desktop.WaitForTimeoutAsync(250);
        await AssertNoViewportOverflowAsync(desktop, "phase20-mobile-overflow");
    }

    [TestMethod]
    public async Task Phase20_LongDocumentVirtualizationSmoke_CoversNavigatorSearchAndCommentRail()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);
        var instanceId = await GetInstanceIdAsync(page);

        await LoadLongVirtualizedDocumentAsync(page, instanceId, pages: 34);
        var metrics = await WaitForMetricAsync(page, instanceId, metric => metric.VirtualizationEnabled && metric.VirtualizedPages > 0);
        metrics.TotalPages.Should().BeGreaterThan(20);
        metrics.VirtualizedPages.Should().BeGreaterThan(0);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page--virtual[aria-hidden='true']").First)
            .ToBeAttachedAsync(new() { Timeout = 10000 });

        await page.Locator("[data-testid='document-side-panel-tab-pages']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-page-navigator']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-page-navigator-item']").Nth(30)).ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.Locator("[data-testid='document-page-navigator-item']").Nth(30).ClickAsync();
        await WaitForMetricAsync(page, instanceId, metric => metric.FirstPage <= 30 && metric.LastPage >= 30);

        await ClearDebugMetricsAsync(page, instanceId);
        await SetSearchMarkerAsync(page, instanceId, "phase20-page-10", 0, 7, active: true);
        var skippedMetrics = await GetDebugMetricsAsync(page, instanceId);
        skippedMetrics.MarkerRenderSkippedCount.Should().BeGreaterThan(0);

        await ScrollToPageAsync(page, instanceId, 10);
        var pageTenMetrics = await WaitForMetricAsync(page, instanceId, metric => metric.FirstPage <= 10 && metric.LastPage >= 10);
        pageTenMetrics.FirstPage.Should().BeLessThanOrEqualTo(10);
        pageTenMetrics.LastPage.Should().BeGreaterThanOrEqualTo(10);
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-host'] [data-block-id='phase20-page-10']", new() { State = WaitForSelectorState.Attached, Timeout = 10000 });
        await SetSearchMarkerAsync(page, instanceId, "phase20-page-10", 0, 7, active: true);
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-host'] [data-marker-id='phase20-long-search']", new() { State = WaitForSelectorState.Attached, Timeout = 10000 });

        await UpsertLongDocumentCommentAsync(page, instanceId, "phase20-page-10");
        await page.Locator("[data-testid='document-side-panel-tab-comments']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-comment-thread'][data-comment-id='phase20-comment']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.WaitForFunctionAsync(
            """
            () => {
                const rail = document.querySelector('[data-testid="document-comment-rail"]');
                const thread = document.querySelector('[data-testid="document-comment-thread"][data-comment-id="phase20-comment"]');
                const anchorTop = Number(thread?.dataset.anchorTop || 0);
                return Number(rail?.dataset.alignedCommentCount || 0) >= 1 && Number.isFinite(anchorTop) && anchorTop >= 0;
            }
            """,
            new PageWaitForFunctionOptions { Timeout = 10000 });
    }

    private async Task TypeAndAssertLatencyAsync(IPage page, string instanceId, string text, bool placeCaret = true)
    {
        if (placeCaret)
        {
            await PlaceCaretAtEndOfBodyAsync(page);
        }

        await ClearDebugMetricsAsync(page, instanceId);
        await page.Keyboard.InsertTextAsync(text);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(text[..Math.Min(24, text.Length)], new() { Timeout = 10000 });
        var metrics = await WaitForMetricAsync(page, instanceId, metric => metric.InputOperationCount > 0);

        metrics.FullRenderCount.Should().Be(0, "typing should stay inside the JS-owned surface");
        metrics.MaxInputLatencyMs.Should().BeLessThan(350);
        metrics.AverageInputLatencyMs.Should().BeLessThan(160);
    }

    private static Task<string> GetInstanceIdAsync(IPage page)
        => page.Locator("[data-testid='document-wysiwyg-host']").GetAttributeAsync("data-instance-id")
            .ContinueWith(task => task.Result ?? throw new InvalidOperationException("WYSIWYG instance id was not found."));

    private static Task ClearDebugMetricsAsync(IPage page, string instanceId)
        => page.EvaluateAsync("instanceId => window.tmDocumentEditorEngine.clearDebugMetrics(instanceId)", instanceId);

    private static Task<DebugMetrics> GetDebugMetricsAsync(IPage page, string instanceId)
        => page.EvaluateAsync<DebugMetrics>("instanceId => window.tmDocumentEditorEngine.getDebugMetrics(instanceId)", instanceId);

    private static async Task<DebugMetrics> WaitForMetricAsync(IPage page, string instanceId, Func<DebugMetrics, bool> predicate)
    {
        DebugMetrics? last = null;
        for (var i = 0; i < 40; i++)
        {
            last = await GetDebugMetricsAsync(page, instanceId);
            if (predicate(last))
            {
                return last;
            }

            await page.WaitForTimeoutAsync(125);
        }

        return last ?? new DebugMetrics();
    }

    private static Task PlaceCaretAtEndOfBodyAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const body = Array.from(host?.querySelectorAll('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body[contenteditable="true"]') || []).at(-1);
                if (!body) throw new Error('Editable body was not found.');
                const blocks = Array.from(body.querySelectorAll('[data-block-id]'))
                    .filter(block => !block.closest('figure, table, [aria-hidden="true"]'));
                const target = blocks.at(-1) || body;
                target.closest('[contenteditable="true"]')?.focus();
                const walker = document.createTreeWalker(target, NodeFilter.SHOW_TEXT);
                let last = null;
                while (walker.nextNode()) {
                    if ((walker.currentNode.textContent || '').length > 0) last = walker.currentNode;
                }
                const range = document.createRange();
                if (last) {
                    range.setStart(last, last.textContent.length);
                } else {
                    range.selectNodeContents(target);
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

    private static Task SelectFirstVisibleTextAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const body = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body[contenteditable="true"]');
                const walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT);
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    const text = node.textContent || '';
                    const trimmed = text.trim();
                    if (trimmed.length >= 6) {
                        const index = Math.max(0, text.indexOf(trimmed));
                        const length = Math.min(10, trimmed.length);
                        const range = document.createRange();
                        range.setStart(node, index);
                        range.setEnd(node, index + length);
                        body.focus();
                        const selection = window.getSelection();
                        selection.removeAllRanges();
                        selection.addRange(range);
                        document.dispatchEvent(new Event('selectionchange'));
                        return;
                    }
                }
                throw new Error('No visible selectable text was found.');
            }
            """);
    }

    private static Task SetSearchMarkerOnFirstBlockAsync(IPage page, string instanceId)
    {
        return page.EvaluateAsync(
            """
            (instanceId) => {
                const block = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) [data-block-id]');
                const text = (block?.textContent || '').trim();
                if (!block || text.length < 4) throw new Error('Search marker target block was not found.');
                window.tmDocumentEditorEngine.setSearchMarkers(instanceId, [{
                    id: 'phase20-search-marker',
                    blockId: block.getAttribute('data-block-id'),
                    offset: 0,
                    length: Math.min(8, text.length),
                    active: true
                }]);
            }
            """,
            instanceId);
    }

    private static Task SetSearchMarkerAsync(IPage page, string instanceId, string blockId, int offset, int length, bool active)
    {
        return page.EvaluateAsync(
            """
            ({ instanceId, blockId, offset, length, active }) => {
                window.tmDocumentEditorEngine.setSearchMarkers(instanceId, [{
                    id: 'phase20-long-search',
                    blockId,
                    offset,
                    length,
                    active
                }]);
            }
            """,
            new { instanceId, blockId, offset, length, active });
    }

    private static Task SetTrackChangesAsync(IPage page, string instanceId, bool enabled)
        => page.EvaluateAsync(
            "({ instanceId, enabled }) => window.tmDocumentEditorRuntime.setTrackChangesEnabled(instanceId, enabled)",
            new { instanceId, enabled });

    private static Task ScrollToPageAsync(IPage page, string instanceId, int pageIndex)
        => page.EvaluateAsync(
            "({ instanceId, pageIndex }) => window.tmDocumentEditorEngine.scrollToPage(instanceId, pageIndex)",
            new { instanceId, pageIndex });

    private static Task LoadSingleTableDocumentAsync(IPage page, string instanceId)
    {
        return page.EvaluateAsync(
            """
            (instanceId) => {
                const snapshot = JSON.parse(window.tmDocumentEditorRuntime.getDocument(instanceId));
                const doc = snapshot.Document || snapshot.document;
                doc.Blocks = [{
                    Id: 'phase20-table',
                    Type: 4,
                    Order: 0,
                    Content: {
                        Rows: [{
                            Cells: [{
                                Id: 'phase20-cell',
                                Blocks: [{
                                    Id: 'phase20-cell-p',
                                    Type: 0,
                                    Order: 0,
                                    Content: { Inlines: [{ Id: 'phase20-cell-inline', Text: '' }] }
                                }]
                            }]
                        }]
                    }
                }];
                doc.blocks = doc.Blocks;
                window.tmDocumentEditorRuntime.loadDocument(instanceId, snapshot, true);
            }
            """,
            instanceId);
    }

    private static async Task InsertTableFromRibbonAsync(IPage page)
    {
        await PlaceCaretAtEndOfBodyAsync(page);
        await page.Locator("[data-testid='document-editor-toolbar-mode']").SelectOptionAsync("Ribbon");
        await page.Locator("[data-testid='document-ribbon-tab-insert']").ClickAsync();
        await page.Locator("[data-testid='document-toolbar-table']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-table-grid-picker']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.Locator("[data-testid='document-table-grid-cell-1-1']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table").Last)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    private static Task FocusFirstTableCellAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const cell = document.querySelector('[data-testid="document-wysiwyg-host"] td[data-cell-id], [data-testid="document-wysiwyg-host"] th[data-cell-id]');
                if (!cell) throw new Error('Table cell was not found.');
                cell.click();
                const selection = window.getSelection();
                const range = document.createRange();
                const walker = document.createTreeWalker(cell, NodeFilter.SHOW_TEXT);
                const text = walker.nextNode() ? walker.currentNode : null;
                if (text) {
                    range.setStart(text, 0);
                    range.collapse(true);
                } else {
                    range.selectNodeContents(cell);
                    range.collapse(true);
                }

                selection.removeAllRanges();
                selection.addRange(range);
                cell.closest('[contenteditable="true"]')?.focus();
                document.dispatchEvent(new Event('selectionchange'));
            }
            """);
    }

    private static Task LoadLongVirtualizedDocumentAsync(IPage page, string instanceId, int pages)
    {
        return page.EvaluateAsync(
            """
            ({ instanceId, pages }) => {
                const snapshot = JSON.parse(window.tmDocumentEditorRuntime.getDocument(instanceId));
                const doc = snapshot.Document || snapshot.document;
                const blocks = [];
                for (let i = 0; i < pages; i++) {
                    blocks.push({
                        Id: `phase20-page-${i}`,
                        Type: 0,
                        Order: blocks.length,
                        Content: { Inlines: [{ Id: `phase20-inline-${i}`, Text: `Phase20 page ${i} virtualized paragraph` }] }
                    });
                    if (i < pages - 1) {
                        blocks.push({
                            Id: `phase20-break-${i}`,
                            Type: 6,
                            Order: blocks.length,
                            Content: {}
                        });
                    }
                }
                doc.Blocks = blocks;
                doc.blocks = blocks;
                doc.Comments = [];
                doc.comments = [];
                window.tmDocumentEditorRuntime.loadDocument(instanceId, snapshot, true);
            }
            """,
            new { instanceId, pages });
    }

    private static Task UpsertLongDocumentCommentAsync(IPage page, string instanceId, string blockId)
    {
        return page.EvaluateAsync(
            """
            ({ instanceId, blockId }) => {
                window.tmDocumentEditorEngine.upsertComment(instanceId, {
                    Id: 'phase20-comment',
                    Anchor: { Type: 0, BlockId: blockId },
                    Status: 0,
                    Visibility: 0,
                    Entries: [{
                        Id: 'phase20-comment-entry',
                        Text: 'Phase20 aligned long document comment',
                        Author: { Id: 'phase20', DisplayName: 'Phase20' },
                        CreatedAt: new Date().toISOString()
                    }]
                }, true);
            }
            """,
            new { instanceId, blockId });
    }

    private async Task AssertNoViewportOverflowAsync(IPage page, string screenshotName)
    {
        var json = await page.EvaluateAsync<string>(
            """
            () => JSON.stringify((() => {
                const viewportWidth = window.innerWidth;
                const scrollWidth = Math.max(document.documentElement.scrollWidth, document.body?.scrollWidth || 0);
                const offenders = Array.from(document.querySelectorAll('body *'))
                    .filter(el => {
                        const style = getComputedStyle(el);
                        if (style.display === 'none' || style.visibility === 'hidden') return false;
                        const rect = el.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0 && (rect.right > viewportWidth + 2 || rect.left < -2);
                    })
                    .slice(0, 8)
                    .map(el => String(`${el.tagName.toLowerCase()}${el.getAttribute('data-testid') ? '[data-testid="' + el.getAttribute('data-testid') + '"]' : ''}.${Array.from(el.classList || []).slice(0, 2).join('.')}`))
                    .filter(Boolean);
                return { viewportWidth, scrollWidth, offenders };
            })())
            """);
        var result = JsonSerializer.Deserialize<OverflowResult>(json) ?? new OverflowResult();

        if (result.ScrollWidth > result.ViewportWidth + 2 && result.Offenders.Count > 0)
        {
            await TakeScreenshotAsync(page, screenshotName);
            Assert.Fail($"Viewport has horizontal overflow. viewport={result.ViewportWidth}, scroll={result.ScrollWidth}, offenders={string.Join(", ", result.Offenders)}");
        }
    }

    private async Task AssertToolbarDoesNotOverflowAsync(IPage page, string screenshotName)
    {
        var offenders = await page.EvaluateAsync<string[]>(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-toolbar"] button, [data-testid="document-toolbar"] select, [data-testid="document-toolbar"] input'))
                .filter(el => el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)
                .map(el => el.getAttribute('data-testid') || el.textContent?.trim() || el.tagName.toLowerCase())
            """);
        if (offenders.Length > 0)
        {
            await TakeScreenshotAsync(page, screenshotName);
            Assert.Fail($"Compact toolbar has overflowing controls: {string.Join(", ", offenders)}");
        }
    }

    private async Task AssertElementWithinViewportAsync(IPage page, string selector, string screenshotName)
    {
        var result = await page.EvaluateAsync<ElementBoundsResult>(
            """
            (selector) => {
                const el = document.querySelector(selector);
                const rect = el?.getBoundingClientRect();
                return {
                    found: !!el,
                    left: rect?.left ?? 0,
                    top: rect?.top ?? 0,
                    right: rect?.right ?? 0,
                    bottom: rect?.bottom ?? 0,
                    viewportWidth: window.innerWidth,
                    viewportHeight: window.innerHeight
                };
            }
            """,
            selector);

        if (!result.Found
            || result.Left < -2
            || result.Top < -2
            || result.Right > result.ViewportWidth + 2
            || result.Bottom > result.ViewportHeight + 2)
        {
            await TakeScreenshotAsync(page, screenshotName);
            Assert.Fail($"{selector} is outside viewport: left={result.Left}, top={result.Top}, right={result.Right}, bottom={result.Bottom}, viewport={result.ViewportWidth}x{result.ViewportHeight}");
        }
    }

    private sealed class DebugMetrics
    {
        public int FullRenderCount { get; set; }

        public int InputOperationCount { get; set; }

        public double MaxInputLatencyMs { get; set; }

        public double AverageInputLatencyMs { get; set; }

        public int MarkerRenderCount { get; set; }

        public int MarkerRenderSkippedCount { get; set; }

        public int ClipboardNormalizationCount { get; set; }

        public double LastClipboardNormalizationMs { get; set; }

        public bool VirtualizationEnabled { get; set; }

        public int TotalPages { get; set; }

        public int VirtualizedPages { get; set; }

        public int FirstPage { get; set; }

        public int LastPage { get; set; }
    }

    private sealed class OverflowResult
    {
        [JsonPropertyName("viewportWidth")]
        public int ViewportWidth { get; set; }

        [JsonPropertyName("scrollWidth")]
        public int ScrollWidth { get; set; }

        [JsonPropertyName("offenders")]
        public List<string> Offenders { get; set; } = [];
    }

    private sealed class ElementBoundsResult
    {
        [JsonPropertyName("found")]
        public bool Found { get; set; }

        [JsonPropertyName("left")]
        public double Left { get; set; }

        [JsonPropertyName("top")]
        public double Top { get; set; }

        [JsonPropertyName("right")]
        public double Right { get; set; }

        [JsonPropertyName("bottom")]
        public double Bottom { get; set; }

        [JsonPropertyName("viewportWidth")]
        public double ViewportWidth { get; set; }

        [JsonPropertyName("viewportHeight")]
        public double ViewportHeight { get; set; }
    }
}
