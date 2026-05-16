using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
[DoNotParallelize]
public class DocumentEditorQualitySmokeTests : WasmTestBase
{
    [TestMethod]
    public async Task QualitySmoke_CoversCoreEditingFormattingRevisionsImagesPanelsAndHeaderFooter()
    {
        var page = await OpenDocumentEditorPageAsync(width: 1440, height: 900);
        var host = page.Locator("[data-testid='document-wysiwyg-host']");
        var body = await WaitForWysiwygBodyAsync(host);

        await Assertions.Expect(host.Locator(".tm-wysiwyg-page__header").First).ToContainTextAsync("Tempo Legal - Service agreement");
        await Assertions.Expect(host.Locator(".tm-wysiwyg-page__footer").First).ToContainTextAsync("Confidential - Page 1");
        await Assertions.Expect(host.Locator("img[alt='Provider-managed exhibit']").First).ToBeVisibleAsync();

        await page.Locator("[data-testid='document-side-panel-close']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-side-panel']")).ToHaveCountAsync(0);
        await page.Locator("[data-testid='document-side-panel-edge-toggle']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-side-panel']")).ToBeVisibleAsync();

        await PlaceCaretAtEndOfBodyAsync(page);
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.InsertTextAsync("Phase19 paragraph");
        await page.Keyboard.PressAsync("Shift+Enter");
        await page.Keyboard.InsertTextAsync("soft break");
        await Assertions.Expect(body).ToContainTextAsync("Phase19 paragraph");
        await Assertions.Expect(body).ToContainTextAsync("soft break");

        await SelectTextAsync(page, "provider");
        await page.Locator("[data-testid='document-bold']").ClickAsync();
        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const elements = Array.from(host?.querySelectorAll('*') || []);
                return elements.some(element => {
                    if (!element.textContent?.includes('provider')) return false;
                    const weight = getComputedStyle(element).fontWeight || '';
                    return weight === 'bold' || parseInt(weight, 10) >= 600;
                });
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 3000 });
        var selectedBold = await page.EvaluateAsync<bool>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const elements = Array.from(host?.querySelectorAll('*') || []);
                return elements.some(element => {
                    if (!element.textContent?.includes('provider')) return false;
                    const weight = getComputedStyle(element).fontWeight || '';
                    return weight === 'bold' || parseInt(weight, 10) >= 600;
                });
            }
            """);
        selectedBold.Should().BeTrue("the smoke suite needs to catch broken selection formatting");

        await page.Locator("[data-testid='document-ribbon-tab-review']").ClickAsync();
        await page.Locator("[data-testid='document-open-revisions']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").First).ToContainTextAsync("Priority support");
        await page.Locator("[data-testid='document-revision-accept']").First.ClickAsync();
        await Assertions.Expect(host).ToContainTextAsync("Priority support");

        await page.Locator("[data-testid='document-track-changes']").ClickAsync();
        await PlaceCaretAtEndOfBodyAsync(page);
        await page.Keyboard.InsertTextAsync(" reject-smoke");
        await Assertions.Expect(host.Locator("[data-testid='document-wysiwyg-revision-insert']").Last).ToContainTextAsync("reject-smoke");
        await page.Locator("[data-testid='document-open-revisions']").ClickAsync();
        await page.Locator("[data-testid='document-revision-reject']").Last.ClickAsync();
        await Assertions.Expect(host).Not.ToContainTextAsync("reject-smoke");
    }

    [TestMethod]
    public async Task PerformanceGuard_FastTypingBurstDoesNotTriggerFullRendersOrLargeAverageDelay()
    {
        var page = await OpenDocumentEditorPageAsync(width: 1440, height: 900);
        var host = page.Locator("[data-testid='document-wysiwyg-host']");
        await WaitForWysiwygBodyAsync(host);
        var instanceId = await GetInstanceIdAsync(page);
        await ClearDebugMetricsAsync(page, instanceId);
        await PlaceCaretAtEndOfBodyAsync(page);
        var text = " " + new string('p', 200);

        var stopwatch = Stopwatch.StartNew();
        await page.Keyboard.InsertTextAsync(text);
        await Assertions.Expect(host).ToContainTextAsync(new string('p', 40));
        stopwatch.Stop();
        await page.WaitForTimeoutAsync(850);
        var metrics = await GetDebugMetricsAsync(page, instanceId);

        (stopwatch.Elapsed.TotalMilliseconds / text.Length).Should().BeLessThan(60);
        metrics.FullRenderCount.Should().Be(0, "fast typing must stay in the JS-owned surface");
    }

    [TestMethod]
    public async Task PerformanceGuard_RemotePatchOutsideActiveBlockDoesNotFullRender()
    {
        var page = await OpenDocumentEditorPageAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page.Locator("[data-testid='document-wysiwyg-host']"));
        var instanceId = await GetInstanceIdAsync(page);
        await PlaceCaretInFirstInlineAsync(page, 2);
        await ClearDebugMetricsAsync(page, instanceId);

        var result = await page.EvaluateAsync<RemotePatchResult>(
            """
            (instanceId) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const blocks = Array.from(host.querySelectorAll('.tm-wysiwyg-page__body [data-block-id]'))
                    .filter(block => !block.closest('.tm-wysiwyg-page--virtual'));
                const targetBlock = blocks.find((block, index) => index > 0 && block.querySelector('[data-inline-id]'));
                const inline = targetBlock?.querySelector('[data-inline-id]');
                if (!targetBlock || !inline) throw new Error('remote target block was not found');
                const text = ' remote-phase19';
                const applied = window.tmDocumentWysiwyg.applyRemoteOperationBatch(instanceId, {
                    Operations: [{
                        OperationId: 'phase19-remote-' + Date.now(),
                        Type: 0,
                        Target: {
                            BlockId: targetBlock.getAttribute('data-block-id'),
                            InlineId: inline.getAttribute('data-inline-id'),
                            InlineIndex: 0,
                            Offset: inline.textContent.length
                        },
                        Text: text
                    }]
                });
                const metrics = window.tmDocumentWysiwyg.getDebugMetrics(instanceId);
                return {
                    Success: !!applied?.success,
                    TargetContainsText: targetBlock.textContent.includes(text.trim()),
                    FullRenderCount: metrics.FullRenderCount,
                    RemoteOperationApplyCount: metrics.RemoteOperationApplyCount
                };
            }
            """,
            instanceId);

        result.Success.Should().BeTrue();
        result.TargetContainsText.Should().BeTrue();
        result.FullRenderCount.Should().Be(0);
        result.RemoteOperationApplyCount.Should().Be(1);
        (await ActiveElementIsInWysiwygAsync(page)).Should().BeTrue();
    }

    [TestMethod]
    public async Task PerformanceGuard_ImagePanelAndRevisionsStayInteractiveAfterQuickTyping()
    {
        var page = await OpenDocumentEditorPageAsync(width: 1440, height: 900);
        var host = page.Locator("[data-testid='document-wysiwyg-host']");
        await WaitForWysiwygBodyAsync(host);
        await page.Locator("[data-testid='document-ribbon-tab-review']").ClickAsync();
        await page.Locator("[data-testid='document-open-revisions']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-revision-panel']")).ToBeVisibleAsync();
        await PlaceCaretAtEndOfBodyAsync(page);

        await page.Keyboard.InsertTextAsync(" " + new string('q', 200));
        await Assertions.Expect(host.Locator("img[alt='Provider-managed exhibit']").First).ToBeVisibleAsync();
        await Assertions.Expect(host).ToContainTextAsync(new string('q', 50));

        await page.Locator("[data-testid='document-side-panel-tab-comments']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-comment-rail']")).ToBeVisibleAsync();
        await page.Locator("[data-testid='document-side-panel-tab-revisions']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-revision-panel']")).ToBeVisibleAsync();
        (await ActiveElementIsInWysiwygAsync(page)).Should().BeTrue();
    }

    private async Task<IPage> OpenDocumentEditorPageAsync(int width, int height)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}/document-editor", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await page.WaitForSelectorAsync("[data-testid='document-editor-demo']", new() { State = WaitForSelectorState.Attached, Timeout = 60000 });
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block", new() { State = WaitForSelectorState.Attached, Timeout = 60000 });
        return page;
    }

    private static async Task<ILocator> WaitForWysiwygBodyAsync(ILocator host)
    {
        await Assertions.Expect(host).ToBeVisibleAsync();
        var body = host.Locator(".tm-wysiwyg-page__body[contenteditable]").First;
        await body.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60000 });
        return body;
    }

    private static Task<string> GetInstanceIdAsync(IPage page)
        => page.Locator("[data-testid='document-wysiwyg-host']").GetAttributeAsync("data-instance-id")
            .ContinueWith(task => task.Result ?? throw new InvalidOperationException("WYSIWYG instance id was not found."));

    private static Task ClearDebugMetricsAsync(IPage page, string instanceId)
        => page.EvaluateAsync("instanceId => window.tmDocumentWysiwyg.clearDebugMetrics(instanceId)", instanceId);

    private static Task<DebugMetrics> GetDebugMetricsAsync(IPage page, string instanceId)
        => page.EvaluateAsync<DebugMetrics>("instanceId => window.tmDocumentWysiwyg.getDebugMetrics(instanceId)", instanceId);

    private static Task PlaceCaretAtEndOfBodyAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const body = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page__body[contenteditable="true"]');
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

    private static Task PlaceCaretInFirstInlineAsync(IPage page, int offset)
    {
        return page.EvaluateAsync(
            """
            (offset) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const inline = host?.querySelector('.tm-wysiwyg-page__body [data-inline-id]');
                if (!inline) throw new Error('First inline was not found.');
                const text = inline.firstChild;
                if (!text || text.nodeType !== Node.TEXT_NODE) throw new Error('First inline text node was not found.');
                const range = document.createRange();
                range.setStart(text, Math.max(0, Math.min(offset, text.textContent.length)));
                range.collapse(true);
                const body = inline.closest('[contenteditable="true"]');
                body?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """,
            offset);
    }

    private static Task SelectTextAsync(IPage page, string text)
    {
        return page.EvaluateAsync(
            """
            (text) => {
                const body = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page__body[contenteditable="true"]');
                if (!body) throw new Error('Editable body was not found.');
                const walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT);
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    const index = node.textContent.indexOf(text);
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
                throw new Error('Text was not found: ' + text);
            }
            """,
            text);
    }

    private static async Task<bool> ActiveElementIsInWysiwygAsync(IPage page)
    {
        return await page.EvaluateAsync<bool>(
            """
            () => {
                const active = document.activeElement;
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const selection = window.getSelection();
                return !!host && (
                    (active && host.contains(active))
                    || (selection && selection.rangeCount > 0 && host.contains(selection.anchorNode))
                );
            }
            """);
    }

    private sealed class DebugMetrics
    {
        public int SnapshotApplyCount { get; set; }

        public int FullRenderCount { get; set; }

        public int RemoteOperationApplyCount { get; set; }

        public int RemoteOperationBatchCount { get; set; }
    }

    private sealed class RemotePatchResult
    {
        public bool Success { get; set; }

        public bool TargetContainsText { get; set; }

        public int FullRenderCount { get; set; }

        public int RemoteOperationApplyCount { get; set; }
    }
}
