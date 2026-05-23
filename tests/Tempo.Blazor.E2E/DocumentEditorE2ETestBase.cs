using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Shared Playwright helpers for document editor quality and runtime migration tests.
/// </summary>
public abstract class DocumentEditorE2ETestBase : WasmTestBase
{
    private static readonly JsonSerializerOptions StrictJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>Resets mutable demo data before each document editor runtime test.</summary>
    [TestInitialize]
    public Task ResetDocumentEditorDemoAsync()
        => DocumentEditorE2EReset.ResetAsync();

    /// <summary>Opens the normal document editor demo route and waits until the WYSIWYG surface is ready.</summary>
    protected async Task<IPage> OpenDocumentEditorAsync(int width = 1280, int height = 720)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}/document-editor", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);
        return page;
    }

    /// <summary>Types text into the visible document body.</summary>
    protected static async Task EditorTypeAsync(IPage page, string text)
    {
        var body = await WaitForWysiwygBodyAsync(page);
        var textBlock = body.Locator(".tm-wysiwyg-block[data-block-id]:not(figure):not(table):not(hr)").First;
        await textBlock.ClickAsync(new() { Position = new() { X = 12, Y = 12 } });
        await page.Keyboard.TypeAsync(text);
    }

    /// <summary>Presses the editor undo shortcut.</summary>
    protected static Task EditorPressUndoAsync(IPage page)
    {
        return page.Keyboard.PressAsync("Control+Z");
    }

    /// <summary>Reads visible document body text from non-virtual pages.</summary>
    protected static Task<string> ReadEditorPlainTextAsync(IPage page)
    {
        return page.EvaluateAsync<string>(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body'))
                .map(body => body.innerText || body.textContent || '')
                .join('\n')
            """);
    }

    /// <summary>Reads current toolbar formatting state from controls and the JS debug bridge.</summary>
    protected static Task<DocumentEditorToolbarState> ReadToolbarStateAsync(IPage page)
    {
        return page.EvaluateAsync<DocumentEditorToolbarState>(
            """
            () => {
                const pressed = selector => {
                    const el = document.querySelector(selector);
                    return el?.getAttribute('aria-pressed') === 'true'
                        || el?.classList?.contains('is-active')
                        || el?.classList?.contains('tm-document-editor__ribbon-button--active')
                        || false;
                };
                const value = selector => {
                    const el = document.querySelector(selector);
                    return el?.value || el?.textContent?.trim() || '';
                };
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const formatting = window.tmDocumentEditorEngine?.getFormattingState?.(instanceId) || {};
                return {
                    bold: !!(formatting.bold ?? formatting.Bold ?? pressed('[data-testid="document-bold"]')),
                    italic: !!(formatting.italic ?? formatting.Italic ?? pressed('[data-testid="document-italic"]')),
                    underline: !!(formatting.underline ?? formatting.Underline ?? pressed('[data-testid="document-underline"]')),
                    fontFamily: String(formatting.fontFamily ?? formatting.FontFamily ?? value('[data-testid="document-font-family"]')),
                    fontSize: String(formatting.fontSize ?? formatting.FontSize ?? value('[data-testid="document-font-size"]')),
                    alignment: String(formatting.alignment ?? formatting.Alignment ?? '')
                };
            }
            """);
    }

    /// <summary>Waits briefly for the editor to settle and captures a full-page screenshot.</summary>
    protected async Task CaptureStableEditorScreenshotAsync(IPage page, string name)
    {
        await page.WaitForTimeoutAsync(250);
        await TakeScreenshotAsync(page, name);
    }

    /// <summary>Starts collecting console and page errors for strict document editor scenarios.</summary>
    protected static DocumentEditorConsoleCapture BeginDocumentEditorConsoleCapture(IPage page)
        => new(page);

    /// <summary>Clicks a visual text line using real mouse coordinates.</summary>
    protected static async Task<DocumentEditorVisualLineTarget> ClickDocumentEditorVisualLineAsync(
        IPage page,
        int lineIndex = 0,
        double xRatio = 0.35,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
    {
        var target = await GetDocumentEditorVisualLineAsync(page, lineIndex, hostSelector);
        var x = target.Rect.X + Math.Clamp(xRatio, 0.02, 0.98) * Math.Max(1, target.Rect.Width);
        var y = target.Rect.Y + target.Rect.Height / 2;
        await page.Mouse.ClickAsync((float)x, (float)y);
        return target;
    }

    /// <summary>Drags a text selection between visual lines with real mouse movement.</summary>
    protected static async Task DragDocumentEditorTextSelectionAsync(
        IPage page,
        int fromLineIndex,
        int toLineIndex,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
    {
        var from = await GetDocumentEditorVisualLineAsync(page, fromLineIndex, hostSelector);
        var to = await GetDocumentEditorVisualLineAsync(page, toLineIndex, hostSelector);
        var startX = from.Rect.X + Math.Min(6, Math.Max(2, from.Rect.Width / 4));
        var startY = from.Rect.Y + from.Rect.Height / 2;
        var endX = to.Rect.X + Math.Max(8, to.Rect.Width - 6);
        var endY = to.Rect.Y + to.Rect.Height / 2;
        await page.Mouse.MoveAsync((float)startX, (float)startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)endX, (float)endY, new() { Steps = 8 });
        await page.Mouse.UpAsync();
    }

    /// <summary>Types text one character at a time and captures strict frame probes after every character.</summary>
    protected async Task<IReadOnlyList<DocumentEditorFrameProbe>> TypeDocumentEditorTextByCharactersWithFrameProbesAsync(
        IPage page,
        string text,
        string behavior,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
    {
        var probes = new List<DocumentEditorFrameProbe>();
        foreach (var ch in text)
        {
            await page.Keyboard.TypeAsync(ch.ToString());
            var sequence = await RunDocumentEditorActionWithFrameProbesAsync(
                page,
                $"{behavior}: typed '{Printable(ch)}'",
                () => Task.CompletedTask,
                hostSelector);
            probes.AddRange(sequence);
            await AssertStrictFrameProbesCleanAsync(page, sequence, $"{behavior}: typed '{Printable(ch)}'", hostSelector);
        }

        return probes;
    }

    /// <summary>Clicks a toolbar command by test id, aria label, title, or visible text.</summary>
    protected static async Task ClickDocumentEditorToolbarCommandAsync(IPage page, string command)
    {
        var selector = BuildCommandSelector(command);
        var locator = page.Locator(selector).First;
        await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await locator.ClickAsync();
    }

    /// <summary>Opens a context menu on a target and clicks a command by test id, aria label, title, or visible text.</summary>
    protected static async Task ExecuteDocumentEditorContextMenuCommandAsync(IPage page, string targetSelector, string command)
    {
        var target = page.Locator(targetSelector).First;
        await target.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await target.ClickAsync(new() { Button = MouseButton.Right });
        var locator = page.Locator(BuildCommandSelector(command)).First;
        await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await locator.ClickAsync();
    }

    /// <summary>Drags an image or its resize handle with real mouse movement.</summary>
    protected static async Task DragDocumentEditorImageResizeAsync(
        IPage page,
        double deltaX,
        double deltaY,
        int imageIndex = 0,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
    {
        var handle = await page.EvaluateAsync<DocumentEditorPointProbe>(
            """
            ({ hostSelector, imageIndex }) => {
                const host = document.querySelector(hostSelector);
                const figures = Array.from(host?.querySelectorAll('figure[data-block-id], figure.tm-wysiwyg-image, .tm-render-image-widget, [data-testid="phase18-image"]') || []);
                const figure = figures[Math.max(0, Number(imageIndex) || 0)] || figures[0];
                if (!figure) throw new Error('No image figure found for strict image drag/resize helper.');
                const handle = figure.querySelector('[data-resize-handle="se"], [data-testid$="resize-handle-se"], .tm-wysiwyg-object-resize-handle--se, .tm-wysiwyg-image__resize-handle') || figure;
                const rect = handle.getBoundingClientRect();
                return { x: rect.x + rect.width / 2, y: rect.y + rect.height / 2 };
            }
            """,
            new { hostSelector, imageIndex });
        await page.Mouse.MoveAsync((float)handle.X, (float)handle.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(handle.X + deltaX), (float)(handle.Y + deltaY), new() { Steps = 10 });
        await page.Mouse.UpAsync();
    }

    /// <summary>Captures a screenshot clipped to the editor page or host and attaches it to the test result.</summary>
    protected async Task<string> CaptureDocumentEditorPageScreenshotAsync(
        IPage page,
        string name,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
    {
        var directory = TestContext.TestResultsDirectory ?? ".";
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{SanitizeFileName(name)}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        var rect = await page.EvaluateAsync<DocumentEditorRectProbe?>(
            """
            (hostSelector) => {
                const host = document.querySelector(hostSelector);
                const page = host?.querySelector('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual), .tm-render-page') || host;
                if (!page) return null;
                const rect = page.getBoundingClientRect();
                return {
                    x: Math.max(0, rect.x),
                    y: Math.max(0, rect.y),
                    width: Math.max(1, Math.min(rect.width, window.innerWidth - Math.max(0, rect.x))),
                    height: Math.max(1, Math.min(rect.height, window.innerHeight - Math.max(0, rect.y)))
                };
            }
            """,
            hostSelector);

        await page.ScreenshotAsync(new()
        {
            Path = path,
            Type = ScreenshotType.Png,
            FullPage = rect is null,
            Clip = rect is null ? null : new() { X = (float)rect.X, Y = (float)rect.Y, Width = (float)rect.Width, Height = (float)rect.Height }
        });
        TestContext.AddResultFile(path);
        return path;
    }

    /// <summary>Runs an action and captures strict probes before and after visible browser/layout milestones.</summary>
    protected async Task<IReadOnlyList<DocumentEditorFrameProbe>> RunDocumentEditorActionWithFrameProbesAsync(
        IPage page,
        string behavior,
        Func<Task> action,
        string hostSelector = "[data-testid='document-wysiwyg-host']",
        bool includeSaveReloadProbe = false)
    {
        var probes = new List<DocumentEditorFrameProbe>
        {
            await CaptureStrictFrameProbeAsync(page, $"{behavior}: before", hostSelector)
        };

        await action();
        await page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => resolve()))");
        probes.Add(await CaptureStrictFrameProbeAsync(page, $"{behavior}: after animation frame", hostSelector));
        await page.WaitForTimeoutAsync(50);
        probes.Add(await CaptureStrictFrameProbeAsync(page, $"{behavior}: after 50 ms", hostSelector));
        await page.WaitForTimeoutAsync(100);
        probes.Add(await CaptureStrictFrameProbeAsync(page, $"{behavior}: after 150 ms", hostSelector));
        await page.EvaluateAsync("() => new Promise(resolve => window.requestIdleCallback ? window.requestIdleCallback(resolve, { timeout: 50 }) : window.setTimeout(resolve, 1))");
        probes.Add(await CaptureStrictFrameProbeAsync(page, $"{behavior}: after idle layout", hostSelector));

        if (includeSaveReloadProbe)
        {
            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
            await WaitForDocumentEditorReadyAsync(page);
            probes.Add(await CaptureStrictFrameProbeAsync(page, $"{behavior}: after save/reload", hostSelector));
        }

        return probes;
    }

    /// <summary>Captures one strict visual frame probe.</summary>
    protected static Task<DocumentEditorFrameProbe> CaptureStrictFrameProbeAsync(
        IPage page,
        string stage = "probe",
        string hostSelector = "[data-testid='document-wysiwyg-host']")
        => page.EvaluateAsync<DocumentEditorFrameProbe>(
            """
            ({ stage, hostSelector }) => {
                const host = document.querySelector(hostSelector);
                const pageBody = host?.querySelector('.tm-wysiwyg-page__body, [data-render-frame="body"]') || host;
                const pageRect = toRect(pageBody?.getBoundingClientRect());
                const textRects = collectTextRects(host);
                const imageRects = collectImageRects(host);
                const captionRects = collectCaptionRects(host);
                const toolbarRects = collectToolbarRects();
                const floatingToolbarVisible = isVisible(document.querySelector('[data-testid="document-wysiwyg-mini-toolbar"], [data-testid="document-wysiwyg-image-toolbar"], .tm-document-editor__floating-root'));
                const contextMenuVisible = isVisible(document.querySelector('[role="menu"], [data-testid*="context-menu"], .tm-context-menu'));
                const sidePanelClipping = collectSidePanelClipping();
                const selection = getSelectionProbe();
                const issues = [];
                let textTextOverlapCount = 0;
                let textImageOverlapCount = 0;
                let textCaptionOverlapCount = 0;
                let toolbarOverlapCount = 0;

                for (let i = 0; i < textRects.length; i++) {
                    for (let j = i + 1; j < textRects.length; j++) {
                        if (textRects[i].sourceId === textRects[j].sourceId) continue;
                        if (intersects(textRects[i].rect, textRects[j].rect, 1.5)) {
                            textTextOverlapCount++;
                            issues.push(`text/text overlap: ${textRects[i].blockId || '?'} <-> ${textRects[j].blockId || '?'}`);
                        }
                    }
                }
                for (const text of textRects) {
                    for (const image of imageRects) {
                        if (intersects(text.rect, image.rect, 1.5)) {
                            textImageOverlapCount++;
                            issues.push(`text/image overlap: ${text.blockId || '?'} -> ${image.blockId || '?'}`);
                        }
                    }
                    for (const caption of captionRects) {
                        if (caption.blockId !== text.blockId && intersects(text.rect, caption.rect, 1.5)) {
                            textCaptionOverlapCount++;
                            issues.push(`text/caption overlap: ${text.blockId || '?'} -> ${caption.blockId || '?'}`);
                        }
                    }
                }
                for (let i = 0; i < toolbarRects.length; i++) {
                    for (let j = i + 1; j < toolbarRects.length; j++) {
                        if (toolbarRects[i].id === toolbarRects[j].id) continue;
                        if (intersects(toolbarRects[i].rect, toolbarRects[j].rect, 3)) {
                            toolbarOverlapCount++;
                            issues.push(`toolbar overlap: ${toolbarRects[i].id} <-> ${toolbarRects[j].id}`);
                        }
                    }
                }
                for (const clipped of sidePanelClipping) {
                    issues.push(`side panel clipping: ${clipped.id}`);
                }
                if (selection.isCollapsed && selection.blockId && !rectInside(selection.caretRect, pageRect, 2)) {
                    issues.push(`caret outside active page body: ${selection.blockId}`);
                }

                const instanceId = host?.getAttribute('data-instance-id') || '';
                const engineDebug = getEngineDebug(instanceId);
                const engineProbe = getEngineLayoutProbe(instanceId);
                const engineArtifact = getEngineArtifact(instanceId, stage);

                return {
                    stage,
                    instanceId,
                    documentText: host?.innerText || host?.textContent || '',
                    issues,
                    textRectCount: textRects.length,
                    imageRectCount: imageRects.length,
                    captionRectCount: captionRects.length,
                    textTextOverlapCount,
                    textImageOverlapCount,
                    textCaptionOverlapCount,
                    toolbarOverlapCount,
                    floatingToolbarVisible,
                    contextMenuVisible,
                    sidePanelClippingCount: sidePanelClipping.length,
                    caretInsideActivePageBody: !selection.isCollapsed || !selection.blockId || rectInside(selection.caretRect, pageRect, 2),
                    selection,
                    selectionText: window.getSelection()?.toString() || '',
                    pageBodyRect: pageRect,
                    engineDebugJson: safeJson(engineDebug),
                    engineLayoutProbeJson: safeJson(engineProbe),
                    engineArtifactJson: safeJson(engineArtifact)
                };

                function collectTextRects(root) {
                    const result = [];
                    if (!root) return result;
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            if (!node.nodeValue?.trim()) return NodeFilter.FILTER_REJECT;
                            const parent = node.parentElement;
                            if (!parent || parent.closest('figure, [role="menu"], [data-testid*="toolbar"], .tm-document-editor__ribbon, [data-testid="document-side-panel"], .tm-document-editor__floating-root')) {
                                return NodeFilter.FILTER_REJECT;
                            }
                            if (!isVisible(parent)) {
                                return NodeFilter.FILTER_REJECT;
                            }
                            return NodeFilter.FILTER_ACCEPT;
                        }
                    });
                    let source = 0;
                    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                        const range = document.createRange();
                        range.selectNodeContents(node);
                        const block = node.parentElement?.closest('[data-block-id], [data-render-block-id]');
                        for (const rect of Array.from(range.getClientRects())) {
                            if (rect.width > 0.5 && rect.height > 0.5) {
                                result.push({
                                    sourceId: `text-${source}`,
                                    blockId: block?.getAttribute('data-block-id') || block?.getAttribute('data-render-block-id') || '',
                                    rect: toRect(rect)
                                });
                            }
                        }
                        source++;
                    }
                    return result;
                }

                function collectImageRects(root) {
                    return Array.from(root?.querySelectorAll('figure[data-block-id], figure.tm-wysiwyg-image, .tm-render-image-widget, [data-testid="phase18-image"]') || [])
                        .map((figure, index) => ({
                            blockId: figure.getAttribute('data-block-id') || figure.getAttribute('data-render-block-id') || `image-${index}`,
                            rect: toRect((figure.querySelector('img') || figure).getBoundingClientRect())
                        }))
                        .filter(item => item.rect.width > 0.5 && item.rect.height > 0.5);
                }

                function collectCaptionRects(root) {
                    return Array.from(root?.querySelectorAll('figcaption, [data-testid*="caption"]') || [])
                        .map((caption, index) => {
                            const figure = caption.closest('[data-block-id], [data-render-block-id], figure');
                            return {
                                blockId: figure?.getAttribute('data-block-id') || figure?.getAttribute('data-render-block-id') || `caption-${index}`,
                                rect: toRect(caption.getBoundingClientRect())
                            };
                        })
                        .filter(item => item.rect.width > 0.5 && item.rect.height > 0.5);
                }

                function collectToolbarRects() {
                    const selector = '[data-testid*="toolbar"], .tm-document-editor__ribbon, .tm-document-editor__floating-root, [data-testid="phase18-toolbar"]';
                    const all = Array.from(document.querySelectorAll(selector));
                    const nodes = all
                        .filter(node => isVisible(node) && hasVisibleToolbarSurface(node))
                        .filter(node => !all
                            .some(other => other !== node && other.contains(node) && isVisible(other)))
                        .map((node, index) => ({ id: node.getAttribute('data-testid') || node.className || `toolbar-${index}`, rect: toRect(node.getBoundingClientRect()) }));
                    return nodes;
                }

                function hasVisibleToolbarSurface(node) {
                    if (!node) return false;
                    if (!node.classList?.contains('tm-document-editor__floating-root')) return true;
                    return Array.from(node.children || []).some(isVisible);
                }

                function collectSidePanelClipping() {
                    return Array.from(document.querySelectorAll('[data-testid="document-side-panel"], .tm-document-editor__side-panel, [data-testid="phase18-side-panel"]'))
                        .filter(isVisible)
                        .flatMap((panel, index) => {
                            const panelRect = toRect(panel.getBoundingClientRect());
                            return Array.from(panel.querySelectorAll('button, input, textarea, select, [role="button"], [data-testid]'))
                                .filter(isVisible)
                                .filter(child => !rectInside(toRect(child.getBoundingClientRect()), panelRect, 1))
                                .map(child => ({ id: child.getAttribute('data-testid') || child.textContent?.trim() || `side-panel-child-${index}` }));
                        });
                }

                function getSelectionProbe() {
                    const selection = window.getSelection();
                    const empty = { isCollapsed: false, blockId: '', offset: -1, caretRect: zeroRect() };
                    if (!selection || selection.rangeCount === 0) return empty;
                    const range = selection.getRangeAt(0).cloneRange();
                    const element = selection.focusNode?.nodeType === Node.ELEMENT_NODE ? selection.focusNode : selection.focusNode?.parentElement;
                    const block = element?.closest?.('[data-block-id], [data-render-block-id]');
                    let offset = -1;
                    if (block) {
                        const pre = document.createRange();
                        pre.selectNodeContents(block);
                        try {
                            pre.setEnd(selection.focusNode, selection.focusOffset);
                            offset = pre.toString().length;
                        } catch { offset = -1; }
                    }
                    return {
                        isCollapsed: selection.isCollapsed,
                        blockId: block?.getAttribute('data-block-id') || block?.getAttribute('data-render-block-id') || '',
                        offset,
                        caretRect: getCaretRect(range)
                    };
                }

                function getCaretRect(range) {
                    const rect = range.getBoundingClientRect();
                    if (rect && rect.height > 0) return toRect(rect);
                    const marker = document.createElement('span');
                    marker.textContent = '\u200b';
                    range.insertNode(marker);
                    const result = toRect(marker.getBoundingClientRect());
                    marker.remove();
                    return result;
                }

                function getEngineDebug(instanceId) {
                    try {
                        return window.tmDocumentEditorEngine?.getDebugSnapshot?.(instanceId)
                            || window.tmDocumentEditorRuntime?.getDebugSnapshot?.(instanceId)
                            || window.tmDocumentEditorEngine?.getDebugSnapshot?.(instanceId)
                            || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function getEngineLayoutProbe(instanceId) {
                    try {
                        return window.tmDocumentEditorEngine?.getLayoutProbe?.(instanceId) || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function getEngineArtifact(instanceId, reason) {
                    try {
                        return window.tmDocumentEditorEngine?.exportFailureArtifact?.(instanceId, reason) || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function safeJson(value) {
                    try { return JSON.stringify(value ?? null); }
                    catch (error) { return JSON.stringify({ error: String(error) }); }
                }

                function intersects(a, b, tolerance) {
                    const x = Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x));
                    const y = Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
                    return x * y > tolerance;
                }

                function rectInside(inner, outer, tolerance) {
                    if (!outer || outer.width <= 0 || outer.height <= 0 || !inner || inner.width < 0) return true;
                    return inner.x >= outer.x - tolerance
                        && inner.y >= outer.y - tolerance
                        && inner.x + Math.max(1, inner.width) <= outer.x + outer.width + tolerance
                        && inner.y + Math.max(1, inner.height) <= outer.y + outer.height + tolerance;
                }

                function isVisible(node) {
                    if (!node) return false;
                    const rect = node.getBoundingClientRect();
                    const style = window.getComputedStyle(node);
                    return rect.width > 0.5 && rect.height > 0.5 && style.visibility !== 'hidden' && style.display !== 'none' && Number(style.opacity || 1) > 0.01;
                }

                function zeroRect() {
                    return { x: 0, y: 0, width: 0, height: 0 };
                }

                function toRect(rect) {
                    if (!rect) return zeroRect();
                    return {
                        x: Number(rect.x || rect.left || 0),
                        y: Number(rect.y || rect.top || 0),
                        width: Number(rect.width || 0),
                        height: Number(rect.height || 0)
                    };
                }
            }
            """,
            new { stage, hostSelector });

    /// <summary>Fails if any captured frame probe reports a strict visual issue.</summary>
    protected async Task AssertStrictFrameProbesCleanAsync(
        IPage page,
        IReadOnlyList<DocumentEditorFrameProbe> probes,
        string behavior,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
    {
        var failed = probes.FirstOrDefault(probe => probe.Issues.Length > 0);
        if (failed is null)
        {
            return;
        }

        var artifact = await CaptureStrictFailureArtifactsAsync(page, behavior, failed, hostSelector);
        throw new AssertFailedException(CreateStrictEngineFailureMessage(behavior, failed, artifact));
    }

    /// <summary>Captures screenshot and JSON artifact for a strict E2E failure.</summary>
    protected async Task<DocumentEditorStrictFailureArtifact> CaptureStrictFailureArtifactsAsync(
        IPage page,
        string behavior,
        DocumentEditorFrameProbe probe,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
    {
        var safe = SanitizeFileName($"document_editor_strict_{behavior}");
        var screenshotPath = await CaptureDocumentEditorPageScreenshotAsync(page, safe, hostSelector);
        var directory = TestContext.TestResultsDirectory ?? ".";
        Directory.CreateDirectory(directory);
        var artifactPath = Path.Combine(directory, $"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        var payload = new
        {
            behavior,
            capturedAt = DateTimeOffset.Now,
            screenshotPath,
            probe,
            consoleErrors = Array.Empty<string>()
        };
        await File.WriteAllTextAsync(artifactPath, JsonSerializer.Serialize(payload, StrictJsonOptions));
        TestContext.AddResultFile(artifactPath);
        return new(screenshotPath, artifactPath);
    }

    /// <summary>Builds a strict failure message that always includes human behavior and artifact paths.</summary>
    protected static string CreateStrictEngineFailureMessage(
        string behavior,
        DocumentEditorFrameProbe probe,
        DocumentEditorStrictFailureArtifact artifact)
        => $"{behavior} is broken at probe '{probe.Stage}'. Issues: {string.Join("; ", probe.Issues)}. Screenshot: {artifact.ScreenshotPath}. JSON artifact: {artifact.JsonArtifactPath}.";

    /// <summary>Asserts that the current native selection contains expected text.</summary>
    protected static async Task AssertDocumentEditorSelectionContainsTextAsync(IPage page, string expectedText)
    {
        var selectionText = await page.EvaluateAsync<string>("() => window.getSelection()?.toString() || ''");
        if (!selectionText.Contains(expectedText, StringComparison.Ordinal))
        {
            throw new AssertFailedException($"Selection highlight over expected text is broken. Expected selection to contain '{expectedText}', actual selection was '{selectionText}'.");
        }
    }

    /// <summary>Waits until the document editor host has rendered at least one WYSIWYG block.</summary>
    protected static async Task WaitForDocumentEditorReadyAsync(IPage page)
    {
        await page.WaitForSelectorAsync("[data-testid='document-editor-demo']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 60000
        });
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 60000
        });
    }

    /// <summary>Returns the visible contenteditable body from the first non-virtual page.</summary>
    protected static async Task<ILocator> WaitForWysiwygBodyAsync(IPage page)
    {
        var host = page.Locator("[data-testid='document-wysiwyg-host']");
        await Assertions.Expect(host).ToBeVisibleAsync();
        var body = host.Locator(".tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body[contenteditable]").First;
        await body.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60000 });
        return body;
    }

    private static Task<DocumentEditorVisualLineTarget> GetDocumentEditorVisualLineAsync(IPage page, int lineIndex, string hostSelector)
        => page.EvaluateAsync<DocumentEditorVisualLineTarget>(
            """
            ({ hostSelector, lineIndex }) => {
                const host = document.querySelector(hostSelector);
                if (!host) throw new Error(`Strict visual line helper could not find host: ${hostSelector}`);
                const lines = [];
                const walker = document.createTreeWalker(host, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        if (!node.nodeValue?.trim()) return NodeFilter.FILTER_REJECT;
                        const parent = node.parentElement;
                        if (!parent || parent.closest('figure, table, [role="menu"], [data-testid*="toolbar"], .tm-document-editor__ribbon, [data-testid="document-side-panel"]')) {
                            return NodeFilter.FILTER_REJECT;
                        }
                        return NodeFilter.FILTER_ACCEPT;
                    }
                });
                let nodeIndex = 0;
                for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                    const range = document.createRange();
                    range.selectNodeContents(node);
                    const block = node.parentElement?.closest('[data-block-id], [data-render-block-id]');
                    for (const rect of Array.from(range.getClientRects())) {
                        if (rect.width <= 0.5 || rect.height <= 0.5) continue;
                        lines.push({
                            nodeIndex,
                            blockId: block?.getAttribute('data-block-id') || block?.getAttribute('data-render-block-id') || '',
                            text: node.nodeValue.trim(),
                            rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height }
                        });
                    }
                    nodeIndex++;
                }
                lines.sort((a, b) => a.rect.y - b.rect.y || a.rect.x - b.rect.x);
                const target = lines[Math.max(0, Math.min(lines.length - 1, Number(lineIndex) || 0))];
                if (!target) throw new Error('Strict visual line helper could not find a measurable text line.');
                return target;
            }
            """,
            new { hostSelector, lineIndex });

    private static string BuildCommandSelector(string command)
    {
        var escaped = command.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
        return $"[data-testid='{escaped}'], [aria-label='{escaped}'], [title='{escaped}'], button:has-text('{escaped}'), [role='menuitem']:has-text('{escaped}')";
    }

    private static string Printable(char ch)
        => ch == ' ' ? "space" : ch.ToString();

    private static string SanitizeFileName(string value)
        => string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_'));
}

/// <summary>Best-effort toolbar state snapshot for document editor E2E tests.</summary>
public sealed record DocumentEditorToolbarState(
    [property: JsonPropertyName("bold")] bool Bold,
    [property: JsonPropertyName("italic")] bool Italic,
    [property: JsonPropertyName("underline")] bool Underline,
    [property: JsonPropertyName("fontFamily")] string FontFamily,
    [property: JsonPropertyName("fontSize")] string FontSize,
    [property: JsonPropertyName("alignment")] string Alignment);

/// <summary>Captured strict visual frame probe for document editor E2E gates.</summary>
public sealed class DocumentEditorFrameProbe
{
    [JsonPropertyName("stage")] public string Stage { get; set; } = string.Empty;
    [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
    [JsonPropertyName("documentText")] public string DocumentText { get; set; } = string.Empty;
    [JsonPropertyName("issues")] public string[] Issues { get; set; } = [];
    [JsonPropertyName("textRectCount")] public int TextRectCount { get; set; }
    [JsonPropertyName("imageRectCount")] public int ImageRectCount { get; set; }
    [JsonPropertyName("captionRectCount")] public int CaptionRectCount { get; set; }
    [JsonPropertyName("textTextOverlapCount")] public int TextTextOverlapCount { get; set; }
    [JsonPropertyName("textImageOverlapCount")] public int TextImageOverlapCount { get; set; }
    [JsonPropertyName("textCaptionOverlapCount")] public int TextCaptionOverlapCount { get; set; }
    [JsonPropertyName("toolbarOverlapCount")] public int ToolbarOverlapCount { get; set; }
    [JsonPropertyName("floatingToolbarVisible")] public bool FloatingToolbarVisible { get; set; }
    [JsonPropertyName("contextMenuVisible")] public bool ContextMenuVisible { get; set; }
    [JsonPropertyName("sidePanelClippingCount")] public int SidePanelClippingCount { get; set; }
    [JsonPropertyName("caretInsideActivePageBody")] public bool CaretInsideActivePageBody { get; set; }
    [JsonPropertyName("selection")] public DocumentEditorSelectionProbe Selection { get; set; } = new();
    [JsonPropertyName("selectionText")] public string SelectionText { get; set; } = string.Empty;
    [JsonPropertyName("pageBodyRect")] public DocumentEditorRectProbe PageBodyRect { get; set; } = new();
    [JsonPropertyName("engineDebugJson")] public string EngineDebugJson { get; set; } = "null";
    [JsonPropertyName("engineLayoutProbeJson")] public string EngineLayoutProbeJson { get; set; } = "null";
    [JsonPropertyName("engineArtifactJson")] public string EngineArtifactJson { get; set; } = "null";
}

/// <summary>Logical/native selection state captured by strict frame probes.</summary>
public sealed class DocumentEditorSelectionProbe
{
    [JsonPropertyName("isCollapsed")] public bool IsCollapsed { get; set; }
    [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
    [JsonPropertyName("offset")] public int Offset { get; set; }
    [JsonPropertyName("caretRect")] public DocumentEditorRectProbe CaretRect { get; set; } = new();
}

/// <summary>Visual text line target for human-like mouse helpers.</summary>
public sealed class DocumentEditorVisualLineTarget
{
    [JsonPropertyName("nodeIndex")] public int NodeIndex { get; set; }
    [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    [JsonPropertyName("rect")] public DocumentEditorRectProbe Rect { get; set; } = new();
}

/// <summary>Simple point value returned by browser-side hit tests.</summary>
public sealed class DocumentEditorPointProbe
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
}

/// <summary>Simple rectangle value returned by browser-side visual probes.</summary>
public sealed class DocumentEditorRectProbe
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
}

/// <summary>Paths attached to a strict E2E failure.</summary>
public sealed record DocumentEditorStrictFailureArtifact(string ScreenshotPath, string JsonArtifactPath);

/// <summary>Collects browser console and page errors during strict document editor tests.</summary>
public sealed class DocumentEditorConsoleCapture : IDisposable
{
    private readonly IPage _page;
    private readonly EventHandler<IConsoleMessage> _consoleHandler;
    private readonly EventHandler<string> _pageErrorHandler;
    private bool _disposed;

    public DocumentEditorConsoleCapture(IPage page)
    {
        _page = page;
        _consoleHandler = (_, message) =>
        {
            if (message.Type is "error" or "warning")
            {
                Entries.Add($"console:{message.Type}: {message.Text}");
            }
        };
        _pageErrorHandler = (_, error) => Entries.Add($"pageerror: {error}");
        _page.Console += _consoleHandler;
        _page.PageError += _pageErrorHandler;
    }

    public List<string> Entries { get; } = [];

    public IReadOnlyList<string> Errors => Entries
        .Where(entry => entry.StartsWith("console:error:", StringComparison.Ordinal) || entry.StartsWith("pageerror:", StringComparison.Ordinal))
        .ToArray();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _page.Console -= _consoleHandler;
        _page.PageError -= _pageErrorHandler;
        _disposed = true;
    }
}
