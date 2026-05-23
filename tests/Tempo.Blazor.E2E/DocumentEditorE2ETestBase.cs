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
    private readonly Dictionary<IPage, DocumentEditorConsoleCapture> _mandatoryConsoleCaptures = [];

    /// <summary>Resets mutable demo data before each document editor runtime test.</summary>
    [TestInitialize]
    public Task ResetDocumentEditorDemoAsync()
        => DocumentEditorE2EReset.ResetAsync();

    /// <summary>Opens the normal document editor demo route and waits until the WYSIWYG surface is ready.</summary>
    protected async Task<IPage> OpenDocumentEditorAsync(int width = 1280, int height = 720)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        StartMandatoryDocumentEditorConsoleCapture(page);
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}/document-editor", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);
        return page;
    }

    /// <summary>Opens the deterministic 2026-05-23 Google Docs engine recovery document.</summary>
    protected async Task<IPage> OpenRecoveryDocumentAsync(int width = 1280, int height = 720)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        StartMandatoryDocumentEditorConsoleCapture(page);
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}/document-editor?tmDocumentEditorEngine=google-docs&recovery=2026-05-23", new PageGotoOptions
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

    /// <summary>Diagnostic read-only helper: reads visible document body text from non-virtual pages.</summary>
    protected static Task<string> ReadEditorPlainTextAsync(IPage page)
    {
        return page.EvaluateAsync<string>(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body'))
                .map(body => body.innerText || body.textContent || '')
                .join('\n')
            """);
    }

    /// <summary>Diagnostic read-only helper: reads current toolbar formatting state from controls and the JS debug bridge.</summary>
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

    /// <summary>Captures a named recovery screenshot and attaches it to the test result.</summary>
    protected async Task<string> CaptureEditorScreenshotAsync(IPage page, string name)
    {
        await page.WaitForTimeoutAsync(150);
        return await CaptureDocumentEditorPageScreenshotAsync(page, $"document_editor_recovery_{name}");
    }

    /// <summary>Starts collecting console and page errors for strict document editor scenarios.</summary>
    protected static DocumentEditorConsoleCapture BeginDocumentEditorConsoleCapture(IPage page)
        => new(page);

    /// <summary>Diagnostic read-only helper: evaluates browser state without applying user-visible editor commands.</summary>
    protected static Task<T> ReadDocumentEditorDiagnosticAsync<T>(IPage page, string expression, object? arg = null)
        => page.EvaluateAsync<T>(expression, arg);

    /// <summary>Guards recovery tests against masking human-facing behavior through internal command APIs.</summary>
    protected static void AssertRecoveryActionUsesHumanInput(string actionName, bool usesInternalApi)
    {
        if (usesInternalApi)
        {
            throw new AssertFailedException($"Recovery action '{actionName}' must use Playwright mouse/keyboard/locator APIs, not internal editor command APIs.");
        }
    }

    /// <summary>Returns the mandatory console/runtime capture registered before page navigation.</summary>
    protected DocumentEditorConsoleCapture GetMandatoryDocumentEditorConsoleCapture(IPage page)
        => _mandatoryConsoleCaptures.TryGetValue(page, out var capture)
            ? capture
            : throw new InvalidOperationException("No mandatory document editor console capture was registered for this page.");

    /// <summary>Fails the test when strict document editor console/runtime errors were captured.</summary>
    protected async Task AssertNoDocumentEditorConsoleErrorsAsync(IPage page, DocumentEditorConsoleCapture console, string behavior)
    {
        var fatal = console.FatalErrors;
        if (fatal.Count == 0)
        {
            return;
        }

        var screenshot = await CaptureEditorScreenshotAsync(page, $"{behavior}_console_error");
        throw new AssertFailedException($"{behavior} emitted document editor console/runtime errors: {string.Join(" | ", fatal)}. Screenshot: {screenshot}.");
    }

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

    /// <summary>Clicks a logical text offset in a visible document block using real mouse coordinates.</summary>
    protected static async Task<DocumentEditorPointProbe> ClickDocumentEditorBlockOffsetAsync(
        IPage page,
        string blockId,
        int offset,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
    {
        var target = await page.EvaluateAsync<DocumentEditorPointProbe>(
            """
            ({ hostSelector, blockId, offset }) => {
                const host = document.querySelector(hostSelector);
                const block = host?.querySelector(`[data-block-id="${cssEscape(blockId)}"], [data-render-block-id="${cssEscape(blockId)}"]`);
                if (!block) throw new Error(`Could not find visible block '${blockId}'.`);
                block.scrollIntoView({ block: 'center', inline: 'nearest' });
                const textNode = firstTextNode(block);
                const requested = Math.max(0, Number(offset) || 0);
                if (!textNode) {
                    const rect = block.getBoundingClientRect();
                    return { x: rect.left + 8, y: rect.top + rect.height / 2 };
                }
                const length = textNode.nodeValue.length;
                const range = document.createRange();
                const caretOffset = Math.max(0, Math.min(length, requested));
                if (caretOffset >= length && length > 0) {
                    range.setStart(textNode, length - 1);
                    range.setEnd(textNode, length);
                    const rect = Array.from(range.getClientRects()).pop() || block.getBoundingClientRect();
                    return { x: rect.right - 1, y: rect.top + rect.height / 2 };
                }
                range.setStart(textNode, caretOffset);
                range.setEnd(textNode, Math.min(length, caretOffset + 1));
                const rect = Array.from(range.getClientRects())[0] || block.getBoundingClientRect();
                return { x: rect.left + 1, y: rect.top + rect.height / 2 };

                function firstTextNode(root) {
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            return node.nodeValue && node.nodeValue.length > 0
                                ? NodeFilter.FILTER_ACCEPT
                                : NodeFilter.FILTER_REJECT;
                        }
                    });
                    return walker.nextNode();
                }

                function cssEscape(value) {
                    return window.CSS?.escape ? window.CSS.escape(String(value)) : String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                }
            }
            """,
            new { hostSelector, blockId, offset });
        await page.Mouse.ClickAsync((float)target.X, (float)target.Y);
        await page.EvaluateAsync(
            """
            ({ hostSelector, blockId, offset }) => {
                const host = document.querySelector(hostSelector);
                const escaped = window.CSS?.escape ? window.CSS.escape(blockId) : String(blockId).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                const block = host?.querySelector(`[data-block-id="${escaped}"], [data-render-block-id="${escaped}"]`);
                if (!block) throw new Error(`Could not find visible block '${blockId}' after click.`);
                const body = block.closest('[contenteditable="true"]');
                if (!body) throw new Error(`Block '${blockId}' is not inside an editable document body.`);
                const requested = Math.max(0, Number(offset) || 0);
                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        return node.nodeValue !== null ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                    }
                });
                let current = 0;
                let node = null;
                let localOffset = 0;
                while (walker.nextNode()) {
                    const candidate = walker.currentNode;
                    const length = candidate.nodeValue.length;
                    if (requested <= current + length) {
                        node = candidate;
                        localOffset = Math.max(0, Math.min(length, requested - current));
                        break;
                    }
                    current += length;
                }
                if (!node) {
                    const lastWalker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                    while (lastWalker.nextNode()) {
                        node = lastWalker.currentNode;
                    }
                    localOffset = node ? node.nodeValue.length : 0;
                }

                body.focus();
                const range = document.createRange();
                if (node) {
                    range.setStart(node, localOffset);
                } else {
                    range.selectNodeContents(block);
                    range.collapse(false);
                }
                range.collapse(true);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """,
            new { hostSelector, blockId, offset });
        return target;
    }

    /// <summary>Diagnostic read-only helper: reads visible text from a rendered document block.</summary>
    protected static Task<string> ReadDocumentEditorBlockTextAsync(
        IPage page,
        string blockId,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
        => page.EvaluateAsync<string>(
            """
            ({ hostSelector, blockId }) => {
                const host = document.querySelector(hostSelector);
                const escaped = window.CSS?.escape ? window.CSS.escape(blockId) : String(blockId).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                const block = host?.querySelector(`[data-block-id="${escaped}"], [data-render-block-id="${escaped}"]`);
                return block?.innerText || block?.textContent || '';
            }
            """,
            new { hostSelector, blockId });

    /// <summary>Diagnostic read-only helper: reads the current native caret rectangle.</summary>
    protected static Task<DocumentEditorRectProbe> ReadDocumentEditorCaretRectAsync(IPage page)
        => page.EvaluateAsync<DocumentEditorRectProbe>(
            """
            () => {
                const selection = window.getSelection();
                if (!selection || selection.rangeCount === 0) return { x: 0, y: 0, width: 0, height: 0 };
                const range = selection.getRangeAt(0).cloneRange();
                range.collapse(false);
                let rect = range.getBoundingClientRect();
                if (!rect || rect.height <= 0) {
                    const marker = document.createElement('span');
                    marker.textContent = '\u200b';
                    range.insertNode(marker);
                    rect = marker.getBoundingClientRect();
                    marker.remove();
                }
                return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
            }
            """);

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

    /// <summary>Diagnostic read-only helper: captures one strict visual frame probe.</summary>
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

    /// <summary>Diagnostic read-only helper: captures visible DOM geometry for recovery tests that need human-visible evidence.</summary>
    protected static Task<DocumentEditorGeometryProbe> CaptureEditorGeometryAsync(IPage page)
        => page.EvaluateAsync<DocumentEditorGeometryProbe>(
            """
            () => {
                const visibleRects = selector => Array.from(document.querySelectorAll(selector))
                    .filter(isVisible)
                    .map(node => toRect(node.getBoundingClientRect()));
                const firstRect = selector => visibleRects(selector)[0] || null;
                return {
                    pageRect: firstRect('[data-testid="document-wysiwyg-engine-document"] .tm-render-page, [data-testid="document-wysiwyg-engine-document"] .tm-wysiwyg-page, .tm-render-page, .tm-wysiwyg-page'),
                    headerRect: firstRect('[data-testid="document-page-header"], .tm-render-header-region, [data-render-frame="header-content"]'),
                    footerRect: firstRect('[data-testid="document-page-footer"], .tm-render-footer-region, [data-render-frame="footer-content"]'),
                    bodyRect: firstRect('[data-render-frame="body"], .tm-render-body-frame, .tm-wysiwyg-page__body'),
                    commentMarkerRects: visibleRects('[data-testid="document-comment-marker"], .tm-render-comment-marker, .tm-wysiwyg-marker--comment'),
                    revisionMarkerRects: visibleRects('[data-testid="document-revision-marker"], .tm-render-revision-marker, .tm-wysiwyg-marker--revision-insert, .tm-wysiwyg-marker--revision-delete, .tm-wysiwyg-marker--revision-format'),
                    floatingToolbarRect: firstRect('[data-testid="document-floating-toolbar"], [data-human-testid="document-floating-toolbar"], [data-testid="document-wysiwyg-mini-toolbar"], .tm-document-editor__mini-toolbar, .tm-document-editor__floating-root'),
                    imageToolbarRect: firstRect('[data-testid="document-image-toolbar"], [data-human-testid="document-image-toolbar"], [data-testid="document-wysiwyg-image-toolbar"], .tm-document-editor__image-toolbar, .tm-wysiwyg-image-toolbar'),
                    sidePanelRect: firstRect('[data-testid="document-image-properties-panel"], [data-testid="document-side-panel"], .tm-document-side-panel'),
                    visibleText: document.querySelector('[data-testid="document-wysiwyg-host"]')?.innerText || ''
                };

                function isVisible(node) {
                    if (!node) return false;
                    const rect = node.getBoundingClientRect();
                    const style = window.getComputedStyle(node);
                    return rect.width > 0.5
                        && rect.height > 0.5
                        && style.display !== 'none'
                        && style.visibility !== 'hidden'
                        && Number(style.opacity || 1) > 0.01;
                }

                function toRect(rect) {
                    return {
                        x: Number(rect.x || rect.left || 0),
                        y: Number(rect.y || rect.top || 0),
                        width: Number(rect.width || 0),
                        height: Number(rect.height || 0)
                    };
                }
            }
            """);

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

    /// <summary>Asserts that a user-visible element is visible and has a measurable rectangle.</summary>
    protected static async Task<DocumentEditorRectProbe> ExpectVisibleAndNonEmptyAsync(ILocator locator, string name)
    {
        await Assertions.Expect(locator).ToBeVisibleAsync();
        var rect = await GetLocatorRectAsync(locator, name);
        if (rect.Width <= 0.5 || rect.Height <= 0.5)
        {
            throw new AssertFailedException($"{name} should be visible and non-empty, but its rect was {FormatRect(rect)}.");
        }

        return rect;
    }

    /// <summary>Asserts that a user-facing element is fully inside the rendered page bounds.</summary>
    protected static async Task ExpectRectInsidePageAsync(ILocator locator, ILocator pageLocator)
    {
        var rect = await GetLocatorRectAsync(locator, "target");
        var pageRect = await GetLocatorRectAsync(pageLocator, "page");
        if (!RectInside(rect, pageRect, 1.5))
        {
            throw new AssertFailedException($"Expected target rect {FormatRect(rect)} to be inside page rect {FormatRect(pageRect)}.");
        }
    }

    /// <summary>Asserts that two user-visible elements do not overlap beyond a tolerance.</summary>
    protected static async Task ExpectNoOverlapAsync(ILocator locatorA, ILocator locatorB, double tolerancePx = 1.5)
    {
        var a = await GetLocatorRectAsync(locatorA, "first element");
        var b = await GetLocatorRectAsync(locatorB, "second element");
        if (RectsOverlap(a, b, tolerancePx))
        {
            throw new AssertFailedException($"Expected no overlap, but rects overlap: A={FormatRect(a)}, B={FormatRect(b)}, tolerance={tolerancePx:0.##}.");
        }
    }

    /// <summary>Asserts that a comment or revision marker intersects the visual text range for expected text.</summary>
    protected static async Task ExpectMarkerIntersectsTextRangeAsync(IPage page, ILocator marker, string expectedText)
    {
        var markerRect = await GetLocatorRectAsync(marker, "marker");
        var result = await page.EvaluateAsync<MarkerTextIntersectionProbe>(
            """
            ({ markerRect, expectedText }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                if (!host) return { foundText: false, intersects: false, textRectCount: 0 };
                const walker = document.createTreeWalker(host, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        if (!node.nodeValue || !node.nodeValue.includes(expectedText)) return NodeFilter.FILTER_REJECT;
                        const parent = node.parentElement;
                        if (!parent || parent.closest('[role="menu"], [data-testid="document-side-panel"], .tm-document-editor__floating-root')) {
                            return NodeFilter.FILTER_REJECT;
                        }
                        return NodeFilter.FILTER_ACCEPT;
                    }
                });
                const textRects = [];
                for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                    const start = node.nodeValue.indexOf(expectedText);
                    if (start < 0) continue;
                    const range = document.createRange();
                    range.setStart(node, start);
                    range.setEnd(node, start + expectedText.length);
                    for (const rect of Array.from(range.getClientRects())) {
                        if (rect.width > 0.5 && rect.height > 0.5) {
                            textRects.push({ x: rect.x, y: rect.y, width: rect.width, height: rect.height });
                        }
                    }
                }
                return {
                    foundText: textRects.length > 0,
                    intersects: textRects.some(rect => intersects(rect, markerRect, 1.5)),
                    textRectCount: textRects.length
                };

                function intersects(a, b, tolerance) {
                    const x = Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x));
                    const y = Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
                    return x * y > tolerance;
                }
            }
            """,
            new { markerRect, expectedText });
        if (!result.FoundText)
        {
            throw new AssertFailedException($"Expected visible text range '{expectedText}' was not found.");
        }

        if (!result.Intersects)
        {
            throw new AssertFailedException($"Marker rect {FormatRect(markerRect)} does not intersect text range '{expectedText}' ({result.TextRectCount} rects).");
        }
    }

    /// <summary>Asserts that a floating toolbar is positioned near the current visual selection.</summary>
    protected static async Task ExpectToolbarNearSelectionAsync(ILocator toolbar, DocumentEditorRectProbe selectionRect)
    {
        var toolbarRect = await GetLocatorRectAsync(toolbar, "toolbar");
        var toolbarCenterX = toolbarRect.X + toolbarRect.Width / 2;
        var selectionCenterX = selectionRect.X + selectionRect.Width / 2;
        var horizontalDistance = Math.Abs(toolbarCenterX - selectionCenterX);
        var verticalGap = Math.Min(
            Math.Abs(toolbarRect.Y + toolbarRect.Height - selectionRect.Y),
            Math.Abs(selectionRect.Y + selectionRect.Height - toolbarRect.Y));
        if (horizontalDistance > Math.Max(160, selectionRect.Width) || verticalGap > 120)
        {
            throw new AssertFailedException($"Toolbar rect {FormatRect(toolbarRect)} should be near selection rect {FormatRect(selectionRect)}.");
        }
    }

    /// <summary>Asserts that a properties panel is visibly bound to the active object.</summary>
    protected static async Task ExpectPanelShowsActiveObjectAsync(ILocator panel, string objectId)
    {
        await Assertions.Expect(panel).ToBeVisibleAsync();
        var actual = await panel.EvaluateAsync<string>(
            """
            (node) => node.getAttribute('data-active-object-id')
                || node.getAttribute('data-active-image-block-id')
                || node.getAttribute('data-object-id')
                || node.textContent
                || ''
            """);
        if (!actual.Contains(objectId, StringComparison.Ordinal))
        {
            throw new AssertFailedException($"Expected properties panel to show active object '{objectId}', actual binding/text was '{actual}'.");
        }
    }

    /// <summary>Measures one human keystroke through the browser-side document editor latency probe.</summary>
    protected static async Task<DocumentEditorKeystrokeLatencyProbe> MeasureKeystrokeLatencyAsync(
        IPage page,
        string key,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
    {
        await page.EvaluateAsync(
            """
            ({ hostSelector }) => {
                if (!window.tmDocumentEditorTestProbe) throw new Error('tmDocumentEditorTestProbe is not available.');
                window.tmDocumentEditorTestProbe.start(hostSelector);
            }
            """,
            new { hostSelector });
        await page.Keyboard.PressAsync(key);
        await page.WaitForTimeoutAsync(120);
        return await page.EvaluateAsync<DocumentEditorKeystrokeLatencyProbe>(
            "() => window.tmDocumentEditorTestProbe.snapshot()");
    }

    /// <summary>Measures browser mutation/render batching while a key is held through Playwright keyboard input.</summary>
    protected static async Task<DocumentEditorKeyHoldBatchProbe> HoldKeyAndMeasureBatchesAsync(
        IPage page,
        string key,
        int holdMilliseconds = 500,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
    {
        await page.EvaluateAsync(
            """
            ({ hostSelector }) => {
                if (!window.tmDocumentEditorTestProbe) throw new Error('tmDocumentEditorTestProbe is not available.');
                window.tmDocumentEditorTestProbe.start(hostSelector);
            }
            """,
            new { hostSelector });
        var stopAt = DateTimeOffset.UtcNow.AddMilliseconds(Math.Max(1, holdMilliseconds));
        do
        {
            await page.Keyboard.PressAsync(key);
            await page.WaitForTimeoutAsync(30);
        }
        while (DateTimeOffset.UtcNow < stopAt);
        await page.WaitForTimeoutAsync(120);
        var probe = await page.EvaluateAsync<DocumentEditorKeyHoldBatchProbe>(
            "() => window.tmDocumentEditorTestProbe.snapshot()");
        probe.HoldMilliseconds = holdMilliseconds;
        return probe;
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

    private static async Task<DocumentEditorRectProbe> GetLocatorRectAsync(ILocator locator, string name)
    {
        var box = await locator.BoundingBoxAsync();
        if (box is null)
        {
            throw new AssertFailedException($"Could not measure {name}; no bounding box was returned.");
        }

        return new()
        {
            X = box.X,
            Y = box.Y,
            Width = box.Width,
            Height = box.Height
        };
    }

    private static bool RectInside(DocumentEditorRectProbe inner, DocumentEditorRectProbe outer, double tolerance)
        => inner.X >= outer.X - tolerance
            && inner.Y >= outer.Y - tolerance
            && inner.X + Math.Max(1, inner.Width) <= outer.X + outer.Width + tolerance
            && inner.Y + Math.Max(1, inner.Height) <= outer.Y + outer.Height + tolerance;

    private static bool RectsOverlap(DocumentEditorRectProbe a, DocumentEditorRectProbe b, double tolerance)
    {
        var x = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X));
        var y = Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
        return x * y > tolerance;
    }

    private static string FormatRect(DocumentEditorRectProbe rect)
        => $"x={rect.X:0.##}, y={rect.Y:0.##}, w={rect.Width:0.##}, h={rect.Height:0.##}";

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

    private void StartMandatoryDocumentEditorConsoleCapture(IPage page)
    {
        if (_mandatoryConsoleCaptures.ContainsKey(page))
        {
            return;
        }

        _mandatoryConsoleCaptures[page] = new DocumentEditorConsoleCapture(page);
    }

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

/// <summary>Visible geometry captured from the document editor DOM for regression recovery tests.</summary>
public sealed class DocumentEditorGeometryProbe
{
    [JsonPropertyName("pageRect")] public DocumentEditorRectProbe? PageRect { get; set; }
    [JsonPropertyName("headerRect")] public DocumentEditorRectProbe? HeaderRect { get; set; }
    [JsonPropertyName("footerRect")] public DocumentEditorRectProbe? FooterRect { get; set; }
    [JsonPropertyName("bodyRect")] public DocumentEditorRectProbe? BodyRect { get; set; }
    [JsonPropertyName("commentMarkerRects")] public DocumentEditorRectProbe[] CommentMarkerRects { get; set; } = [];
    [JsonPropertyName("revisionMarkerRects")] public DocumentEditorRectProbe[] RevisionMarkerRects { get; set; } = [];
    [JsonPropertyName("floatingToolbarRect")] public DocumentEditorRectProbe? FloatingToolbarRect { get; set; }
    [JsonPropertyName("imageToolbarRect")] public DocumentEditorRectProbe? ImageToolbarRect { get; set; }
    [JsonPropertyName("sidePanelRect")] public DocumentEditorRectProbe? SidePanelRect { get; set; }
    [JsonPropertyName("visibleText")] public string VisibleText { get; set; } = string.Empty;
}

/// <summary>Browser-side marker/text intersection result for recovery visual assertions.</summary>
public sealed class MarkerTextIntersectionProbe
{
    [JsonPropertyName("foundText")] public bool FoundText { get; set; }
    [JsonPropertyName("intersects")] public bool Intersects { get; set; }
    [JsonPropertyName("textRectCount")] public int TextRectCount { get; set; }
}

/// <summary>Browser-side latency probe for one human keystroke.</summary>
public class DocumentEditorKeystrokeLatencyProbe
{
    [JsonPropertyName("startedAt")] public double StartedAt { get; set; }
    [JsonPropertyName("keyDownAt")] public double? KeyDownAt { get; set; }
    [JsonPropertyName("beforeInputAt")] public double? BeforeInputAt { get; set; }
    [JsonPropertyName("firstDomMutationAt")] public double? FirstDomMutationAt { get; set; }
    [JsonPropertyName("visibleTextChangedAt")] public double? VisibleTextChangedAt { get; set; }
    [JsonPropertyName("beforeInputLatencyMs")] public double? BeforeInputLatencyMs { get; set; }
    [JsonPropertyName("domMutationLatencyMs")] public double? DomMutationLatencyMs { get; set; }
    [JsonPropertyName("visibleTextChangeLatencyMs")] public double? VisibleTextChangeLatencyMs { get; set; }
    [JsonPropertyName("fullRenderCount")] public int FullRenderCount { get; set; }
    [JsonPropertyName("partialRenderCount")] public int PartialRenderCount { get; set; }
    [JsonPropertyName("blazorCallbackCount")] public int BlazorCallbackCount { get; set; }
    [JsonPropertyName("keydownCount")] public int KeydownCount { get; set; }
    [JsonPropertyName("beforeInputCount")] public int BeforeInputCount { get; set; }
    [JsonPropertyName("mutationBatchCount")] public int MutationBatchCount { get; set; }
    [JsonPropertyName("mutationRecordCount")] public int MutationRecordCount { get; set; }
    [JsonPropertyName("largestBatchSize")] public int LargestBatchSize { get; set; }
    [JsonPropertyName("visibleTextLength")] public int VisibleTextLength { get; set; }
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
}

/// <summary>Browser-side batching probe for a held key.</summary>
public sealed class DocumentEditorKeyHoldBatchProbe : DocumentEditorKeystrokeLatencyProbe
{
    [JsonPropertyName("holdMilliseconds")] public int HoldMilliseconds { get; set; }
}

/// <summary>Paths attached to a strict E2E failure.</summary>
public sealed record DocumentEditorStrictFailureArtifact(string ScreenshotPath, string JsonArtifactPath);

/// <summary>Collects browser console and page errors during strict document editor tests.</summary>
public sealed class DocumentEditorConsoleCapture : IDisposable
{
    private readonly IPage _page;
    private readonly EventHandler<IConsoleMessage> _consoleHandler;
    private readonly EventHandler<string> _pageErrorHandler;
    private readonly EventHandler<IRequest> _requestFailedHandler;
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
        _requestFailedHandler = (_, request) =>
        {
            var failure = request.Failure ?? string.Empty;
            if (IsDocumentEditorRequestFailure(request.Url, failure))
            {
                Entries.Add($"requestfailed: {request.Method} {request.Url}: {failure}");
            }
        };
        _page.Console += _consoleHandler;
        _page.PageError += _pageErrorHandler;
        _page.RequestFailed += _requestFailedHandler;
    }

    public List<string> Entries { get; } = [];

    public IReadOnlyList<string> Errors => Entries
        .Where(entry => entry.StartsWith("console:error:", StringComparison.Ordinal) || entry.StartsWith("pageerror:", StringComparison.Ordinal))
        .ToArray();

    public IReadOnlyList<string> FatalErrors => Entries
        .Where(IsFatalEntry)
        .ToArray();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _page.Console -= _consoleHandler;
        _page.PageError -= _pageErrorHandler;
        _page.RequestFailed -= _requestFailedHandler;
        _disposed = true;
    }

    private static bool IsFatalEntry(string entry)
    {
        if (IsWhitelisted(entry))
        {
            return false;
        }

        if (entry.StartsWith("console:error:", StringComparison.Ordinal)
            || entry.StartsWith("pageerror:", StringComparison.Ordinal)
            || entry.StartsWith("requestfailed:", StringComparison.Ordinal))
        {
            return true;
        }

        var fatalFragments = new[]
        {
            "crit: Microsoft.AspNetCore.Components",
            "Unhandled exception rendering component",
            "Cannot read properties of null",
            ".NET runtime already exited",
            "invokeMethodAsync failed"
        };

        return fatalFragments.Any(fragment => entry.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWhitelisted(string entry)
        => entry.Contains("favicon", StringComparison.OrdinalIgnoreCase);

    private static bool IsDocumentEditorRequestFailure(string url, string failure)
    {
        if (url.Contains("favicon", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return url.Contains("/document-editor", StringComparison.OrdinalIgnoreCase)
            || url.Contains("_framework", StringComparison.OrdinalIgnoreCase)
            || failure.Contains("net::ERR", StringComparison.OrdinalIgnoreCase);
    }
}
