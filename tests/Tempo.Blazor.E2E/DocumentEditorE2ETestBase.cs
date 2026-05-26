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
    protected const string DocumentEditorHostSelector = "[data-testid='document-wysiwyg-host']";

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

    /// <summary>Opens the deterministic 2026-05-24 ONLYOFFICE parity baseline document.</summary>
    protected async Task<IPage> OpenOnlyOfficeParityDocumentAsync(int width = 1280, int height = 720)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        StartMandatoryDocumentEditorConsoleCapture(page);
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}/document-editor?documentId=onlyoffice-parity-2026-05-24", new PageGotoOptions
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

    /// <summary>Selects visible text in one block with real mouse movement and returns a strict selection snapshot.</summary>
    protected static async Task<DocumentEditorSelectionSnapshot> SelectTextByMouseAsync(
        IPage page,
        string blockId,
        string text,
        string hostSelector = DocumentEditorHostSelector)
    {
        var target = await ReadTextSelectionMouseTargetAsync(page, blockId, text, hostSelector);
        await page.Mouse.MoveAsync((float)target.StartX, (float)target.StartY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)target.EndX, (float)target.EndY, new() { Steps = 12 });
        await page.Mouse.UpAsync();
        return await WaitForTextSelectionAsync(page, blockId, text, target, hostSelector);
    }

    /// <summary>Selects the first visible occurrence of text with real mouse movement.</summary>
    protected static async Task<DocumentEditorSelectionSnapshot> SelectFirstTextByMouseAsync(
        IPage page,
        string text,
        string hostSelector = DocumentEditorHostSelector)
    {
        var target = await ReadTextSelectionMouseTargetAsync(page, null, text, hostSelector);
        await page.Mouse.MoveAsync((float)target.StartX, (float)target.StartY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)target.EndX, (float)target.EndY, new() { Steps = 12 });
        await page.Mouse.UpAsync();
        return await WaitForTextSelectionAsync(page, target.BlockId, text, target, hostSelector);
    }

    /// <summary>Selects a logical text range in a visible block with real mouse movement.</summary>
    protected static async Task<DocumentEditorSelectionSnapshot> SelectTextByMouseAsync(
        IPage page,
        string blockId,
        int startOffset,
        int endOffset,
        string hostSelector = DocumentEditorHostSelector)
    {
        var target = await ReadTextSelectionMouseTargetAsync(page, blockId, startOffset, endOffset, hostSelector);
        await page.Mouse.MoveAsync((float)target.StartX, (float)target.StartY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)target.EndX, (float)target.EndY, new() { Steps = 12 });
        await page.Mouse.UpAsync();
        return await WaitForTextSelectionAsync(page, blockId, target.ExpectedText, target, hostSelector);
    }

    /// <summary>Selects visible text in one block using a collapsed caret plus Shift+ArrowRight.</summary>
    protected static async Task<DocumentEditorSelectionSnapshot> SelectTextByKeyboardAsync(
        IPage page,
        string blockId,
        string text,
        string hostSelector = DocumentEditorHostSelector)
    {
        var target = await ReadTextSelectionMouseTargetAsync(page, blockId, text, hostSelector);
        await ClickDocumentEditorBlockOffsetAsync(page, blockId, target.StartOffset, hostSelector);
        await page.Keyboard.DownAsync("Shift");
        for (var index = target.StartOffset; index < target.EndOffset; index++)
        {
            await page.Keyboard.PressAsync("ArrowRight");
        }

        await page.Keyboard.UpAsync("Shift");
        return await WaitForTextSelectionAsync(page, blockId, text, target, hostSelector);
    }

    /// <summary>Selects text that must span at least two rendered text nodes or inline runs.</summary>
    protected static async Task<DocumentEditorSelectionSnapshot> SelectTextAcrossInlineRunsByMouseAsync(
        IPage page,
        string blockId,
        string text,
        string hostSelector = DocumentEditorHostSelector)
    {
        var target = await ReadTextSelectionMouseTargetAsync(page, blockId, text, hostSelector);
        if (target.TextNodeCount < 2)
        {
            throw new AssertFailedException($"Expected '{text}' in block '{blockId}' to span at least two text nodes/inline runs, but target node count was {target.TextNodeCount}. Target: {target.Debug}");
        }

        await page.Mouse.MoveAsync((float)target.StartX, (float)target.StartY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)target.EndX, (float)target.EndY, new() { Steps = 12 });
        await page.Mouse.UpAsync();
        return await WaitForTextSelectionAsync(page, blockId, text, target, hostSelector);
    }

    /// <summary>Selects text and verifies that the selected range exposes mixed computed formatting.</summary>
    protected static async Task<DocumentEditorSelectionSnapshot> SelectMixedFormattingTextByMouseAsync(
        IPage page,
        string blockId,
        string text,
        string hostSelector = DocumentEditorHostSelector)
    {
        var selection = await SelectTextByMouseAsync(page, blockId, text, hostSelector);
        var styles = await ReadTextRunComputedStylesAsync(page, blockId, text, hostSelector);
        if (!styles.HasMixedFormatting)
        {
            throw new AssertFailedException($"Expected selected text '{text}' in block '{blockId}' to have mixed formatting. Styles: {styles.Debug}");
        }

        return selection;
    }

    /// <summary>Reads the current native/runtime document selection without changing the document.</summary>
    protected static Task<DocumentEditorSelectionSnapshot> ReadDocumentEditorSelectionSnapshotAsync(
        IPage page,
        string hostSelector = DocumentEditorHostSelector)
        => page.EvaluateAsync<DocumentEditorSelectionSnapshot>(
            """
            (hostSelector) => {
                const host = document.querySelector(hostSelector);
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const selection = window.getSelection();
                const runtimeSelection = getRuntimeSelection(instanceId);
                const runtimeToken = findSelectionToken(runtimeSelection) || findSelectionToken(getRuntimeDebug(instanceId));
                const empty = {
                    targetBlockId: '',
                    expectedText: '',
                    selectedText: selection?.toString() || '',
                    isCollapsed: true,
                    anchorBlockId: '',
                    anchorInlineId: '',
                    anchorBlockOffset: -1,
                    focusBlockId: '',
                    focusInlineId: '',
                    focusBlockOffset: -1,
                    startBlockId: '',
                    startInlineId: '',
                    startBlockOffset: -1,
                    endBlockId: '',
                    endInlineId: '',
                    endBlockOffset: -1,
                    rect: zeroRect(),
                    runtimeSelectionJson: safeJson(runtimeSelection),
                    runtimeSelectionToken: runtimeToken,
                    hasRuntimeSelection: !!runtimeSelection,
                    hasRuntimeSelectionToken: !!runtimeToken,
                    debug: ''
                };
                if (!selection || selection.rangeCount === 0) {
                    empty.debug = safeJson({ reason: 'no native selection range', runtimeSelection });
                    return empty;
                }

                const range = selection.getRangeAt(0).cloneRange();
                const anchor = positionOf(selection.anchorNode, selection.anchorOffset);
                const focus = positionOf(selection.focusNode, selection.focusOffset);
                const start = positionOf(range.startContainer, range.startOffset);
                const end = positionOf(range.endContainer, range.endOffset);
                const rect = unionRects(Array.from(range.getClientRects()).filter(rect => rect.width > 0.5 && rect.height > 0.5));
                return {
                    targetBlockId: '',
                    expectedText: '',
                    selectedText: selection.toString() || '',
                    isCollapsed: selection.isCollapsed,
                    anchorBlockId: anchor.blockId,
                    anchorInlineId: anchor.inlineId,
                    anchorBlockOffset: anchor.blockOffset,
                    focusBlockId: focus.blockId,
                    focusInlineId: focus.inlineId,
                    focusBlockOffset: focus.blockOffset,
                    startBlockId: start.blockId,
                    startInlineId: start.inlineId,
                    startBlockOffset: start.blockOffset,
                    endBlockId: end.blockId,
                    endInlineId: end.inlineId,
                    endBlockOffset: end.blockOffset,
                    rect,
                    runtimeSelectionJson: safeJson(runtimeSelection),
                    runtimeSelectionToken: runtimeToken,
                    hasRuntimeSelection: !!runtimeSelection,
                    hasRuntimeSelectionToken: !!runtimeToken,
                    debug: safeJson({ selectedText: selection.toString(), anchor, focus, start, end, rect, runtimeSelection, runtimeToken })
                };

                function positionOf(node, offset) {
                    const element = elementOf(node);
                    const block = element?.closest?.('[data-block-id], [data-render-block-id]');
                    const inline = element?.closest?.('[data-inline-id]');
                    let blockOffset = -1;
                    if (block && node) {
                        try {
                            const pre = document.createRange();
                            pre.selectNodeContents(block);
                            pre.setEnd(node, Math.max(0, Number(offset) || 0));
                            blockOffset = pre.toString().length;
                        } catch {
                            blockOffset = -1;
                        }
                    }

                    return {
                        blockId: block?.getAttribute('data-block-id') || block?.getAttribute('data-render-block-id') || '',
                        inlineId: inline?.getAttribute('data-inline-id') || '',
                        blockOffset
                    };
                }

                function elementOf(node) {
                    if (!node) return null;
                    if (node.nodeType === Node.ELEMENT_NODE) return node;
                    if (node.parentElement) return node.parentElement;
                    return node.parentNode?.nodeType === Node.ELEMENT_NODE ? node.parentNode : null;
                }

                function getRuntimeSelection(id) {
                    try {
                        const runtime = window.tmDocumentEditorRuntime || window.tmDocumentEditorEngine;
                        const snapshot = runtime?.getSelectionSnapshot?.(id)
                            || runtime?.getCurrentSelection?.(id)
                            || runtime?.selection?.getSelectionSnapshot?.(id)
                            || null;
                        if (snapshot) return snapshot;
                        const state = window.tmDocumentEditorDebug?.getRuntimeState?.(id) || null;
                        return state?.currentSelection || state?.CurrentSelection || state?.lastSelection || state?.LastSelection || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function getRuntimeDebug(id) {
                    try {
                        return window.tmDocumentEditorRuntime?.getDebugSnapshot?.(id)
                            || window.tmDocumentEditorEngine?.getDebugSnapshot?.(id)
                            || window.tmDocumentEditorDebug?.getRuntimeState?.(id)
                            || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function findSelectionToken(value, depth = 0) {
                    if (!value || typeof value !== 'object' || depth > 3) return '';
                    for (const key of ['selectionToken', 'SelectionToken', 'stableSelectionToken', 'StableSelectionToken', 'token', 'Token']) {
                        const candidate = value[key];
                        if (typeof candidate === 'string' && candidate.trim()) return candidate;
                    }
                    for (const key of ['selection', 'Selection', 'currentSelection', 'CurrentSelection', 'lastSelection', 'LastSelection']) {
                        const nested = findSelectionToken(value[key], depth + 1);
                        if (nested) return nested;
                    }
                    return '';
                }

                function unionRects(rects) {
                    if (!rects.length) return zeroRect();
                    const left = Math.min(...rects.map(rect => rect.left));
                    const top = Math.min(...rects.map(rect => rect.top));
                    const right = Math.max(...rects.map(rect => rect.right));
                    const bottom = Math.max(...rects.map(rect => rect.bottom));
                    return { x: left, y: top, width: right - left, height: bottom - top };
                }

                function zeroRect() {
                    return { x: 0, y: 0, width: 0, height: 0 };
                }

                function safeJson(value) {
                    try { return JSON.stringify(value ?? null); }
                    catch (error) { return JSON.stringify({ error: String(error) }); }
                }
            }
            """,
            hostSelector);

    /// <summary>Asserts that the current range selection still represents the captured snapshot.</summary>
    protected static async Task AssertSelectionStillEqualsAsync(
        IPage page,
        DocumentEditorSelectionSnapshot expected,
        string hostSelector = DocumentEditorHostSelector)
    {
        var actual = await ReadDocumentEditorSelectionSnapshotAsync(page, hostSelector);
        if (actual.IsCollapsed
            || !actual.SelectedText.Contains(expected.SelectedText, StringComparison.Ordinal)
            || !string.Equals(actual.StartBlockId, expected.StartBlockId, StringComparison.Ordinal)
            || !string.Equals(actual.EndBlockId, expected.EndBlockId, StringComparison.Ordinal)
            || actual.StartBlockOffset != expected.StartBlockOffset
            || actual.EndBlockOffset != expected.EndBlockOffset)
        {
            throw new AssertFailedException($"Selection moved or collapsed. Expected: {expected.Debug}; Actual: {actual.Debug}");
        }
    }

    /// <summary>Asserts that the current selection is a collapsed caret at a logical block offset.</summary>
    protected static async Task AssertSelectionCollapsedAtAsync(
        IPage page,
        string blockId,
        int offset,
        string hostSelector = DocumentEditorHostSelector)
    {
        var actual = await ReadDocumentEditorSelectionSnapshotAsync(page, hostSelector);
        if (!actual.IsCollapsed
            || !string.Equals(actual.FocusBlockId, blockId, StringComparison.Ordinal)
            || Math.Abs(actual.FocusBlockOffset - offset) > 1)
        {
            throw new AssertFailedException($"Expected collapsed caret at block '{blockId}' offset {offset}, actual selection was {actual.Debug}.");
        }
    }

    /// <summary>Checks that a toolbar pointerdown does not destroy the captured text selection before command execution.</summary>
    protected static async Task<DocumentEditorToolbarActionResult> AssertSelectionDoesNotMoveDuringToolbarPointerDownAsync(
        IPage page,
        string testId,
        DocumentEditorSelectionSnapshot? expectedSelection = null,
        bool requireRuntimeSelectionToken = true,
        string hostSelector = DocumentEditorHostSelector)
        => await ClickToolbarElementWithPointerAsync(
            page,
            page.GetByTestId(testId),
            testId,
            "ribbon",
            expectedSelection,
            requireRuntimeSelectionToken,
            hostSelector,
            assertCommandStatePublished: false);

    /// <summary>Clicks a ribbon button command with real pointer events and strict selection diagnostics.</summary>
    protected static Task<DocumentEditorToolbarActionResult> ClickRibbonCommandAsync(
        IPage page,
        string testId,
        DocumentEditorSelectionSnapshot? expectedSelection = null,
        bool requireRuntimeSelectionToken = true,
        string hostSelector = DocumentEditorHostSelector)
        => ClickToolbarElementWithPointerAsync(
            page,
            page.GetByTestId(testId),
            testId,
            "ribbon",
            expectedSelection,
            requireRuntimeSelectionToken,
            hostSelector);

    /// <summary>Clicks a floating toolbar button command with real pointer events and strict selection diagnostics.</summary>
    protected static Task<DocumentEditorToolbarActionResult> ClickFloatingToolbarCommandAsync(
        IPage page,
        string testId,
        DocumentEditorSelectionSnapshot? expectedSelection = null,
        bool requireRuntimeSelectionToken = true,
        string hostSelector = DocumentEditorHostSelector)
        => ClickToolbarElementWithPointerAsync(
            page,
            page.GetByTestId(testId),
            testId,
            "floating",
            expectedSelection,
            requireRuntimeSelectionToken,
            hostSelector);

    /// <summary>Opens a ribbon native select with a real pointer click.</summary>
    protected static Task<DocumentEditorToolbarActionResult> OpenRibbonSelectAsync(
        IPage page,
        string testId,
        DocumentEditorSelectionSnapshot? expectedSelection = null,
        bool requireRuntimeSelectionToken = true,
        string hostSelector = DocumentEditorHostSelector)
        => ClickToolbarElementWithPointerAsync(
            page,
            page.GetByTestId(testId),
            testId,
            "ribbon",
            expectedSelection,
            requireRuntimeSelectionToken,
            hostSelector,
            assertCommandStatePublished: false);

    /// <summary>Chooses a ribbon select option while preserving the captured editor selection.</summary>
    protected static async Task<DocumentEditorToolbarActionResult> ChooseRibbonSelectOptionAsync(
        IPage page,
        string testId,
        string value,
        DocumentEditorSelectionSnapshot? expectedSelection = null,
        bool requireRuntimeSelectionToken = true,
        string hostSelector = DocumentEditorHostSelector)
    {
        var action = await OpenRibbonSelectAsync(page, testId, expectedSelection, requireRuntimeSelectionToken, hostSelector);
        await page.GetByTestId(testId).SelectOptionAsync(value);
        await WaitForEditorStableAsync(page, $"ribbon select '{testId}'", expectedSelection?.StartBlockId, expectedSelection?.SelectedText, hostSelector);
        action.AfterSelection = await ReadDocumentEditorSelectionSnapshotAsync(page, hostSelector);
        action.AfterRibbonState = await ReadRibbonFormattingStateAsync(page);
        return action;
    }

    /// <summary>Opens a floating toolbar select with a real pointer click.</summary>
    protected static Task<DocumentEditorToolbarActionResult> OpenFloatingSelectAsync(
        IPage page,
        string testId,
        DocumentEditorSelectionSnapshot? expectedSelection = null,
        bool requireRuntimeSelectionToken = true,
        string hostSelector = DocumentEditorHostSelector)
        => ClickToolbarElementWithPointerAsync(
            page,
            page.GetByTestId(testId),
            testId,
            "floating",
            expectedSelection,
            requireRuntimeSelectionToken,
            hostSelector,
            assertCommandStatePublished: false);

    /// <summary>Chooses a floating toolbar select option while preserving the captured editor selection.</summary>
    protected static async Task<DocumentEditorToolbarActionResult> ChooseFloatingSelectOptionAsync(
        IPage page,
        string testId,
        string value,
        DocumentEditorSelectionSnapshot? expectedSelection = null,
        bool requireRuntimeSelectionToken = true,
        string hostSelector = DocumentEditorHostSelector)
    {
        var action = await OpenFloatingSelectAsync(page, testId, expectedSelection, requireRuntimeSelectionToken, hostSelector);
        await page.GetByTestId(testId).SelectOptionAsync(value);
        await WaitForEditorStableAsync(page, $"floating select '{testId}'", expectedSelection?.StartBlockId, expectedSelection?.SelectedText, hostSelector);
        action.AfterSelection = await ReadDocumentEditorSelectionSnapshotAsync(page, hostSelector);
        action.AfterFloatingState = await ReadFloatingFormattingStateAsync(page);
        return action;
    }

    /// <summary>Opens a ribbon color picker popover using a real pointer click on its trigger.</summary>
    protected static async Task<DocumentEditorToolbarActionResult> OpenRibbonColorPickerAsync(
        IPage page,
        string pickerTestId,
        DocumentEditorSelectionSnapshot? expectedSelection = null,
        bool requireRuntimeSelectionToken = true,
        string hostSelector = DocumentEditorHostSelector)
    {
        var action = await ClickToolbarElementWithPointerAsync(
            page,
            page.GetByTestId(pickerTestId).Locator(".tm-color-picker-trigger"),
            pickerTestId,
            "ribbon",
            expectedSelection,
            requireRuntimeSelectionToken,
            hostSelector,
            assertCommandStatePublished: false);
        await Assertions.Expect(page.GetByTestId(pickerTestId).Locator(".tm-color-picker-dropdown")).ToBeVisibleAsync(new() { Timeout = 5000 });
        return action;
    }

    /// <summary>Opens a floating toolbar color picker popover using a real pointer click on its trigger.</summary>
    protected static async Task<DocumentEditorToolbarActionResult> OpenFloatingColorPickerAsync(
        IPage page,
        string pickerTestId,
        DocumentEditorSelectionSnapshot? expectedSelection = null,
        bool requireRuntimeSelectionToken = true,
        string hostSelector = DocumentEditorHostSelector)
    {
        var action = await ClickToolbarElementWithPointerAsync(
            page,
            page.GetByTestId(pickerTestId).Locator(".tm-color-picker-trigger"),
            pickerTestId,
            "floating",
            expectedSelection,
            requireRuntimeSelectionToken,
            hostSelector,
            assertCommandStatePublished: false);
        await Assertions.Expect(page.GetByTestId(pickerTestId).Locator(".tm-color-picker-dropdown")).ToBeVisibleAsync(new() { Timeout = 5000 });
        return action;
    }

    /// <summary>Chooses an open color-palette swatch by hex value with a real mouse click.</summary>
    protected static async Task ChooseColorPaletteSwatchAsync(IPage page, string hex)
    {
        var point = await page.EvaluateAsync<DocumentEditorPointProbe>(
            """
            (hex) => {
                const expected = normalizeColor(hex);
                const dropdown = Array.from(document.querySelectorAll('.tm-color-picker--open .tm-color-picker-dropdown'))
                    .find(isVisible);
                if (!dropdown) throw new Error('No open color picker dropdown was found.');
                const swatch = Array.from(dropdown.querySelectorAll('.tm-color-palette-swatch'))
                    .find(node => normalizeColor(getComputedStyle(node).backgroundColor) === expected);
                if (!swatch) throw new Error(`No visible color swatch matched '${hex}'.`);
                const rect = swatch.getBoundingClientRect();
                return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };

                function normalizeColor(value) {
                    if (!value) return '';
                    const text = String(value).trim().toLowerCase();
                    if (/^#[0-9a-f]{6}$/i.test(text)) return text;
                    const match = text.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([.\d]+))?\)$/i);
                    if (!match || match[4] === '0') return '';
                    return '#' + [match[1], match[2], match[3]]
                        .map(part => Math.max(0, Math.min(255, parseInt(part, 10))).toString(16).padStart(2, '0'))
                        .join('');
                }

                function isVisible(node) {
                    const rect = node?.getBoundingClientRect();
                    const style = node ? getComputedStyle(node) : null;
                    return !!(rect && style && rect.width > 0.5 && rect.height > 0.5 && style.display !== 'none' && style.visibility !== 'hidden');
                }
            }
            """,
            hex);
        await page.Mouse.ClickAsync((float)point.X, (float)point.Y);
        await page.Locator(".tm-color-picker--open .tm-color-picker-apply").ClickAsync();
        await WaitForEditorStableAsync(page, $"choose color swatch '{hex}'");
    }

    /// <summary>Enters a hex value into the open color picker and applies it.</summary>
    protected static async Task EnterColorHexAsync(IPage page, string hex)
    {
        var input = page.Locator(".tm-color-picker--open .tm-flat-color-picker-hex input").First;
        await input.FillAsync(hex);
        await input.PressAsync("Tab");
        await page.Locator(".tm-color-picker--open .tm-color-picker-apply").ClickAsync();
        await WaitForEditorStableAsync(page, $"enter color hex '{hex}'");
    }

    /// <summary>Clears the currently open color picker and applies the empty color value.</summary>
    protected static async Task ClearOpenColorPickerAsync(IPage page)
    {
        await page.Locator(".tm-color-picker--open .tm-color-palette-clear").ClickAsync();
        await page.Locator(".tm-color-picker--open .tm-color-picker-apply").ClickAsync();
        await WaitForEditorStableAsync(page, "clear color picker");
    }

    /// <summary>Reads computed styles for the visible text range in a document block.</summary>
    protected static Task<DocumentEditorTextRunComputedStyleProbe> ReadTextRunComputedStylesAsync(
        IPage page,
        string blockId,
        string text,
        string hostSelector = DocumentEditorHostSelector)
        => page.EvaluateAsync<DocumentEditorTextRunComputedStyleProbe>(
            """
            ({ hostSelector, blockId, text }) => {
                const host = document.querySelector(hostSelector);
                const block = visibleBlock(host, blockId);
                if (!block) throw new Error(`Could not find visible block '${blockId}'.`);
                const entries = collectTextEntries(block);
                const blockText = entries.map(entry => entry.text).join('');
                const start = blockText.indexOf(text);
                if (start < 0) throw new Error(`Text '${text}' was not found in block '${blockId}'. Block text: '${blockText}'.`);
                const end = start + text.length;
                const targetStyles = entries.filter(entry => entry.end > start && entry.start < end).map(toStyleEntry);
                const beforeStyles = entries.filter(entry => entry.end <= start).map(toStyleEntry);
                const afterStyles = entries.filter(entry => entry.start >= end).map(toStyleEntry);
                return {
                    blockId,
                    text,
                    blockText,
                    startOffset: start,
                    endOffset: end,
                    nodeCount: targetStyles.length,
                    targetStyles,
                    beforeStyles,
                    afterStyles,
                    hasMixedFormatting: hasMixedFormatting(targetStyles),
                    debug: JSON.stringify({ blockId, text, blockText, start, end, targetStyles, beforeStyles, afterStyles })
                };

                function visibleBlock(root, id) {
                    const escaped = CSS.escape(id);
                    return Array.from(root?.querySelectorAll(`[data-block-id="${escaped}"], [data-render-block-id="${escaped}"]`) || [])
                        .find(node => {
                            const rect = node.getBoundingClientRect();
                            const style = getComputedStyle(node);
                            return rect.width > 1 && rect.height > 1 && style.display !== 'none' && style.visibility !== 'hidden' && !node.closest('.tm-wysiwyg-page--virtual');
                        });
                }

                function collectTextEntries(root) {
                    const entries = [];
                    let offset = 0;
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            return node.nodeValue && node.nodeValue.length > 0 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                        }
                    });
                    while (walker.nextNode()) {
                        const node = walker.currentNode;
                        const textValue = node.nodeValue || '';
                        entries.push({ node, text: textValue, start: offset, end: offset + textValue.length });
                        offset += textValue.length;
                    }
                    return entries;
                }

                function toStyleEntry(entry) {
                    const element = entry.node.parentElement;
                    const style = getComputedStyle(element);
                    const fontSizePx = parseFloat(style.fontSize || '0') || 0;
                    const decoration = `${element?.style?.textDecoration || ''} ${style.textDecorationLine || ''} ${style.textDecoration || ''}`.toLowerCase();
                    const fontWeightNumber = parseInt(style.fontWeight || '400', 10);
                    return {
                        text: entry.text,
                        startOffset: entry.start,
                        endOffset: entry.end,
                        inlineId: element?.closest('[data-inline-id]')?.getAttribute('data-inline-id') || '',
                        parentHtml: element?.outerHTML?.slice(0, 700) || '',
                        fontWeight: style.fontWeight || '',
                        bold: style.fontWeight === 'bold' || (Number.isFinite(fontWeightNumber) && fontWeightNumber >= 600),
                        fontStyle: style.fontStyle || '',
                        italic: style.fontStyle === 'italic',
                        textDecorationLine: style.textDecorationLine || '',
                        underline: decoration.includes('underline'),
                        strikethrough: decoration.includes('line-through'),
                        fontSize: style.fontSize || '',
                        fontSizePx,
                        fontSizePt: fontSizePx * 0.75,
                        fontFamily: style.fontFamily || '',
                        color: style.color || '',
                        colorHex: normalizeColor(style.color || ''),
                        backgroundColor: style.backgroundColor || '',
                        backgroundColorHex: normalizeColor(style.backgroundColor || '')
                    };
                }

                function hasMixedFormatting(styles) {
                    if (styles.length < 2) return false;
                    const keys = ['fontWeight', 'fontStyle', 'textDecorationLine', 'fontSize', 'fontFamily', 'colorHex', 'backgroundColorHex'];
                    return keys.some(key => new Set(styles.map(style => String(style[key] || '').toLowerCase())).size > 1);
                }

                function normalizeColor(value) {
                    if (!value) return '';
                    const text = String(value).trim().toLowerCase();
                    if (text === 'transparent' || text === 'rgba(0, 0, 0, 0)') return '';
                    if (/^#[0-9a-f]{6}$/i.test(text)) return text;
                    const match = text.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([.\d]+))?\)$/i);
                    if (!match || match[4] === '0') return '';
                    return '#' + [match[1], match[2], match[3]]
                        .map(part => Math.max(0, Math.min(255, parseInt(part, 10))).toString(16).padStart(2, '0'))
                        .join('');
                }
            }
            """,
            new { hostSelector, blockId, text });

    /// <summary>Reads visible formatting state from the main ribbon and runtime debug bridge.</summary>
    protected static Task<DocumentEditorFormattingToolbarState> ReadRibbonFormattingStateAsync(IPage page)
        => ReadFormattingToolbarStateAsync(page, "ribbon");

    /// <summary>Reads visible formatting state from the floating toolbar and runtime debug bridge.</summary>
    protected static Task<DocumentEditorFormattingToolbarState> ReadFloatingFormattingStateAsync(IPage page)
        => ReadFormattingToolbarStateAsync(page, "floating");

    /// <summary>Asserts that ribbon and floating toolbar visible formatting states agree.</summary>
    protected static async Task AssertRibbonAndFloatingStateEqualAsync(IPage page)
    {
        var ribbon = await ReadRibbonFormattingStateAsync(page);
        var floating = await ReadFloatingFormattingStateAsync(page);
        if (!floating.IsVisible)
        {
            throw new AssertFailedException($"Floating toolbar is not visible. Ribbon state: {ribbon.Debug}");
        }

        Assert.AreEqual(ribbon.Bold, floating.Bold, $"Bold state differs. Ribbon={ribbon.Debug}; Floating={floating.Debug}");
        Assert.AreEqual(ribbon.Italic, floating.Italic, $"Italic state differs. Ribbon={ribbon.Debug}; Floating={floating.Debug}");
        Assert.AreEqual(ribbon.Underline, floating.Underline, $"Underline state differs. Ribbon={ribbon.Debug}; Floating={floating.Debug}");
        Assert.AreEqual(ribbon.Strikethrough, floating.Strikethrough, $"Strikethrough state differs. Ribbon={ribbon.Debug}; Floating={floating.Debug}");
        if (!string.IsNullOrWhiteSpace(ribbon.FontSize) && !string.IsNullOrWhiteSpace(floating.FontSize))
        {
            Assert.AreEqual(NormalizeFontSizeToken(ribbon.FontSize), NormalizeFontSizeToken(floating.FontSize), $"Font size state differs. Ribbon={ribbon.Debug}; Floating={floating.Debug}");
        }

        if (!string.IsNullOrWhiteSpace(ribbon.TextColor) && !string.IsNullOrWhiteSpace(floating.TextColor))
        {
            AssertCssColorEquals(ribbon.TextColor, floating.TextColor, $"Text color state differs. Ribbon={ribbon.Debug}; Floating={floating.Debug}");
        }

        if (!string.IsNullOrWhiteSpace(ribbon.HighlightColor) && !string.IsNullOrWhiteSpace(floating.HighlightColor))
        {
            AssertCssColorEquals(ribbon.HighlightColor, floating.HighlightColor, $"Highlight state differs. Ribbon={ribbon.Debug}; Floating={floating.Debug}");
        }
    }

    /// <summary>Asserts that a toolbar state reflects the computed style of the target text.</summary>
    protected static void AssertToolbarStateMatchesTextStyles(DocumentEditorFormattingToolbarState state, DocumentEditorTextRunComputedStyleProbe styles)
    {
        if (styles.TargetStyles.Length == 0)
        {
            throw new AssertFailedException($"No target text styles were captured. Styles: {styles.Debug}");
        }

        var allBold = styles.TargetStyles.All(style => style.Bold);
        var allItalic = styles.TargetStyles.All(style => style.Italic);
        var allUnderline = styles.TargetStyles.All(style => style.Underline);
        var allStrike = styles.TargetStyles.All(style => style.Strikethrough);
        Assert.AreEqual(allBold, state.Bold, $"Toolbar bold state does not match text styles. State={state.Debug}; Styles={styles.Debug}");
        Assert.AreEqual(allItalic, state.Italic, $"Toolbar italic state does not match text styles. State={state.Debug}; Styles={styles.Debug}");
        Assert.AreEqual(allUnderline, state.Underline, $"Toolbar underline state does not match text styles. State={state.Debug}; Styles={styles.Debug}");
        Assert.AreEqual(allStrike, state.Strikethrough, $"Toolbar strike state does not match text styles. State={state.Debug}; Styles={styles.Debug}");
    }

    protected static void AssertTextRunsAreBold(DocumentEditorTextRunComputedStyleProbe probe)
    {
        Assert.IsTrue(probe.TargetStyles.Length > 0, $"No target styles captured. Debug: {probe.Debug}");
        Assert.IsTrue(probe.TargetStyles.All(style => style.Bold),
            $"Expected all target text runs to be bold. Debug: {probe.Debug}");
    }

    protected static void AssertTextRunsAreNormalWeight(DocumentEditorTextRunComputedStyleProbe probe)
    {
        Assert.IsTrue(probe.TargetStyles.Length > 0, $"No target styles captured. Debug: {probe.Debug}");
        Assert.IsTrue(probe.TargetStyles.All(style => !style.Bold),
            $"Expected all target text runs to have normal font weight. Debug: {probe.Debug}");
    }

    protected static void AssertTextRunsFontStyle(DocumentEditorTextRunComputedStyleProbe probe, string expectedFontStyle)
    {
        Assert.IsTrue(probe.TargetStyles.Length > 0, $"No target styles captured. Debug: {probe.Debug}");
        Assert.IsTrue(probe.TargetStyles.All(style => string.Equals(style.FontStyle, expectedFontStyle, StringComparison.OrdinalIgnoreCase)),
            $"Expected all target text runs to have font-style '{expectedFontStyle}'. Debug: {probe.Debug}");
    }

    protected static void AssertTextRunsTextDecorationContains(DocumentEditorTextRunComputedStyleProbe probe, string expectedDecoration)
    {
        Assert.IsTrue(probe.TargetStyles.Length > 0, $"No target styles captured. Debug: {probe.Debug}");
        Assert.IsTrue(probe.TargetStyles.All(style => style.TextDecorationLine.Contains(expectedDecoration, StringComparison.OrdinalIgnoreCase)),
            $"Expected all target text runs to contain text decoration '{expectedDecoration}'. Debug: {probe.Debug}");
    }

    protected static void AssertTextRunsFontFamilyContains(DocumentEditorTextRunComputedStyleProbe probe, string expectedFontFamily)
    {
        Assert.IsTrue(probe.TargetStyles.Length > 0, $"No target styles captured. Debug: {probe.Debug}");
        Assert.IsTrue(probe.TargetStyles.All(style => style.FontFamily.Contains(expectedFontFamily, StringComparison.OrdinalIgnoreCase)),
            $"Expected all target text runs to use font family '{expectedFontFamily}'. Debug: {probe.Debug}");
    }

    protected static void AssertTextRunsFontSizeNearPt(DocumentEditorTextRunComputedStyleProbe probe, double expectedPt, double tolerancePt = 1.75)
    {
        Assert.IsTrue(probe.TargetStyles.Length > 0, $"No target styles captured. Debug: {probe.Debug}");
        var sizes = probe.TargetStyles.Select(style => style.FontSizePt).Where(size => size > 0).ToArray();
        Assert.IsTrue(sizes.Length > 0, $"No target font sizes captured. Debug: {probe.Debug}");
        Assert.IsTrue(sizes.All(size => Math.Abs(size - expectedPt) <= tolerancePt),
            $"Expected all target font sizes near {expectedPt:0.##}pt, got {string.Join(", ", sizes.Select(size => size.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)))}pt. Debug: {probe.Debug}");
    }

    protected static void AssertTextRunsColorEquals(DocumentEditorTextRunComputedStyleProbe probe, string expectedHex)
    {
        Assert.IsTrue(probe.TargetStyles.Length > 0, $"No target styles captured. Debug: {probe.Debug}");
        Assert.IsTrue(probe.TargetStyles.All(style => CssColorMatches(style.ColorHex, expectedHex) || CssColorMatches(style.Color, expectedHex)),
            $"Expected all target text colors to match {expectedHex}. Debug: {probe.Debug}");
    }

    protected static void AssertTextRunsBackgroundColorEquals(DocumentEditorTextRunComputedStyleProbe probe, string expectedHex)
    {
        Assert.IsTrue(probe.TargetStyles.Length > 0, $"No target styles captured. Debug: {probe.Debug}");
        Assert.IsTrue(probe.TargetStyles.All(style => CssColorMatches(style.BackgroundColorHex, expectedHex) || CssColorMatches(style.BackgroundColor, expectedHex)),
            $"Expected all target background colors to match {expectedHex}. Debug: {probe.Debug}");
    }

    protected static void AssertSurroundingTextStylesUnchanged(
        DocumentEditorTextRunComputedStyleProbe before,
        DocumentEditorTextRunComputedStyleProbe after)
    {
        AssertStyleArraysEquivalent(before.BeforeStyles, after.BeforeStyles, "before target text", before, after);
        AssertStyleArraysEquivalent(before.AfterStyles, after.AfterStyles, "after target text", before, after);
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

    /// <summary>Diagnostic read-only helper: reads text from the JS-owned runtime document model.</summary>
    protected static Task<string> ReadDocumentEditorModelBlockTextAsync(
        IPage page,
        string blockId,
        string hostSelector = "[data-testid='document-wysiwyg-host']")
        => page.EvaluateAsync<string>(
            """
            ({ hostSelector, blockId }) => {
                const host = document.querySelector(hostSelector);
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const raw = window.tmDocumentEditorRuntime?.getDocumentSnapshot?.(instanceId)
                    || window.tmDocumentEditorEngine?.getDocumentSnapshot?.(instanceId)
                    || window.tmDocumentEditorRuntime?.getDocument?.(instanceId)
                    || window.tmDocumentEditorEngine?.getDocument?.(instanceId)
                    || null;
                const snapshot = typeof raw === 'string' ? JSON.parse(raw) : raw;
                const documentModel = snapshot?.Document || snapshot?.document || snapshot?.csharpDocument || snapshot;
                const block = findBlock(documentModel);
                if (!block) {
                    return typeof raw === 'string' ? raw : JSON.stringify(raw ?? null);
                }
                return collectBlockText(block);

                function findBlock(value) {
                    if (!value || typeof value !== 'object') return null;
                    if ((value.Id || value.id) === blockId) return value;
                    const childCollections = [
                        value.Blocks,
                        value.blocks,
                        value.Children,
                        value.children,
                        value.Rows,
                        value.rows,
                        value.Cells,
                        value.cells,
                        value.Inlines,
                        value.inlines
                    ];
                    for (const collection of childCollections) {
                        if (!Array.isArray(collection)) continue;
                        for (const child of collection) {
                            const found = findBlock(child);
                            if (found) return found;
                        }
                    }
                    for (const key of ['Document', 'document', 'Body', 'body', 'Content', 'content']) {
                        const found = findBlock(value[key]);
                        if (found) return found;
                    }
                    return null;
                }

                function collectBlockText(value) {
                    if (!value || typeof value !== 'object') return '';
                    if (typeof value.Text === 'string') return value.Text;
                    if (typeof value.text === 'string') return value.text;
                    if (typeof value.Content === 'string') return value.Content;
                    if (typeof value.content === 'string') return value.content;
                    if (typeof value.Data === 'string') return value.Data;
                    if (typeof value.data === 'string') return value.data;
                    const inlines = value.Inlines || value.inlines || value.Runs || value.runs || [];
                    if (Array.isArray(inlines) && inlines.length) {
                        return inlines.map(collectInlineText).join('');
                    }
                    const cells = value.Cells || value.cells || value.Rows || value.rows || [];
                    if (Array.isArray(cells) && cells.length) {
                        return cells.map(collectBlockText).join('');
                    }
                    return collectNamedTextStrings(value).join('');
                }

                function collectInlineText(value) {
                    if (!value || typeof value !== 'object') return '';
                    const direct = value.Text ?? value.text ?? value.Content ?? value.content ?? value.Value ?? value.value ?? value.Data ?? value.data;
                    if (direct !== undefined && direct !== null) return String(direct);
                    return collectNamedTextStrings(value).join('');
                }

                function collectNamedTextStrings(value, depth = 0) {
                    if (!value || typeof value !== 'object' || depth > 4) return [];
                    const result = [];
                    for (const [key, child] of Object.entries(value)) {
                        if (child === null || child === undefined) continue;
                        if (typeof child === 'string') {
                            if (/^(text|content|value|data|plainText|displayText)$/i.test(key)) {
                                result.push(child);
                            }
                            continue;
                        }

                        if (Array.isArray(child)) {
                            for (const item of child) {
                                result.push(...collectNamedTextStrings(item, depth + 1));
                            }
                            continue;
                        }

                        if (typeof child === 'object' && !/^(style|marks|metadata|layout|range|selection|author)$/i.test(key)) {
                            result.push(...collectNamedTextStrings(child, depth + 1));
                        }
                    }

                    return result;
                }
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

    /// <summary>Diagnostic read-only helper: captures image object, caret, model, and focus diagnostics for parity tests.</summary>
    protected static Task<DocumentEditorImageDiagnosticsProbe> ReadDocumentEditorImageDiagnosticsAsync(
        IPage page,
        string? imageId = null,
        string hostSelector = DocumentEditorHostSelector)
        => page.EvaluateAsync<DocumentEditorImageDiagnosticsProbe>(
            """
            ({ hostSelector, imageId }) => {
                const host = document.querySelector(hostSelector);
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const runtimeSelection = getRuntimeSelection(instanceId);
                const documentModel = getRuntimeDocument(instanceId);
                const selection = getSelectionProbe(host);
                const activeImageId = readActiveImageId(host, runtimeSelection);
                const effectiveImageId = imageId || activeImageId;
                const modelImage = findImageObject(documentModel, effectiveImageId);
                const domImage = findImageDomAnchor(host, effectiveImageId);
                const targetImage = domImage.anchorBlockId ? domImage : modelImage;
                const imageRect = getImageRect(host, effectiveImageId);
                const imageCenterTarget = getImageCenterTarget(imageRect);
                const caretRect = getCaretRect();
                const lineIntervals = getLineIntervalsAroundImage(host, imageRect);
                const topLevelImageBlockCount = countTopLevelImageBlocks(documentModel);
                const drawingRunCount = countDrawingRuns(documentModel);
                const activeElement = document.activeElement;
                const hostHasFocus = !!(host && activeElement && (activeElement === host || host.contains(activeElement)));
                const imageToolbarVisible = isVisible(document.querySelector('[data-testid="document-image-toolbar"], [data-human-testid="document-image-toolbar"], [data-testid="document-wysiwyg-image-toolbar"], .tm-document-editor__image-toolbar, .tm-wysiwyg-image-toolbar'));
                const selectionMode = inferSelectionMode(runtimeSelection, activeImageId);
                const engineDebug = getEngineDebug(instanceId);

                return {
                    instanceId,
                    selectionMode,
                    activeImageId,
                    caretBlockId: selection.blockId,
                    caretOffset: selection.offset,
                    caretRect,
                    anchorBlockId: targetImage.anchorBlockId || '',
                    anchorOffset: targetImage.anchorOffset,
                    topLevelImageBlockCount,
                    drawingRunCount,
                    imageRect,
                    lineIntervals,
                    hostHasFocus,
                    imageToolbarVisible,
                    runtimeSelectionJson: safeJson(runtimeSelection),
                    documentModelJson: safeJson(documentModel),
                    debug: safeJson({
                        imageId,
                        effectiveImageId,
                        activeImageId,
                        selectionMode,
                        selection,
                        targetImage,
                        imageRect,
                        imageCenterTarget,
                        lineIntervals,
                        topLevelImageBlockCount,
                        drawingRunCount,
                        activeElement: describeElement(activeElement),
                        hostHasFocus,
                        imageToolbarVisible,
                        runtimeSelection,
                        objectPointer: engineDebug?.lastObjectPointerInteraction || null,
                        commandCount: engineDebug?.commandCount ?? null,
                        lastTransaction: engineDebug?.lastTransaction || null,
                        lastError: engineDebug?.lastError || null,
                        lastOperationValidation: engineDebug?.lastOperationValidation || null
                    })
                };

                function getRuntimeSelection(id) {
                    try {
                        return window.tmDocumentEditorRuntime?.getRuntimeSelection?.(id)
                            || window.tmDocumentEditorRuntime?.getSelectionSnapshot?.(id)
                            || window.tmDocumentEditorEngine?.getSelectionSnapshot?.(id)
                            || window.tmDocumentEditorDebug?.getRuntimeState?.(id)?.currentSelection
                            || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function getRuntimeDocument(id) {
                    try {
                        const raw = window.tmDocumentEditorRuntime?.getDocumentSnapshot?.(id)
                            || window.tmDocumentEditorEngine?.getDocumentSnapshot?.(id)
                            || window.tmDocumentEditorRuntime?.getDocument?.(id)
                            || window.tmDocumentEditorEngine?.getDocument?.(id)
                            || null;
                        const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
                        return parsed?.Document || parsed?.document || parsed?.csharpDocument || parsed || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function getEngineDebug(id) {
                    try {
                        return window.tmDocumentEditorEngine?.getDebugSnapshot?.(id)
                            || window.tmDocumentEditorDebug?.getRuntimeState?.(id)
                            || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function inferSelectionMode(runtimeSelection, currentImageId) {
                    const explicitMode = runtimeSelection?.SelectionMode
                        || runtimeSelection?.selectionMode
                        || runtimeSelection?.Mode
                        || runtimeSelection?.mode
                        || '';
                    if (explicitMode) return String(explicitMode);
                    const region = String(runtimeSelection?.Region ?? runtimeSelection?.region ?? '').toLowerCase();
                    if (region === 'image' || currentImageId) return 'Object';
                    return 'Text';
                }

                function readActiveImageId(root, runtimeSelection) {
                    const fromSelection = runtimeSelection?.ObjectSelection?.ObjectId
                        || runtimeSelection?.objectSelection?.objectId
                        || runtimeSelection?.ActiveObjectId
                        || runtimeSelection?.activeObjectId
                        || runtimeSelection?.ActiveImageBlockId
                        || runtimeSelection?.activeImageBlockId
                        || runtimeSelection?.ObjectId
                        || runtimeSelection?.objectId
                        || '';
                    if (fromSelection) return String(fromSelection);
                    const selected = root?.querySelector('.tm-wysiwyg-image--selected, [aria-selected="true"][data-block-id], [data-object-selected="true"]');
                    return selected?.getAttribute('data-object-id')
                        || selected?.getAttribute('data-block-id')
                        || selected?.getAttribute('data-render-block-id')
                        || '';
                }

                function getSelectionProbe(root) {
                    const nativeSelection = window.getSelection();
                    const empty = { blockId: '', offset: -1 };
                    if (!nativeSelection || nativeSelection.rangeCount === 0) return empty;
                    const focusNode = nativeSelection.focusNode;
                    const focusElement = elementOf(focusNode);
                    const block = focusElement?.closest?.('[data-block-id], [data-render-block-id]');
                    if (!block || (root && !root.contains(block))) return empty;
                    let offset = -1;
                    try {
                        const pre = document.createRange();
                        pre.selectNodeContents(block);
                        pre.setEnd(focusNode, nativeSelection.focusOffset);
                        offset = pre.toString().length;
                    } catch {
                        offset = -1;
                    }
                    return {
                        blockId: block.getAttribute('data-block-id') || block.getAttribute('data-render-block-id') || '',
                        offset
                    };
                }

                function getCaretRect() {
                    const nativeSelection = window.getSelection();
                    if (!nativeSelection || nativeSelection.rangeCount === 0) return zeroRect();
                    const range = nativeSelection.getRangeAt(0).cloneRange();
                    range.collapse(false);
                    const rect = range.getBoundingClientRect();
                    if (rect && rect.height > 0) return toRect(rect);
                    try {
                        const marker = document.createElement('span');
                        marker.textContent = '\u200b';
                        range.insertNode(marker);
                        const markerRect = toRect(marker.getBoundingClientRect());
                        marker.remove();
                        return markerRect;
                    } catch {
                        return zeroRect();
                    }
                }

                function getImageRect(root, id) {
                    const selectors = [];
                    if (id) {
                        const escaped = cssEscape(id);
                        selectors.push(`[data-object-id="${escaped}"]`);
                        selectors.push(`[data-block-id="${escaped}"]`);
                        selectors.push(`[data-render-block-id="${escaped}"]`);
                    }
                    selectors.push('.tm-wysiwyg-image--selected');
                    selectors.push('figure.tm-wysiwyg-image');
                    selectors.push('.tm-render-image-widget');
                    selectors.push('.tm-wysiwyg-inline-drawing[data-object-id]');
                    for (const selector of selectors) {
                        const node = root?.querySelector(selector);
                        if (isVisible(node)) {
                            return toRect((node.querySelector?.('img') || node).getBoundingClientRect());
                        }
                    }
                    return zeroRect();
                }

                function getImageCenterTarget(imageRect) {
                    if (!imageRect || imageRect.width <= 0 || imageRect.height <= 0) return null;
                    const x = imageRect.x + imageRect.width / 2;
                    const y = imageRect.y + imageRect.height / 2;
                    const target = document.elementFromPoint(x, y);
                    return {
                        x,
                        y,
                        target: describeElement(target),
                        objectLayerItem: describeElement(target?.closest?.('.tm-wysiwyg-object-layer-item, [data-testid="document-wysiwyg-object-layer-item"]') || null),
                        selectionOverlay: describeElement(target?.closest?.('.tm-wysiwyg-object-selection-overlay, [data-testid="document-wysiwyg-object-selection-overlay"]') || null),
                        textBlock: describeElement(target?.closest?.('.tm-wysiwyg-block[data-block-id]') || null)
                    };
                }

                function getLineIntervalsAroundImage(root, imageRect) {
                    if (!root || !imageRect || imageRect.width <= 0 || imageRect.height <= 0) return [];
                    const body = Array.from(root.querySelectorAll('.tm-wysiwyg-page__body, [data-render-frame="body"], [contenteditable="true"]')).find(isVisible) || root;
                    const bodyRect = toRect(body.getBoundingClientRect());
                    const lines = collectTextLineRects(root)
                        .filter(line => verticalOverlap(line.rect, imageRect) > 0.5)
                        .map(line => ({
                            blockId: line.blockId,
                            x: line.rect.x,
                            y: line.rect.y,
                            width: line.rect.width,
                            height: line.rect.height,
                            leftAvailable: Math.max(0, imageRect.x - bodyRect.x),
                            rightAvailable: Math.max(0, bodyRect.x + bodyRect.width - (imageRect.x + imageRect.width))
                        }));
                    return lines;
                }

                function collectTextLineRects(root) {
                    const result = [];
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            if (!node.nodeValue?.trim()) return NodeFilter.FILTER_REJECT;
                            const parent = node.parentElement;
                            if (!parent || parent.closest('figure, [data-testid*="toolbar"], .tm-document-editor__ribbon, [role="menu"], .tm-wysiwyg-page__layer--object, .tm-wysiwyg-page__layer--selection, .tm-wysiwyg-page__layer--guides, .tm-wysiwyg-layout-bubble')) return NodeFilter.FILTER_REJECT;
                            return isVisible(parent) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                        }
                    });
                    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                        const range = document.createRange();
                        range.selectNodeContents(node);
                        const block = node.parentElement?.closest('[data-block-id], [data-render-block-id]');
                        for (const rect of Array.from(range.getClientRects())) {
                            if (rect.width > 0.5 && rect.height > 0.5) {
                                result.push({
                                    blockId: block?.getAttribute('data-block-id') || block?.getAttribute('data-render-block-id') || '',
                                    rect: toRect(rect)
                                });
                            }
                        }
                    }
                    return result;
                }

                function findImageObject(documentModel, id) {
                    const empty = { anchorBlockId: '', anchorOffset: -1 };
                    if (!documentModel || typeof documentModel !== 'object') return empty;
                    const matches = [];
                    walk(documentModel, (node, parent) => {
                        const nodeId = node.ObjectId || node.objectId || node.Id || node.id || '';
                        const isImageBlock = isTopLevelImageBlock(node);
                        const isDrawing = isDrawingRun(node);
                        if ((id && String(nodeId) === String(id)) || (!id && (isImageBlock || isDrawing))) {
                            const layout = node.Layout || node.layout || node.Content?.Layout || node.content?.layout || {};
                            const anchor = layout.Anchor || layout.anchor || {};
                            matches.push({
                                anchorBlockId: anchor.BlockId || anchor.blockId || layout.AnchorBlockId || layout.anchorBlockId || parent?.Id || parent?.id || '',
                                anchorOffset: Number(anchor.Offset ?? anchor.offset ?? layout.AnchorOffset ?? layout.anchorOffset ?? -1)
                            });
                        }
                    });
                    return matches[0] || empty;
                }

                function findImageDomAnchor(root, id) {
                    const empty = { anchorBlockId: '', anchorOffset: -1 };
                    if (!root || !id) return empty;
                    const escaped = cssEscape(id);
                    const node = root.querySelector(`[data-testid="document-wysiwyg-object-layer-item"][data-object-id="${escaped}"], [data-object-id="${escaped}"]`);
                    if (!node) return empty;
                    return {
                        anchorBlockId: node.getAttribute('data-anchor-block-id') || node.getAttribute('data-block-id') || '',
                        anchorOffset: Number(node.getAttribute('data-anchor-offset') || 0)
                    };
                }

                function countTopLevelImageBlocks(documentModel) {
                    const blocks = documentModel?.Blocks || documentModel?.blocks || [];
                    if (!Array.isArray(blocks)) return 0;
                    return blocks.filter(isTopLevelImageBlock).length;
                }

                function countDrawingRuns(documentModel) {
                    let count = 0;
                    for (const rootBlock of collectDocumentBlocks(documentModel)) {
                        visitBlockInlines(rootBlock, inline => {
                            if (isDrawingRun(inline)) count++;
                        });
                    }
                    return count;
                }

                function collectDocumentBlocks(document) {
                    if (!document || typeof document !== 'object') return [];
                    const blocks = [];
                    appendBlocks(blocks, document.Blocks || document.blocks);
                    appendBlocks(blocks, document.body?.blocks || document.Body?.Blocks);
                    for (const header of [...asArray(document.Headers || document.headers), ...asArray(document.HeadersFooters || document.headersFooters)]) {
                        appendBlocks(blocks, header.Blocks || header.blocks);
                    }
                    for (const footer of asArray(document.Footers || document.footers)) {
                        appendBlocks(blocks, footer.Blocks || footer.blocks);
                    }
                    return blocks;
                }

                function visitBlockInlines(block, visitor) {
                    if (!block || typeof block !== 'object') return;
                    const content = block.Content || block.content || {};
                    for (const inline of asArray(content.Inlines || content.inlines || content.Runs || content.runs)) {
                        visitor(inline);
                    }
                    for (const row of asArray(content.Rows || content.rows)) {
                        for (const cell of asArray(row.Cells || row.cells)) {
                            for (const childBlock of asArray(cell.Blocks || cell.blocks)) visitBlockInlines(childBlock, visitor);
                        }
                    }
                    for (const childBlock of asArray(content.Blocks || content.blocks || block.Blocks || block.blocks)) {
                        visitBlockInlines(childBlock, visitor);
                    }
                }

                function appendBlocks(target, blocks) {
                    for (const block of asArray(blocks)) target.push(block);
                }

                function asArray(value) {
                    return Array.isArray(value) ? value : [];
                }

                function isTopLevelImageBlock(node) {
                    const type = node?.Type ?? node?.type;
                    const contentType = node?.Content?.$type ?? node?.content?.$type;
                    return type === 5 || String(type).toLowerCase() === 'image' || String(contentType).toLowerCase() === 'image';
                }

                function isDrawingRun(node) {
                    if (!node || typeof node !== 'object') return false;
                    const discriminator = node.$type || node.Kind || node.kind || node.Type || node.type || '';
                    return String(discriminator).toLowerCase() === 'drawing'
                        || !!(node.ObjectId || node.objectId) && !!(node.Layout || node.layout) && (
                            !!(node.Image || node.image || node.Url || node.url || node.AssetId || node.assetId || node.DrawingKind || node.drawingKind)
                            || node.Source !== undefined
                            || node.source !== undefined);
                }

                function walk(value, visitor, parent = null, depth = 0) {
                    if (!value || typeof value !== 'object' || depth > 8) return;
                    if (!Array.isArray(value)) {
                        visitor(value, parent);
                    }
                    const entries = Array.isArray(value) ? value.map((item, index) => [index, item]) : Object.entries(value);
                    for (const [, child] of entries) {
                        if (child && typeof child === 'object') {
                            walk(child, visitor, Array.isArray(value) ? parent : value, depth + 1);
                        }
                    }
                }

                function elementOf(node) {
                    if (!node) return null;
                    return node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement || null;
                }

                function verticalOverlap(a, b) {
                    return Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
                }

                function isVisible(node) {
                    if (!node) return false;
                    const rect = node.getBoundingClientRect();
                    const style = getComputedStyle(node);
                    return rect.width > 0.5 && rect.height > 0.5 && style.display !== 'none' && style.visibility !== 'hidden' && Number(style.opacity || 1) > 0.01;
                }

                function describeElement(node) {
                    if (!node) return '';
                    return `${node.tagName || ''}#${node.id || ''}.${String(node.className || '').replace(/\s+/g, '.')}`;
                }

                function cssEscape(value) {
                    return window.CSS?.escape ? window.CSS.escape(String(value)) : String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
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

                function zeroRect() {
                    return { x: 0, y: 0, width: 0, height: 0 };
                }

                function safeJson(value) {
                    try { return JSON.stringify(value ?? null); }
                    catch (error) { return JSON.stringify({ error: String(error) }); }
                }
            }
            """,
            new { hostSelector, imageId = imageId ?? string.Empty });

    /// <summary>Diagnostic read-only helper: reads whether the current editor selection is text or object-based.</summary>
    protected static async Task<string> ReadDocumentEditorSelectionModeAsync(IPage page, string hostSelector = DocumentEditorHostSelector)
        => (await ReadDocumentEditorImageDiagnosticsAsync(page, hostSelector: hostSelector)).SelectionMode;

    /// <summary>Diagnostic read-only helper: reads the active image object identifier if one is selected.</summary>
    protected static async Task<string> ReadActiveDocumentEditorImageIdAsync(IPage page, string hostSelector = DocumentEditorHostSelector)
        => (await ReadDocumentEditorImageDiagnosticsAsync(page, hostSelector: hostSelector)).ActiveImageId;

    /// <summary>Diagnostic read-only helper: reads the current caret block and offset.</summary>
    protected static async Task<DocumentEditorCaretProbe> ReadDocumentEditorCaretProbeAsync(IPage page, string hostSelector = DocumentEditorHostSelector)
    {
        var diagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, hostSelector: hostSelector);
        return new DocumentEditorCaretProbe
        {
            BlockId = diagnostics.CaretBlockId,
            Offset = diagnostics.CaretOffset,
            Rect = diagnostics.CaretRect
        };
    }

    /// <summary>Diagnostic read-only helper: reads image anchor information from the runtime model.</summary>
    protected static async Task<DocumentEditorImageAnchorProbe> ReadDocumentEditorImageAnchorAsync(
        IPage page,
        string imageId,
        string hostSelector = DocumentEditorHostSelector)
    {
        var diagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, imageId, hostSelector);
        return new DocumentEditorImageAnchorProbe
        {
            ImageId = imageId,
            AnchorBlockId = diagnostics.AnchorBlockId,
            AnchorOffset = diagnostics.AnchorOffset
        };
    }

    /// <summary>Diagnostic read-only helper: counts top-level image blocks in the runtime document.</summary>
    protected static async Task<int> ReadDocumentEditorTopLevelImageBlockCountAsync(IPage page, string hostSelector = DocumentEditorHostSelector)
        => (await ReadDocumentEditorImageDiagnosticsAsync(page, hostSelector: hostSelector)).TopLevelImageBlockCount;

    /// <summary>Diagnostic read-only helper: counts drawing runs in the runtime document.</summary>
    protected static async Task<int> ReadDocumentEditorDrawingRunCountAsync(IPage page, string hostSelector = DocumentEditorHostSelector)
        => (await ReadDocumentEditorImageDiagnosticsAsync(page, hostSelector: hostSelector)).DrawingRunCount;

    /// <summary>Diagnostic read-only helper: reads drawing runs from the runtime document, optionally scoped to one block.</summary>
    protected static Task<DocumentEditorDrawingRunProbe[]> ReadDocumentEditorDrawingRunsAsync(
        IPage page,
        string? blockId = null,
        string? objectId = null,
        string hostSelector = DocumentEditorHostSelector)
        => page.EvaluateAsync<DocumentEditorDrawingRunProbe[]>(
            """
            ({ hostSelector, blockId, objectId }) => {
                const host = document.querySelector(hostSelector);
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const documentModel = getRuntimeDocument(instanceId);
                const runs = [];
                for (const rootBlock of collectDocumentBlocks(documentModel)) {
                    visitBlock(rootBlock);
                }
                return runs
                    .filter(run => !blockId || run.blockId === blockId || run.anchorBlockId === blockId)
                    .filter(run => !objectId || run.objectId === objectId)
                    .sort((a, b) => a.blockId.localeCompare(b.blockId) || a.inlineIndex - b.inlineIndex || a.objectId.localeCompare(b.objectId));

                function getRuntimeDocument(id) {
                    try {
                        const raw = window.tmDocumentEditorRuntime?.getDocumentSnapshot?.(id)
                            || window.tmDocumentEditorEngine?.getDocumentSnapshot?.(id)
                            || window.tmDocumentEditorRuntime?.getDocument?.(id)
                            || window.tmDocumentEditorEngine?.getDocument?.(id)
                            || null;
                        const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
                        return parsed?.Document || parsed?.document || parsed?.csharpDocument || parsed || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function collectDocumentBlocks(document) {
                    if (!document || typeof document !== 'object') return [];
                    const blocks = [];
                    appendBlocks(blocks, document.Blocks || document.blocks);
                    appendBlocks(blocks, document.body?.blocks || document.Body?.Blocks);
                    for (const header of [...asArray(document.Headers || document.headers), ...asArray(document.HeadersFooters || document.headersFooters)]) {
                        appendBlocks(blocks, header.Blocks || header.blocks);
                    }
                    for (const footer of [...asArray(document.Footers || document.footers)]) {
                        appendBlocks(blocks, footer.Blocks || footer.blocks);
                    }
                    return blocks;
                }

                function visitBlock(block) {
                    if (!block || typeof block !== 'object') return;
                    const currentBlockId = String(block.Id || block.id || '');
                    const content = block.Content || block.content || {};
                    const inlines = content.Inlines || content.inlines || content.Runs || content.runs || [];
                    asArray(inlines).forEach((inline, index) => {
                        if (!isDrawingRun(inline)) return;
                        const layout = inline.Layout || inline.layout || {};
                        const anchor = layout.Anchor || layout.anchor || {};
                        const wrap = layout.Wrap || layout.wrap || {};
                        const transform = layout.Transform || layout.transform || {};
                        const crop = transform.Crop || transform.crop || {};
                        runs.push({
                            blockId: currentBlockId || String(anchor.BlockId || anchor.blockId || ''),
                            objectId: String(inline.ObjectId || inline.objectId || inline.Id || inline.id || ''),
                            runId: String(inline.Id || inline.id || ''),
                            anchorBlockId: String(anchor.BlockId || anchor.blockId || layout.AnchorBlockId || layout.anchorBlockId || currentBlockId || ''),
                            anchorOffset: Number(anchor.Offset ?? anchor.offset ?? layout.AnchorOffset ?? layout.anchorOffset ?? 0) || 0,
                            inlineIndex: Number(anchor.InlineIndex ?? anchor.inlineIndex ?? index),
                            altText: String(inline.AltText || inline.altText || ''),
                            url: String(inline.Url || inline.url || ''),
                            region: normalizeAnchorRegion(anchor.Region ?? anchor.region ?? ''),
                            tableId: String(anchor.TableId ?? anchor.tableId ?? ''),
                            cellId: String(anchor.CellId ?? anchor.cellId ?? ''),
                            headerFooterId: String(anchor.HeaderFooterId ?? anchor.headerFooterId ?? ''),
                            wrapMode: normalizeWrapMode(wrap.Mode ?? wrap.mode ?? ''),
                            width: Number(transform.Width ?? transform.width ?? inline.Size?.Width ?? inline.size?.width ?? 0) || 0,
                            height: Number(transform.Height ?? transform.height ?? inline.Size?.Height ?? inline.size?.height ?? 0) || 0,
                            cropLeft: Number(crop.Left ?? crop.left ?? 0) || 0,
                            cropTop: Number(crop.Top ?? crop.top ?? 0) || 0,
                            cropRight: Number(crop.Right ?? crop.right ?? 0) || 0,
                            cropBottom: Number(crop.Bottom ?? crop.bottom ?? 0) || 0
                        });
                    });

                    for (const row of asArray(content.Rows || content.rows)) {
                        for (const cell of asArray(row.Cells || row.cells)) {
                            for (const childBlock of asArray(cell.Blocks || cell.blocks)) visitBlock(childBlock);
                        }
                    }
                    for (const childBlock of asArray(content.Blocks || content.blocks || block.Blocks || block.blocks)) {
                        visitBlock(childBlock);
                    }
                }

                function appendBlocks(target, blocks) {
                    for (const block of asArray(blocks)) target.push(block);
                }

                function asArray(value) {
                    return Array.isArray(value) ? value : [];
                }

                function isDrawingRun(node) {
                    if (!node || typeof node !== 'object') return false;
                    const discriminator = node.$type || node.Kind || node.kind || node.Type || node.type || '';
                    return String(discriminator).toLowerCase() === 'drawing'
                        || (!!(node.ObjectId || node.objectId) && !!(node.Layout || node.layout) && (
                            !!(node.Image || node.image || node.Url || node.url || node.AssetId || node.assetId || node.DrawingKind || node.drawingKind)
                            || node.Source !== undefined
                            || node.source !== undefined));
                }

                function normalizeWrapMode(mode) {
                    const raw = String(mode ?? '').trim();
                    if (raw === '0') return 'Inline';
                    if (raw === '1') return 'Square';
                    if (raw === '2') return 'Tight';
                    if (raw === '3') return 'Through';
                    if (raw === '4') return 'TopBottom';
                    if (raw === '5') return 'BehindText';
                    if (raw === '6') return 'InFrontOfText';
                    return raw || 'Inline';
                }

                function normalizeAnchorRegion(region) {
                    const raw = String(region ?? '').trim().toLowerCase();
                    if (raw === '1' || raw === 'header') return 'Header';
                    if (raw === '2' || raw === 'footer') return 'Footer';
                    if (raw === '3' || raw === 'footnote') return 'Footnote';
                    if (raw === '4' || raw === 'endnote') return 'Endnote';
                    if (raw === '5' || raw === 'floatingobject' || raw === 'floating-object') return 'FloatingObject';
                    if (raw === '6' || raw === 'tablecell' || raw === 'table-cell') return 'TableCell';
                    if (raw === '7' || raw === 'comment') return 'Comment';
                    return 'Body';
                }
            }
            """,
            new { hostSelector, blockId = blockId ?? string.Empty, objectId = objectId ?? string.Empty });

    /// <summary>Diagnostic read-only helper: reads the visible rectangle of an image object.</summary>
    protected static async Task<DocumentEditorRectProbe> ReadDocumentEditorImageRectAsync(
        IPage page,
        string imageId,
        string hostSelector = DocumentEditorHostSelector)
        => (await ReadDocumentEditorImageDiagnosticsAsync(page, imageId, hostSelector)).ImageRect;

    /// <summary>Diagnostic read-only helper: reads visible text line intervals around an image object.</summary>
    protected static async Task<DocumentEditorLineIntervalProbe[]> ReadDocumentEditorLineIntervalsAroundImageAsync(
        IPage page,
        string imageId,
        string hostSelector = DocumentEditorHostSelector)
        => (await ReadDocumentEditorImageDiagnosticsAsync(page, imageId, hostSelector)).LineIntervals;

    /// <summary>Asserts that keyboard focus is still within the editable document host.</summary>
    protected static async Task AssertDocumentEditorHostHasFocusAsync(IPage page, string hostSelector = DocumentEditorHostSelector)
    {
        var diagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, hostSelector: hostSelector);
        if (!diagnostics.HostHasFocus)
        {
            throw new AssertFailedException($"Expected focus to remain inside the document editor host. Diagnostics: {diagnostics.Debug}");
        }
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
                const figures = Array.from(host?.querySelectorAll('figure[data-block-id], figure.tm-wysiwyg-image, .tm-render-image-widget, .tm-wysiwyg-inline-drawing[data-object-id], .tm-wysiwyg-object-layer-item[data-object-id], [data-testid="phase18-image"]') || []);
                const figure = figures[Math.max(0, Number(imageIndex) || 0)] || figures[0];
                if (!figure) throw new Error('No image figure found for strict image drag/resize helper.');
                const objectId = figure.getAttribute('data-object-id') || figure.getAttribute('data-render-object-id') || '';
                const overlay = objectId ? host?.querySelector?.(`[data-testid="document-wysiwyg-object-selection-overlay"][data-object-id="${CSS.escape(objectId)}"]`) : null;
                const handle = overlay?.querySelector?.('[data-resize-handle="se"], [data-testid$="resize-handle-se"], .tm-wysiwyg-object-resize-handle--se')
                    || figure.querySelector('[data-resize-handle="se"], [data-testid$="resize-handle-se"], .tm-wysiwyg-object-resize-handle--se, .tm-wysiwyg-image__resize-handle')
                    || figure;
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
                    return Array.from(root?.querySelectorAll('figure[data-block-id], figure.tm-wysiwyg-image, .tm-render-image-widget, .tm-wysiwyg-inline-drawing[data-object-id], .tm-wysiwyg-object-layer-item[data-object-id], [data-testid="phase18-image"]') || [])
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

    /// <summary>Asserts that a user-facing element is fully inside the browser viewport.</summary>
    protected static async Task ExpectRectInsideViewportAsync(IPage page, ILocator locator, string name)
    {
        var rect = await GetLocatorRectAsync(locator, name);
        var viewport = await page.EvaluateAsync<DocumentEditorRectProbe>(
            "() => ({ x: 0, y: 0, width: window.innerWidth || document.documentElement.clientWidth || 0, height: window.innerHeight || document.documentElement.clientHeight || 0 })");
        if (!RectInside(rect, viewport, 1.5))
        {
            throw new AssertFailedException($"Expected {name} rect {FormatRect(rect)} to stay inside viewport {FormatRect(viewport)}.");
        }
    }

    /// <summary>Asserts that the floating toolbar does not overlap the main ribbon.</summary>
    protected static Task ExpectToolbarAvoidsRibbonAsync(IPage page, ILocator toolbar)
        => ExpectNoOverlapAsync(toolbar, page.GetByTestId("document-toolbar"), 1.5);

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

    /// <summary>Scrolls the nearest scrollable document viewport and reports the applied delta.</summary>
    protected static Task<DocumentEditorScrollProbe> ScrollDocumentViewportAsync(
        IPage page,
        double deltaY,
        string hostSelector = DocumentEditorHostSelector)
        => page.EvaluateAsync<DocumentEditorScrollProbe>(
            """
            async ({ hostSelector, deltaY }) => {
                const host = document.querySelector(hostSelector);
                const candidates = [];
                for (let node = host; node; node = node.parentElement) {
                    const style = getComputedStyle(node);
                    const scrollable = /(auto|scroll|overlay)/.test(style.overflowY)
                        && node.scrollHeight > node.clientHeight + 20;
                    if (scrollable) candidates.push(node);
                }
                const documentScroller = document.scrollingElement || document.documentElement;
                candidates.push(documentScroller);
                const target = candidates.find(node => node && node.scrollHeight > node.clientHeight + 20);
                if (!target) return { target: '', deltaY: 0, debug: 'no scrollable target' };

                const isDocumentScroller = target === documentScroller
                    || target === document.documentElement
                    || target === document.body;
                const readTop = () => isDocumentScroller
                    ? (window.scrollY || document.documentElement.scrollTop || document.body.scrollTop || 0)
                    : target.scrollTop;
                const writeTop = value => {
                    if (isDocumentScroller) {
                        window.scrollTo(0, value);
                        document.documentElement.scrollTop = value;
                        document.body.scrollTop = value;
                    } else {
                        target.scrollTop = value;
                        target.dispatchEvent(new Event('scroll', { bubbles: true }));
                    }
                };

                const before = readTop();
                if (target === documentScroller) {
                    writeTop(before + deltaY);
                } else {
                    writeTop(before + deltaY);
                }

                await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
                const after = readTop();
                return {
                    target: isDocumentScroller ? 'document' : (target.getAttribute('data-testid') || target.className || target.tagName || ''),
                    deltaY: after - before,
                    debug: JSON.stringify({ before, after, scrollHeight: target.scrollHeight, clientHeight: target.clientHeight })
                };
            }
            """,
            new { hostSelector, deltaY });

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
        await WaitForEditorStableAsync(page, "initial editor ready", timeoutMs: 60000);
    }

    /// <summary>
    /// Waits for a local editor assertion point: host attached, visible blocks rendered, expected text visible when supplied,
    /// and no Blazor/runtime error UI. This intentionally does not wait for save/autosave.
    /// </summary>
    protected static async Task WaitForEditorStableAsync(
        IPage page,
        string reason,
        string? targetBlockId = null,
        string? expectedVisibleText = null,
        string hostSelector = DocumentEditorHostSelector,
        int timeoutMs = 5000)
    {
        var args = new
        {
            hostSelector,
            reason,
            targetBlockId = targetBlockId ?? string.Empty,
            expectedVisibleText = expectedVisibleText ?? string.Empty
        };

        try
        {
            await page.WaitForFunctionAsync(
                """
                async (args) => {
                    await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
                    const probe = buildProbe(args);
                    window.__tmDocumentEditorLastStableProbe = probe;
                    return probe.isStable === true;

                    function buildProbe(input) {
                        const host = document.querySelector(input.hostSelector);
                        const visibleBlocks = Array.from(host?.querySelectorAll('.tm-wysiwyg-block, [data-block-id], [data-render-block-id]') || [])
                            .filter(isVisible)
                            .filter(node => !node.closest('.tm-wysiwyg-page--virtual'));
                        const targetBlock = input.targetBlockId
                            ? visibleBlocks.find(node => node.getAttribute('data-block-id') === input.targetBlockId
                                || node.getAttribute('data-render-block-id') === input.targetBlockId)
                            : null;
                        const targetText = textOf(targetBlock);
                        const hostText = textOf(host);
                        const errorTexts = visibleErrorTexts();
                        const expectedFound = !input.expectedVisibleText
                            || targetText.includes(input.expectedVisibleText)
                            || hostText.includes(input.expectedVisibleText);
                        const activeElement = document.activeElement;
                        const probe = {
                            reason: input.reason || '',
                            isStable: !!host
                                && visibleBlocks.length > 0
                                && (!input.targetBlockId || !!targetBlock)
                                && expectedFound
                                && errorTexts.length === 0,
                            hostFound: !!host,
                            visibleBlockCount: visibleBlocks.length,
                            targetBlockFound: !input.targetBlockId || !!targetBlock,
                            expectedTextFound: expectedFound,
                            blazorErrorVisible: errorTexts.length > 0,
                            documentReadyState: document.readyState || '',
                            selectionText: window.getSelection?.()?.toString?.() || '',
                            activeElement: activeElement?.outerHTML?.slice(0, 500) || '',
                            targetText: targetText.slice(0, 500),
                            hostText: hostText.slice(0, 500),
                            debug: ''
                        };
                        probe.debug = JSON.stringify({
                            reason: probe.reason,
                            hostFound: probe.hostFound,
                            visibleBlockCount: probe.visibleBlockCount,
                            targetBlockId: input.targetBlockId,
                            targetBlockFound: probe.targetBlockFound,
                            expectedVisibleText: input.expectedVisibleText,
                            expectedTextFound: probe.expectedTextFound,
                            errorTexts,
                            documentReadyState: probe.documentReadyState,
                            selectionText: probe.selectionText,
                            activeElement: probe.activeElement,
                            targetText: probe.targetText
                        });
                        return probe;
                    }

                    function isVisible(node) {
                        const rect = node?.getBoundingClientRect?.();
                        const style = node ? getComputedStyle(node) : null;
                        return !!(rect && style && rect.width > 0.5 && rect.height > 0.5 && style.display !== 'none' && style.visibility !== 'hidden');
                    }

                    function textOf(node) {
                        return node ? (node.innerText || node.textContent || '') : '';
                    }

                    function visibleErrorTexts() {
                        const selectors = [
                            '#blazor-error-ui',
                            '.blazor-error-boundary',
                            '[data-testid="document-runtime-error"]',
                            '[data-testid="document-editor-runtime-message"]'
                        ];
                        return selectors
                            .flatMap(selector => Array.from(document.querySelectorAll(selector)))
                            .filter(node => {
                                if (!isVisible(node)) return false;
                                const text = textOf(node).trim();
                                if (!text) return false;
                                if (node.id === 'blazor-error-ui' && getComputedStyle(node).display === 'none') return false;
                                return /error|exception|failed|crash|unhandled|runtime/i.test(text);
                            })
                            .map(node => textOf(node).trim().slice(0, 500));
                    }
                }
                """,
                args,
                new() { Timeout = timeoutMs, PollingInterval = 50 });
        }
        catch (Exception ex) when (ex is TimeoutException || ex is PlaywrightException)
        {
            var probe = await ReadEditorStableProbeAsync(page, reason, targetBlockId, expectedVisibleText, hostSelector);
            throw new AssertFailedException($"Document editor did not become stable after '{reason}' within {timeoutMs}ms. Probe: {probe.Debug}", ex);
        }
    }

    /// <summary>Reads the last known local editor stability probe for timeout diagnostics.</summary>
    protected static Task<DocumentEditorStableProbe> ReadEditorStableProbeAsync(
        IPage page,
        string reason,
        string? targetBlockId = null,
        string? expectedVisibleText = null,
        string hostSelector = DocumentEditorHostSelector)
        => page.EvaluateAsync<DocumentEditorStableProbe>(
            """
            (args) => {
                const existing = window.__tmDocumentEditorLastStableProbe;
                if (existing && existing.reason === args.reason) return existing;
                const host = document.querySelector(args.hostSelector);
                const blocks = Array.from(host?.querySelectorAll('.tm-wysiwyg-block, [data-block-id], [data-render-block-id]') || [])
                    .filter(node => {
                        const rect = node.getBoundingClientRect();
                        const style = getComputedStyle(node);
                        return rect.width > 0.5 && rect.height > 0.5 && style.display !== 'none' && style.visibility !== 'hidden' && !node.closest('.tm-wysiwyg-page--virtual');
                    });
                const target = args.targetBlockId
                    ? blocks.find(node => node.getAttribute('data-block-id') === args.targetBlockId || node.getAttribute('data-render-block-id') === args.targetBlockId)
                    : null;
                const targetText = target ? (target.innerText || target.textContent || '') : '';
                const hostText = host ? (host.innerText || host.textContent || '') : '';
                const errorText = Array.from(document.querySelectorAll('#blazor-error-ui, .blazor-error-boundary, [data-testid="document-runtime-error"], [data-testid="document-editor-runtime-message"]'))
                    .map(node => (node.innerText || node.textContent || '').trim())
                    .filter(Boolean);
                const expectedFound = !args.expectedVisibleText || targetText.includes(args.expectedVisibleText) || hostText.includes(args.expectedVisibleText);
                const probe = {
                    reason: args.reason || '',
                    isStable: !!host && blocks.length > 0 && (!args.targetBlockId || !!target) && expectedFound && errorText.length === 0,
                    hostFound: !!host,
                    visibleBlockCount: blocks.length,
                    targetBlockFound: !args.targetBlockId || !!target,
                    expectedTextFound: expectedFound,
                    blazorErrorVisible: errorText.length > 0,
                    documentReadyState: document.readyState || '',
                    selectionText: window.getSelection?.()?.toString?.() || '',
                    activeElement: document.activeElement?.outerHTML?.slice(0, 500) || '',
                    targetText: targetText.slice(0, 500),
                    hostText: hostText.slice(0, 500),
                    debug: ''
                };
                probe.debug = JSON.stringify({ ...probe, errorText });
                return probe;
            }
            """,
            new { hostSelector, reason, targetBlockId = targetBlockId ?? string.Empty, expectedVisibleText = expectedVisibleText ?? string.Empty });

    /// <summary>Returns the visible contenteditable body from the first non-virtual page.</summary>
    protected static async Task<ILocator> WaitForWysiwygBodyAsync(IPage page)
    {
        var host = page.Locator("[data-testid='document-wysiwyg-host']");
        await Assertions.Expect(host).ToBeVisibleAsync();
        var body = host.Locator(".tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body[contenteditable]").First;
        await body.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60000 });
        return body;
    }

    /// <summary>Captures screenshot plus JSON diagnostics for a human-facing document editor failure.</summary>
    protected async Task<string> CaptureDocumentEditorDiagnosticArtifactAsync(
        IPage page,
        string behavior,
        string? targetBlockId = null,
        DocumentEditorConsoleCapture? console = null,
        string hostSelector = DocumentEditorHostSelector)
    {
        var safe = SanitizeFileName($"document_editor_diagnostic_{behavior}");
        var screenshotPath = await CaptureDocumentEditorPageScreenshotAsync(page, safe, hostSelector);
        var selection = await ReadDocumentEditorSelectionSnapshotAsync(page, hostSelector);
        var ribbonState = await ReadRibbonFormattingStateAsync(page);
        var floatingState = await ReadFloatingFormattingStateAsync(page);
        var browser = await page.EvaluateAsync<DocumentEditorBrowserDiagnosticArtifact>(
            """
            ({ hostSelector, targetBlockId }) => {
                const host = document.querySelector(hostSelector);
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const targetBlock = targetBlockId
                    ? Array.from(host?.querySelectorAll(`[data-block-id="${CSS.escape(targetBlockId)}"], [data-render-block-id="${CSS.escape(targetBlockId)}"]`) || []).find(isVisible)
                    : null;
                const runtimeSnapshot = getRuntimeSnapshot(instanceId);
                return {
                    instanceId,
                    documentText: host?.innerText || host?.textContent || '',
                    activeElement: document.activeElement?.outerHTML?.slice(0, 800) || '',
                    toolbarHtml: document.querySelector('[data-testid="document-toolbar"]')?.outerHTML?.slice(0, 4000) || '',
                    floatingToolbarHtml: document.querySelector('[data-testid="document-mini-toolbar"]')?.outerHTML?.slice(0, 2500) || '',
                    targetBlockHtml: targetBlock?.outerHTML?.slice(0, 4000) || '',
                    runtimeSnapshotJson: safeJson(runtimeSnapshot),
                    runtimeStateJson: safeJson(window.tmDocumentEditorDebug?.getRuntimeState?.(instanceId) || null),
                    formattingStateJson: safeJson(
                        window.tmDocumentEditorRuntime?.getFormattingState?.(instanceId)
                        || window.tmDocumentEditorEngine?.getFormattingState?.(instanceId)
                        || null),
                    undoStackJson: safeJson(
                        window.tmDocumentWysiwygDebug?.getUndoStack?.(instanceId)
                        || runtimeSnapshot?.undoStack
                        || runtimeSnapshot?.UndoStack
                        || null)
                };

                function getRuntimeSnapshot(id) {
                    try {
                        return window.tmDocumentEditorRuntime?.getDebugSnapshot?.(id)
                            || window.tmDocumentEditorEngine?.getDebugSnapshot?.(id)
                            || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function isVisible(node) {
                    const rect = node?.getBoundingClientRect();
                    const style = node ? getComputedStyle(node) : null;
                    return !!(rect && style && rect.width > 0.5 && rect.height > 0.5 && style.display !== 'none' && style.visibility !== 'hidden');
                }

                function safeJson(value) {
                    try { return JSON.stringify(value ?? null); }
                    catch (error) { return JSON.stringify({ error: String(error) }); }
                }
            }
            """,
            new { hostSelector, targetBlockId = targetBlockId ?? string.Empty });

        var directory = TestContext.TestResultsDirectory ?? ".";
        Directory.CreateDirectory(directory);
        var artifactPath = Path.Combine(directory, $"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        var payload = new
        {
            behavior,
            capturedAt = DateTimeOffset.Now,
            screenshotPath,
            selection,
            ribbonState,
            floatingState,
            consoleEntries = console?.Entries.ToArray() ?? [],
            fatalConsoleErrors = console?.FatalErrors.ToArray() ?? [],
            browser
        };
        await File.WriteAllTextAsync(artifactPath, JsonSerializer.Serialize(payload, StrictJsonOptions));
        TestContext.AddResultFile(artifactPath);
        return artifactPath;
    }

    private static Task<DocumentEditorTextSelectionMouseTarget> ReadTextSelectionMouseTargetAsync(
        IPage page,
        string? blockId,
        string text,
        string hostSelector)
        => page.EvaluateAsync<DocumentEditorTextSelectionMouseTarget>(
            """
            async ({ hostSelector, blockId, text }) => {
                const host = document.querySelector(hostSelector);
                const block = blockId ? visibleBlock(host, blockId) : visibleBlocks(host).find(candidate => (candidate.textContent || '').includes(text));
                if (!block) throw new Error(blockId ? `Could not find visible block '${blockId}'.` : `Could not find visible block containing '${text}'.`);
                await scrollIntoMouseViewport(block);
                const entries = collectTextEntries(block);
                const blockText = entries.map(entry => entry.text).join('');
                const start = blockText.indexOf(text);
                if (start < 0) throw new Error(`Text '${text}' was not found in block text '${blockText}'.`);
                return createTarget(block, entries, start, start + text.length, text);

                function visibleBlocks(root) {
                    return Array.from(root?.querySelectorAll('[data-block-id], [data-render-block-id]') || [])
                        .filter(node => {
                            const rect = node.getBoundingClientRect();
                            const style = getComputedStyle(node);
                            return rect.width > 1 && rect.height > 1 && style.display !== 'none' && style.visibility !== 'hidden' && !node.closest('.tm-wysiwyg-page--virtual');
                        });
                }

                function visibleBlock(root, id) {
                    const escaped = CSS.escape(id);
                    return visibleBlocks(root)
                        .find(node => node.getAttribute('data-block-id') === id || node.getAttribute('data-render-block-id') === id || node.matches(`[data-block-id="${escaped}"], [data-render-block-id="${escaped}"]`));
                }

                function collectTextEntries(root) {
                    const entries = [];
                    let offset = 0;
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            return node.nodeValue && node.nodeValue.length > 0 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                        }
                    });
                    while (walker.nextNode()) {
                        const node = walker.currentNode;
                        const value = node.nodeValue || '';
                        entries.push({ node, text: value, start: offset, end: offset + value.length });
                        offset += value.length;
                    }
                    return entries;
                }

                function createTarget(block, entries, start, end, expectedText) {
                    const first = positionAt(entries, start);
                    const last = positionAt(entries, Math.max(start, end - 1));
                    const range = document.createRange();
                    const endPosition = positionAt(entries, end);
                    range.setStart(first.node, first.offset);
                    range.setEnd(endPosition.node, endPosition.offset);
                    const startRect = rectFor(first.node, first.offset, 1, block);
                    const endRect = rectFor(last.node, last.offset, 1, block);
                    const rect = unionRects(Array.from(range.getClientRects()).filter(item => item.width > 0.5 && item.height > 0.5));
                    const blockIdValue = block.getAttribute('data-block-id') || block.getAttribute('data-render-block-id') || '';
                    const selectedEntries = entries.filter(entry => entry.end > start && entry.start < end);
                    return {
                        blockId: blockIdValue,
                        startOffset: start,
                        endOffset: end,
                        expectedText,
                        startX: startRect.left + 1,
                        startY: pointerY(startRect),
                        endX: endRect.right - 1,
                        endY: pointerY(endRect),
                        rect,
                        blockText: entries.map(entry => entry.text).join(''),
                        textNodeCount: selectedEntries.length,
                        debug: JSON.stringify({ blockId: blockIdValue, start, end, expectedText, rect, selectedEntries: selectedEntries.map(entry => ({ text: entry.text, start: entry.start, end: entry.end })) })
                    };
                }

                function positionAt(entries, offset) {
                    for (const entry of entries) {
                        if (offset <= entry.end) {
                            return { node: entry.node, offset: Math.max(0, Math.min(entry.node.nodeValue.length, offset - entry.start)) };
                        }
                    }
                    const last = entries[entries.length - 1];
                    if (!last) throw new Error('No text nodes were available for selection target.');
                    return { node: last.node, offset: last.node.nodeValue.length };
                }

                function rectFor(node, offset, length, fallback) {
                    const range = document.createRange();
                    const start = Math.max(0, Math.min(node.nodeValue.length, offset));
                    const end = Math.max(start + 1, Math.min(node.nodeValue.length, start + Math.max(1, length)));
                    range.setStart(node, start);
                    range.setEnd(node, Math.min(node.nodeValue.length, end));
                    return Array.from(range.getClientRects())[0] || fallback.getBoundingClientRect();
                }

                function pointerY(rect) {
                    return Math.max(rect.top + 1, Math.min(rect.bottom - 1, rect.top + Math.min(10, Math.max(4, rect.height * 0.38))));
                }

                function unionRects(rects) {
                    if (!rects.length) {
                        const fallback = block.getBoundingClientRect();
                        return { x: fallback.x, y: fallback.y, width: fallback.width, height: fallback.height };
                    }
                    const left = Math.min(...rects.map(rect => rect.left));
                    const top = Math.min(...rects.map(rect => rect.top));
                    const right = Math.max(...rects.map(rect => rect.right));
                    const bottom = Math.max(...rects.map(rect => rect.bottom));
                    return { x: left, y: top, width: right - left, height: bottom - top };
                }

                async function scrollIntoMouseViewport(node) {
                    node.scrollIntoView({ block: 'center', inline: 'nearest' });
                    await nextFrame();
                    for (const scroller of scrollableAncestors(node)) {
                        const rect = node.getBoundingClientRect();
                        const container = scroller === document.scrollingElement || scroller === document.documentElement || scroller === document.body
                            ? { top: 0, height: window.innerHeight }
                            : scroller.getBoundingClientRect();
                        const delta = rect.top - container.top - (container.height / 2) + (rect.height / 2);
                        if (Math.abs(delta) > 2) {
                            scroller.scrollTop += delta;
                            await nextFrame();
                        }
                    }
                    const rect = node.getBoundingClientRect();
                    if (rect.top < 80 || rect.bottom > window.innerHeight - 40) {
                        window.scrollBy({ top: rect.top - (window.innerHeight / 2) + (rect.height / 2), behavior: 'instant' });
                        await nextFrame();
                    }
                }

                function scrollableAncestors(node) {
                    const result = [];
                    for (let current = node.parentElement; current; current = current.parentElement) {
                        const style = getComputedStyle(current);
                        const overflow = `${style.overflow} ${style.overflowY}`.toLowerCase();
                        if (/(auto|scroll|overlay)/.test(overflow) && current.scrollHeight > current.clientHeight + 1) {
                            result.push(current);
                        }
                    }
                    result.push(document.scrollingElement || document.documentElement);
                    return result;
                }

                function nextFrame() {
                    return new Promise(resolve => requestAnimationFrame(() => resolve()));
                }
            }
            """,
            new { hostSelector, blockId = blockId ?? string.Empty, text });

    private static Task<DocumentEditorTextSelectionMouseTarget> ReadTextSelectionMouseTargetAsync(
        IPage page,
        string blockId,
        int startOffset,
        int endOffset,
        string hostSelector)
        => page.EvaluateAsync<DocumentEditorTextSelectionMouseTarget>(
            """
            async ({ hostSelector, blockId, startOffset, endOffset }) => {
                const host = document.querySelector(hostSelector);
                const escaped = CSS.escape(blockId);
                const block = Array.from(host?.querySelectorAll(`[data-block-id="${escaped}"], [data-render-block-id="${escaped}"]`) || [])
                    .find(node => {
                        const rect = node.getBoundingClientRect();
                        const style = getComputedStyle(node);
                        return rect.width > 1 && rect.height > 1 && style.display !== 'none' && style.visibility !== 'hidden' && !node.closest('.tm-wysiwyg-page--virtual');
                    });
                if (!block) throw new Error(`Could not find visible block '${blockId}'.`);
                await scrollIntoMouseViewport(block);
                const entries = [];
                let text = '';
                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        return node.nodeValue && node.nodeValue.length > 0 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                    }
                });
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    const value = node.nodeValue || '';
                    entries.push({ node, text: value, start: text.length, end: text.length + value.length });
                    text += value;
                }
                const start = Math.max(0, Math.min(Number(startOffset) || 0, text.length));
                const end = Math.max(start, Math.min(Number(endOffset) || 0, text.length));
                const first = positionAt(start);
                const last = positionAt(Math.max(start, end - 1));
                const endPos = positionAt(end);
                const startRect = rectFor(first.node, first.offset, 1, block);
                const endRect = rectFor(last.node, last.offset, 1, block);
                const range = document.createRange();
                range.setStart(first.node, first.offset);
                range.setEnd(endPos.node, endPos.offset);
                const rect = unionRects(Array.from(range.getClientRects()).filter(item => item.width > 0.5 && item.height > 0.5));
                const selectedEntries = entries.filter(entry => entry.end > start && entry.start < end);
                return {
                    blockId,
                    startOffset: start,
                    endOffset: end,
                    expectedText: text.slice(start, end),
                    startX: startRect.left + 1,
                    startY: pointerY(startRect),
                    endX: endRect.right - 1,
                    endY: pointerY(endRect),
                    rect,
                    blockText: text,
                    textNodeCount: selectedEntries.length,
                    debug: JSON.stringify({ blockId, start, end, expectedText: text.slice(start, end), rect })
                };

                function positionAt(offset) {
                    for (const entry of entries) {
                        if (offset <= entry.end) {
                            return { node: entry.node, offset: Math.max(0, Math.min(entry.node.nodeValue.length, offset - entry.start)) };
                        }
                    }
                    const last = entries[entries.length - 1];
                    if (!last) throw new Error('No text nodes were available for selection target.');
                    return { node: last.node, offset: last.node.nodeValue.length };
                }

                function rectFor(node, offset, length, fallback) {
                    const range = document.createRange();
                    const start = Math.max(0, Math.min(node.nodeValue.length, offset));
                    const end = Math.max(start + 1, Math.min(node.nodeValue.length, start + Math.max(1, length)));
                    range.setStart(node, start);
                    range.setEnd(node, Math.min(node.nodeValue.length, end));
                    return Array.from(range.getClientRects())[0] || fallback.getBoundingClientRect();
                }

                function pointerY(rect) {
                    return Math.max(rect.top + 1, Math.min(rect.bottom - 1, rect.top + Math.min(10, Math.max(4, rect.height * 0.38))));
                }

                function unionRects(rects) {
                    if (!rects.length) {
                        const fallback = block.getBoundingClientRect();
                        return { x: fallback.x, y: fallback.y, width: fallback.width, height: fallback.height };
                    }
                    const left = Math.min(...rects.map(rect => rect.left));
                    const top = Math.min(...rects.map(rect => rect.top));
                    const right = Math.max(...rects.map(rect => rect.right));
                    const bottom = Math.max(...rects.map(rect => rect.bottom));
                    return { x: left, y: top, width: right - left, height: bottom - top };
                }

                async function scrollIntoMouseViewport(node) {
                    node.scrollIntoView({ block: 'center', inline: 'nearest' });
                    await nextFrame();
                    for (const scroller of scrollableAncestors(node)) {
                        const rect = node.getBoundingClientRect();
                        const container = scroller === document.scrollingElement || scroller === document.documentElement || scroller === document.body
                            ? { top: 0, height: window.innerHeight }
                            : scroller.getBoundingClientRect();
                        const delta = rect.top - container.top - (container.height / 2) + (rect.height / 2);
                        if (Math.abs(delta) > 2) {
                            scroller.scrollTop += delta;
                            await nextFrame();
                        }
                    }
                    const rect = node.getBoundingClientRect();
                    if (rect.top < 80 || rect.bottom > window.innerHeight - 40) {
                        window.scrollBy({ top: rect.top - (window.innerHeight / 2) + (rect.height / 2), behavior: 'instant' });
                        await nextFrame();
                    }
                }

                function scrollableAncestors(node) {
                    const result = [];
                    for (let current = node.parentElement; current; current = current.parentElement) {
                        const style = getComputedStyle(current);
                        const overflow = `${style.overflow} ${style.overflowY}`.toLowerCase();
                        if (/(auto|scroll|overlay)/.test(overflow) && current.scrollHeight > current.clientHeight + 1) {
                            result.push(current);
                        }
                    }
                    result.push(document.scrollingElement || document.documentElement);
                    return result;
                }

                function nextFrame() {
                    return new Promise(resolve => requestAnimationFrame(() => resolve()));
                }
            }
            """,
            new { hostSelector, blockId, startOffset, endOffset });

    private static async Task<DocumentEditorSelectionSnapshot> WaitForTextSelectionAsync(
        IPage page,
        string blockId,
        string expectedText,
        DocumentEditorTextSelectionMouseTarget target,
        string hostSelector)
    {
        DocumentEditorSelectionSnapshot? last = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = await ReadDocumentEditorSelectionSnapshotAsync(page, hostSelector);
            last.TargetBlockId = blockId;
            last.ExpectedText = expectedText;
            last.TargetStartOffset = target.StartOffset;
            last.TargetEndOffset = target.EndOffset;
            if (!last.IsCollapsed
                && last.SelectedText.Contains(expectedText, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(blockId)
                    || string.Equals(last.StartBlockId, blockId, StringComparison.Ordinal)
                    || string.Equals(last.AnchorBlockId, blockId, StringComparison.Ordinal)
                    || string.Equals(last.FocusBlockId, blockId, StringComparison.Ordinal)))
            {
                return last;
            }

            await page.WaitForTimeoutAsync(75);
        }

        var actual = last is null ? "<no selection snapshot>" : last.Debug;
        throw new AssertFailedException($"Human mouse text selection failed. Expected '{expectedText}' in block '{blockId}'. Target: {target.Debug}. Actual selection: {actual}");
    }

    private static async Task<DocumentEditorToolbarActionResult> ClickToolbarElementWithPointerAsync(
        IPage page,
        ILocator locator,
        string name,
        string scope,
        DocumentEditorSelectionSnapshot? expectedSelection,
        bool requireRuntimeSelectionToken,
        string hostSelector,
        bool assertCommandStatePublished = true)
    {
        var beforeSelection = expectedSelection ?? await ReadDocumentEditorSelectionSnapshotAsync(page, hostSelector);
        if (expectedSelection is not null)
        {
            await AssertSelectionStillEqualsAsync(page, expectedSelection, hostSelector);
        }

        await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        var box = await locator.BoundingBoxAsync();
        if (box is null)
        {
            throw new AssertFailedException($"Could not click toolbar control '{name}' because it has no bounding box.");
        }

        var x = box.X + box.Width / 2;
        var y = box.Y + box.Height / 2;
        await page.Mouse.MoveAsync((float)x, (float)y);
        await page.Mouse.DownAsync();
        var pointerDownSelection = await ReadDocumentEditorSelectionSnapshotAsync(page, hostSelector);
        var pointerDownFailure = ValidatePointerDownSelection(beforeSelection, pointerDownSelection, name, requireRuntimeSelectionToken);
        await page.Mouse.UpAsync();
        await WaitForEditorStableAsync(page, $"toolbar {scope} '{name}'", beforeSelection.StartBlockId, beforeSelection.SelectedText, hostSelector);

        var afterSelection = await ReadDocumentEditorSelectionSnapshotAsync(page, hostSelector);
        var afterRibbonState = await ReadRibbonFormattingStateAsync(page);
        var afterFloatingState = await ReadFloatingFormattingStateAsync(page);
        var result = new DocumentEditorToolbarActionResult
        {
            Name = name,
            Scope = scope,
            BeforeSelection = beforeSelection,
            PointerDownSelection = pointerDownSelection,
            AfterSelection = afterSelection,
            AfterRibbonState = afterRibbonState,
            AfterFloatingState = afterFloatingState
        };

        if (pointerDownFailure is not null)
        {
            throw new AssertFailedException(pointerDownFailure);
        }

        if (assertCommandStatePublished && !afterRibbonState.HasRuntimeFormattingState)
        {
            throw new AssertFailedException($"Toolbar command '{name}' did not publish a runtime formatting state. Action: {JsonSerializer.Serialize(result, StrictJsonOptions)}");
        }

        return result;
    }

    private static string? ValidatePointerDownSelection(
        DocumentEditorSelectionSnapshot before,
        DocumentEditorSelectionSnapshot pointerDown,
        string actionName,
        bool requireRuntimeSelectionToken)
    {
        if (!string.IsNullOrWhiteSpace(before.SelectedText))
        {
            if (pointerDown.IsCollapsed || !pointerDown.SelectedText.Contains(before.SelectedText, StringComparison.Ordinal))
            {
                return $"Toolbar pointerdown for '{actionName}' destroyed the selected text. Before={DescribeSelectionForFailure(before)}; PointerDown={DescribeSelectionForFailure(pointerDown)}";
            }

            if (!string.Equals(pointerDown.StartBlockId, before.StartBlockId, StringComparison.Ordinal)
                || !string.Equals(pointerDown.EndBlockId, before.EndBlockId, StringComparison.Ordinal)
                || pointerDown.StartBlockOffset != before.StartBlockOffset
                || pointerDown.EndBlockOffset != before.EndBlockOffset)
            {
                return $"Toolbar pointerdown for '{actionName}' moved the selected range. Before={DescribeSelectionForFailure(before)}; PointerDown={DescribeSelectionForFailure(pointerDown)}";
            }
        }

        if (requireRuntimeSelectionToken && !pointerDown.HasRuntimeSelectionToken)
        {
            return $"Toolbar pointerdown for '{actionName}' did not preserve a runtime selection token. PointerDown={DescribeSelectionForFailure(pointerDown)}";
        }

        return null;
    }

    private static string DescribeSelectionForFailure(DocumentEditorSelectionSnapshot selection)
        => $"text='{selection.SelectedText}', collapsed={selection.IsCollapsed}, start={selection.StartBlockId}:{selection.StartBlockOffset}, end={selection.EndBlockId}:{selection.EndBlockOffset}, runtimeSelection={selection.HasRuntimeSelection}, runtimeToken={selection.HasRuntimeSelectionToken}";

    private static Task<DocumentEditorFormattingToolbarState> ReadFormattingToolbarStateAsync(IPage page, string scope)
        => page.EvaluateAsync<DocumentEditorFormattingToolbarState>(
            """
            (scope) => {
                const isFloating = scope === 'floating';
                const root = document.querySelector(isFloating ? '[data-testid="document-mini-toolbar"]' : '[data-testid="document-toolbar"]');
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const runtimeFormatting = getRuntimeFormatting(instanceId);
                const runtimeSelection = getRuntimeSelection(instanceId);
                const ids = isFloating
                    ? {
                        bold: 'document-mini-bold',
                        italic: 'document-mini-italic',
                        underline: 'document-mini-underline',
                        strike: 'document-mini-strikethrough',
                        fontFamily: '',
                        fontSize: 'document-mini-font-size',
                        textColor: 'document-mini-text-color',
                        highlight: 'document-mini-highlight'
                    }
                    : {
                        bold: 'document-bold',
                        italic: 'document-italic',
                        underline: 'document-underline',
                        strike: 'document-strikethrough',
                        fontFamily: 'document-font-family',
                        fontSize: 'document-font-size',
                        textColor: 'document-font-color-trigger',
                        highlight: 'document-highlight-color-trigger'
                    };
                const bold = buttonState(ids.bold);
                const italic = buttonState(ids.italic);
                const underline = buttonState(ids.underline);
                const strike = buttonState(ids.strike);
                const fontFamily = selectState(ids.fontFamily);
                const fontSize = selectState(ids.fontSize);
                const textColor = colorState(ids.textColor);
                const highlight = colorState(ids.highlight);
                const state = {
                    scope,
                    isVisible: isVisible(root),
                    bold: bold.active,
                    boldMixed: bold.mixed,
                    boldDisabled: bold.disabled,
                    italic: italic.active,
                    italicMixed: italic.mixed,
                    italicDisabled: italic.disabled,
                    underline: underline.active,
                    underlineMixed: underline.mixed,
                    underlineDisabled: underline.disabled,
                    strikethrough: strike.active,
                    strikethroughMixed: strike.mixed,
                    strikethroughDisabled: strike.disabled,
                    fontFamily: fontFamily.value,
                    fontFamilyMixed: fontFamily.mixed,
                    fontFamilyDisabled: fontFamily.disabled,
                    fontSize: fontSize.value,
                    fontSizeMixed: fontSize.mixed,
                    fontSizeDisabled: fontSize.disabled,
                    textColor: textColor.value,
                    textColorMixed: textColor.mixed,
                    textColorDisabled: textColor.disabled,
                    highlightColor: highlight.value,
                    highlightColorMixed: highlight.mixed,
                    highlightColorDisabled: highlight.disabled,
                    runtimeFormattingJson: safeJson(runtimeFormatting),
                    runtimeSelectionJson: safeJson(runtimeSelection),
                    hasRuntimeFormattingState: !!runtimeFormatting && Object.keys(runtimeFormatting).length > 0,
                    hasRuntimeSelection: !!runtimeSelection,
                    hasRuntimeSelectionToken: !!findSelectionToken(runtimeSelection),
                    debug: ''
                };
                state.debug = safeJson(state);
                return state;

                function byTestId(id) {
                    if (!id) return null;
                    const escaped = CSS.escape(id);
                    return root?.querySelector(`[data-testid="${escaped}"]`) || document.querySelector(`[data-testid="${escaped}"]`);
                }

                function buttonState(id) {
                    const node = byTestId(id);
                    const aria = node?.getAttribute('aria-pressed') || '';
                    const className = String(node?.className || '');
                    return {
                        active: aria === 'true' || className.includes('--active') || className.includes('is-active'),
                        mixed: aria === 'mixed' || className.includes('--mixed'),
                        disabled: !node || !!node.disabled || node.getAttribute('aria-disabled') === 'true'
                    };
                }

                function selectState(id) {
                    const node = byTestId(id);
                    return {
                        value: node?.value || '',
                        mixed: !node ? false : node.value === '' && Array.from(node.options || []).some(option => /mixed/i.test(option.textContent || '')),
                        disabled: !node || !!node.disabled || node.getAttribute('aria-disabled') === 'true'
                    };
                }

                function colorState(id) {
                    const node = byTestId(id);
                    const text = node?.querySelector('.tm-color-picker-trigger-text')?.textContent?.trim() || '';
                    const swatch = node?.querySelector('.tm-color-picker-trigger-color');
                    const color = normalizeColor(text) || normalizeColor(swatch ? getComputedStyle(swatch).backgroundColor : '');
                    const className = String(node?.className || '');
                    return {
                        value: color,
                        mixed: className.includes('--mixed'),
                        disabled: !node || node.classList.contains('tm-color-picker--disabled') || node.getAttribute('aria-disabled') === 'true'
                    };
                }

                function getRuntimeFormatting(id) {
                    try {
                        return window.tmDocumentEditorRuntime?.getFormattingState?.(id)
                            || window.tmDocumentEditorEngine?.getFormattingState?.(id)
                            || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function getRuntimeSelection(id) {
                    try {
                        return window.tmDocumentEditorRuntime?.getSelectionSnapshot?.(id)
                            || window.tmDocumentEditorEngine?.getSelectionSnapshot?.(id)
                            || null;
                    } catch (error) {
                        return { error: String(error) };
                    }
                }

                function findSelectionToken(value, depth = 0) {
                    if (!value || typeof value !== 'object' || depth > 3) return '';
                    for (const key of ['selectionToken', 'SelectionToken', 'stableSelectionToken', 'StableSelectionToken', 'token', 'Token']) {
                        const candidate = value[key];
                        if (typeof candidate === 'string' && candidate.trim()) return candidate;
                    }
                    for (const key of ['selection', 'Selection', 'currentSelection', 'CurrentSelection', 'lastSelection', 'LastSelection']) {
                        const nested = findSelectionToken(value[key], depth + 1);
                        if (nested) return nested;
                    }
                    return '';
                }

                function normalizeColor(value) {
                    if (!value) return '';
                    const text = String(value).trim().toLowerCase();
                    if (/^#[0-9a-f]{6}$/i.test(text)) return text;
                    const match = text.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([.\d]+))?\)$/i);
                    if (!match || match[4] === '0') return '';
                    return '#' + [match[1], match[2], match[3]]
                        .map(part => Math.max(0, Math.min(255, parseInt(part, 10))).toString(16).padStart(2, '0'))
                        .join('');
                }

                function isVisible(node) {
                    const rect = node?.getBoundingClientRect();
                    const style = node ? getComputedStyle(node) : null;
                    return !!(rect && style && rect.width > 0.5 && rect.height > 0.5 && style.display !== 'none' && style.visibility !== 'hidden');
                }

                function safeJson(value) {
                    try { return JSON.stringify(value ?? null); }
                    catch (error) { return JSON.stringify({ error: String(error) }); }
                }
            }
            """,
            scope);

    private static void AssertStyleArraysEquivalent(
        DocumentEditorCssStyleEntry[] expected,
        DocumentEditorCssStyleEntry[] actual,
        string label,
        DocumentEditorTextRunComputedStyleProbe before,
        DocumentEditorTextRunComputedStyleProbe after)
    {
        var expectedSignature = StyleSignature(expected);
        var actualSignature = StyleSignature(actual);
        Assert.AreEqual(expectedSignature, actualSignature, $"Expected surrounding {label} styles to remain unchanged. Before={before.Debug}; After={after.Debug}");
    }

    private static string StyleSignature(IEnumerable<DocumentEditorCssStyleEntry> styles)
        => string.Join("|", styles.Select(style => string.Join(":", style.Bold, style.Italic, style.Underline, style.Strikethrough, NormalizeFontSizeToken(style.FontSize), NormalizeCssColor(style.ColorHex), NormalizeCssColor(style.BackgroundColorHex), style.FontFamily)));

    private static string NormalizeFontSizeToken(string value)
    {
        var cleaned = value.Trim().Replace("pt", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("px", string.Empty, StringComparison.OrdinalIgnoreCase);
        return double.TryParse(cleaned, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            : value.Trim();
    }

    private static void AssertCssColorEquals(string actual, string expected, string message)
    {
        if (!CssColorMatches(actual, expected))
        {
            throw new AssertFailedException($"{message} Actual color '{actual}' did not match expected '{expected}'.");
        }
    }

    protected static bool CssColorMatches(string actual, string expected)
    {
        var actualHex = NormalizeCssColor(actual);
        var expectedHex = NormalizeCssColor(expected);
        return !string.IsNullOrWhiteSpace(actualHex)
            && !string.IsNullOrWhiteSpace(expectedHex)
            && string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCssColor(string value)
    {
        var text = value.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text) || text is "transparent" or "rgba(0, 0, 0, 0)")
        {
            return string.Empty;
        }

        if (text.Length == 7 && text[0] == '#' && text.Skip(1).All(Uri.IsHexDigit))
        {
            return text;
        }

        var numbers = System.Text.RegularExpressions.Regex.Matches(text, @"\d+")
            .Select(match => int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        return numbers.Length >= 3
            ? $"#{numbers[0]:x2}{numbers[1]:x2}{numbers[2]:x2}"
            : text;
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

/// <summary>Local editor stability probe used by E2E waits that must not depend on autosave.</summary>
public sealed class DocumentEditorStableProbe
{
    [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
    [JsonPropertyName("isStable")] public bool IsStable { get; set; }
    [JsonPropertyName("hostFound")] public bool HostFound { get; set; }
    [JsonPropertyName("visibleBlockCount")] public int VisibleBlockCount { get; set; }
    [JsonPropertyName("targetBlockFound")] public bool TargetBlockFound { get; set; }
    [JsonPropertyName("expectedTextFound")] public bool ExpectedTextFound { get; set; }
    [JsonPropertyName("blazorErrorVisible")] public bool BlazorErrorVisible { get; set; }
    [JsonPropertyName("documentReadyState")] public string DocumentReadyState { get; set; } = string.Empty;
    [JsonPropertyName("selectionText")] public string SelectionText { get; set; } = string.Empty;
    [JsonPropertyName("activeElement")] public string ActiveElement { get; set; } = string.Empty;
    [JsonPropertyName("targetText")] public string TargetText { get; set; } = string.Empty;
    [JsonPropertyName("hostText")] public string HostText { get; set; } = string.Empty;
    [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
}

/// <summary>Strict human-selection snapshot captured from native selection and runtime diagnostics.</summary>
public sealed class DocumentEditorSelectionSnapshot
{
    [JsonPropertyName("targetBlockId")] public string TargetBlockId { get; set; } = string.Empty;
    [JsonPropertyName("expectedText")] public string ExpectedText { get; set; } = string.Empty;
    [JsonPropertyName("targetStartOffset")] public int TargetStartOffset { get; set; }
    [JsonPropertyName("targetEndOffset")] public int TargetEndOffset { get; set; }
    [JsonPropertyName("selectedText")] public string SelectedText { get; set; } = string.Empty;
    [JsonPropertyName("isCollapsed")] public bool IsCollapsed { get; set; }
    [JsonPropertyName("anchorBlockId")] public string AnchorBlockId { get; set; } = string.Empty;
    [JsonPropertyName("anchorInlineId")] public string AnchorInlineId { get; set; } = string.Empty;
    [JsonPropertyName("anchorBlockOffset")] public int AnchorBlockOffset { get; set; }
    [JsonPropertyName("focusBlockId")] public string FocusBlockId { get; set; } = string.Empty;
    [JsonPropertyName("focusInlineId")] public string FocusInlineId { get; set; } = string.Empty;
    [JsonPropertyName("focusBlockOffset")] public int FocusBlockOffset { get; set; }
    [JsonPropertyName("startBlockId")] public string StartBlockId { get; set; } = string.Empty;
    [JsonPropertyName("startInlineId")] public string StartInlineId { get; set; } = string.Empty;
    [JsonPropertyName("startBlockOffset")] public int StartBlockOffset { get; set; }
    [JsonPropertyName("endBlockId")] public string EndBlockId { get; set; } = string.Empty;
    [JsonPropertyName("endInlineId")] public string EndInlineId { get; set; } = string.Empty;
    [JsonPropertyName("endBlockOffset")] public int EndBlockOffset { get; set; }
    [JsonPropertyName("rect")] public DocumentEditorRectProbe Rect { get; set; } = new();
    [JsonPropertyName("runtimeSelectionJson")] public string RuntimeSelectionJson { get; set; } = "null";
    [JsonPropertyName("runtimeSelectionToken")] public string RuntimeSelectionToken { get; set; } = string.Empty;
    [JsonPropertyName("hasRuntimeSelection")] public bool HasRuntimeSelection { get; set; }
    [JsonPropertyName("hasRuntimeSelectionToken")] public bool HasRuntimeSelectionToken { get; set; }
    [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
}

/// <summary>Mouse target used to select a visible document text range.</summary>
public sealed class DocumentEditorTextSelectionMouseTarget
{
    [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
    [JsonPropertyName("startOffset")] public int StartOffset { get; set; }
    [JsonPropertyName("endOffset")] public int EndOffset { get; set; }
    [JsonPropertyName("expectedText")] public string ExpectedText { get; set; } = string.Empty;
    [JsonPropertyName("startX")] public double StartX { get; set; }
    [JsonPropertyName("startY")] public double StartY { get; set; }
    [JsonPropertyName("endX")] public double EndX { get; set; }
    [JsonPropertyName("endY")] public double EndY { get; set; }
    [JsonPropertyName("rect")] public DocumentEditorRectProbe Rect { get; set; } = new();
    [JsonPropertyName("blockText")] public string BlockText { get; set; } = string.Empty;
    [JsonPropertyName("textNodeCount")] public int TextNodeCount { get; set; }
    [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
}

/// <summary>Result of a strict toolbar pointer action.</summary>
public sealed class DocumentEditorToolbarActionResult
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("scope")] public string Scope { get; set; } = string.Empty;
    [JsonPropertyName("beforeSelection")] public DocumentEditorSelectionSnapshot BeforeSelection { get; set; } = new();
    [JsonPropertyName("pointerDownSelection")] public DocumentEditorSelectionSnapshot PointerDownSelection { get; set; } = new();
    [JsonPropertyName("afterSelection")] public DocumentEditorSelectionSnapshot AfterSelection { get; set; } = new();
    [JsonPropertyName("afterRibbonState")] public DocumentEditorFormattingToolbarState AfterRibbonState { get; set; } = new();
    [JsonPropertyName("afterFloatingState")] public DocumentEditorFormattingToolbarState AfterFloatingState { get; set; } = new();
}

/// <summary>Scroll result returned by document editor viewport probes.</summary>
public sealed class DocumentEditorScrollProbe
{
    [JsonPropertyName("target")] public string Target { get; set; } = string.Empty;
    [JsonPropertyName("deltaY")] public double DeltaY { get; set; }
    [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
}

/// <summary>Computed styles for a visible text range and its surrounding text.</summary>
public sealed class DocumentEditorTextRunComputedStyleProbe
{
    [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    [JsonPropertyName("blockText")] public string BlockText { get; set; } = string.Empty;
    [JsonPropertyName("startOffset")] public int StartOffset { get; set; }
    [JsonPropertyName("endOffset")] public int EndOffset { get; set; }
    [JsonPropertyName("nodeCount")] public int NodeCount { get; set; }
    [JsonPropertyName("targetStyles")] public DocumentEditorCssStyleEntry[] TargetStyles { get; set; } = [];
    [JsonPropertyName("beforeStyles")] public DocumentEditorCssStyleEntry[] BeforeStyles { get; set; } = [];
    [JsonPropertyName("afterStyles")] public DocumentEditorCssStyleEntry[] AfterStyles { get; set; } = [];
    [JsonPropertyName("hasMixedFormatting")] public bool HasMixedFormatting { get; set; }
    [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
}

/// <summary>Computed style values for one rendered text node/inline run.</summary>
public sealed class DocumentEditorCssStyleEntry
{
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    [JsonPropertyName("startOffset")] public int StartOffset { get; set; }
    [JsonPropertyName("endOffset")] public int EndOffset { get; set; }
    [JsonPropertyName("inlineId")] public string InlineId { get; set; } = string.Empty;
    [JsonPropertyName("parentHtml")] public string ParentHtml { get; set; } = string.Empty;
    [JsonPropertyName("fontWeight")] public string FontWeight { get; set; } = string.Empty;
    [JsonPropertyName("bold")] public bool Bold { get; set; }
    [JsonPropertyName("fontStyle")] public string FontStyle { get; set; } = string.Empty;
    [JsonPropertyName("italic")] public bool Italic { get; set; }
    [JsonPropertyName("textDecorationLine")] public string TextDecorationLine { get; set; } = string.Empty;
    [JsonPropertyName("underline")] public bool Underline { get; set; }
    [JsonPropertyName("strikethrough")] public bool Strikethrough { get; set; }
    [JsonPropertyName("fontSize")] public string FontSize { get; set; } = string.Empty;
    [JsonPropertyName("fontSizePx")] public double FontSizePx { get; set; }
    [JsonPropertyName("fontSizePt")] public double FontSizePt { get; set; }
    [JsonPropertyName("fontFamily")] public string FontFamily { get; set; } = string.Empty;
    [JsonPropertyName("color")] public string Color { get; set; } = string.Empty;
    [JsonPropertyName("colorHex")] public string ColorHex { get; set; } = string.Empty;
    [JsonPropertyName("backgroundColor")] public string BackgroundColor { get; set; } = string.Empty;
    [JsonPropertyName("backgroundColorHex")] public string BackgroundColorHex { get; set; } = string.Empty;
}

/// <summary>Visible formatting state from a ribbon or floating toolbar.</summary>
public sealed class DocumentEditorFormattingToolbarState
{
    [JsonPropertyName("scope")] public string Scope { get; set; } = string.Empty;
    [JsonPropertyName("isVisible")] public bool IsVisible { get; set; }
    [JsonPropertyName("bold")] public bool Bold { get; set; }
    [JsonPropertyName("boldMixed")] public bool BoldMixed { get; set; }
    [JsonPropertyName("boldDisabled")] public bool BoldDisabled { get; set; }
    [JsonPropertyName("italic")] public bool Italic { get; set; }
    [JsonPropertyName("italicMixed")] public bool ItalicMixed { get; set; }
    [JsonPropertyName("italicDisabled")] public bool ItalicDisabled { get; set; }
    [JsonPropertyName("underline")] public bool Underline { get; set; }
    [JsonPropertyName("underlineMixed")] public bool UnderlineMixed { get; set; }
    [JsonPropertyName("underlineDisabled")] public bool UnderlineDisabled { get; set; }
    [JsonPropertyName("strikethrough")] public bool Strikethrough { get; set; }
    [JsonPropertyName("strikethroughMixed")] public bool StrikethroughMixed { get; set; }
    [JsonPropertyName("strikethroughDisabled")] public bool StrikethroughDisabled { get; set; }
    [JsonPropertyName("fontFamily")] public string FontFamily { get; set; } = string.Empty;
    [JsonPropertyName("fontFamilyMixed")] public bool FontFamilyMixed { get; set; }
    [JsonPropertyName("fontFamilyDisabled")] public bool FontFamilyDisabled { get; set; }
    [JsonPropertyName("fontSize")] public string FontSize { get; set; } = string.Empty;
    [JsonPropertyName("fontSizeMixed")] public bool FontSizeMixed { get; set; }
    [JsonPropertyName("fontSizeDisabled")] public bool FontSizeDisabled { get; set; }
    [JsonPropertyName("textColor")] public string TextColor { get; set; } = string.Empty;
    [JsonPropertyName("textColorMixed")] public bool TextColorMixed { get; set; }
    [JsonPropertyName("textColorDisabled")] public bool TextColorDisabled { get; set; }
    [JsonPropertyName("highlightColor")] public string HighlightColor { get; set; } = string.Empty;
    [JsonPropertyName("highlightColorMixed")] public bool HighlightColorMixed { get; set; }
    [JsonPropertyName("highlightColorDisabled")] public bool HighlightColorDisabled { get; set; }
    [JsonPropertyName("runtimeFormattingJson")] public string RuntimeFormattingJson { get; set; } = "null";
    [JsonPropertyName("runtimeSelectionJson")] public string RuntimeSelectionJson { get; set; } = "null";
    [JsonPropertyName("hasRuntimeFormattingState")] public bool HasRuntimeFormattingState { get; set; }
    [JsonPropertyName("hasRuntimeSelection")] public bool HasRuntimeSelection { get; set; }
    [JsonPropertyName("hasRuntimeSelectionToken")] public bool HasRuntimeSelectionToken { get; set; }
    [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
}

/// <summary>Browser-side diagnostic payload attached to strict document editor failures.</summary>
public sealed class DocumentEditorBrowserDiagnosticArtifact
{
    [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
    [JsonPropertyName("documentText")] public string DocumentText { get; set; } = string.Empty;
    [JsonPropertyName("activeElement")] public string ActiveElement { get; set; } = string.Empty;
    [JsonPropertyName("toolbarHtml")] public string ToolbarHtml { get; set; } = string.Empty;
    [JsonPropertyName("floatingToolbarHtml")] public string FloatingToolbarHtml { get; set; } = string.Empty;
    [JsonPropertyName("targetBlockHtml")] public string TargetBlockHtml { get; set; } = string.Empty;
    [JsonPropertyName("runtimeSnapshotJson")] public string RuntimeSnapshotJson { get; set; } = "null";
    [JsonPropertyName("runtimeStateJson")] public string RuntimeStateJson { get; set; } = "null";
    [JsonPropertyName("formattingStateJson")] public string FormattingStateJson { get; set; } = "null";
    [JsonPropertyName("undoStackJson")] public string UndoStackJson { get; set; } = "null";
}

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

/// <summary>Image/caret diagnostics captured from the document editor runtime and visible DOM.</summary>
public sealed class DocumentEditorImageDiagnosticsProbe
{
    [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
    [JsonPropertyName("selectionMode")] public string SelectionMode { get; set; } = string.Empty;
    [JsonPropertyName("activeImageId")] public string ActiveImageId { get; set; } = string.Empty;
    [JsonPropertyName("caretBlockId")] public string CaretBlockId { get; set; } = string.Empty;
    [JsonPropertyName("caretOffset")] public int CaretOffset { get; set; }
    [JsonPropertyName("caretRect")] public DocumentEditorRectProbe CaretRect { get; set; } = new();
    [JsonPropertyName("anchorBlockId")] public string AnchorBlockId { get; set; } = string.Empty;
    [JsonPropertyName("anchorOffset")] public int AnchorOffset { get; set; }
    [JsonPropertyName("topLevelImageBlockCount")] public int TopLevelImageBlockCount { get; set; }
    [JsonPropertyName("drawingRunCount")] public int DrawingRunCount { get; set; }
    [JsonPropertyName("imageRect")] public DocumentEditorRectProbe ImageRect { get; set; } = new();
    [JsonPropertyName("lineIntervals")] public DocumentEditorLineIntervalProbe[] LineIntervals { get; set; } = [];
    [JsonPropertyName("hostHasFocus")] public bool HostHasFocus { get; set; }
    [JsonPropertyName("imageToolbarVisible")] public bool ImageToolbarVisible { get; set; }
    [JsonPropertyName("runtimeSelectionJson")] public string RuntimeSelectionJson { get; set; } = "null";
    [JsonPropertyName("documentModelJson")] public string DocumentModelJson { get; set; } = "null";
    [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
}

/// <summary>Drawing run diagnostic read from the runtime document model.</summary>
public sealed class DocumentEditorDrawingRunProbe
{
    [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
    [JsonPropertyName("objectId")] public string ObjectId { get; set; } = string.Empty;
    [JsonPropertyName("runId")] public string RunId { get; set; } = string.Empty;
    [JsonPropertyName("anchorBlockId")] public string AnchorBlockId { get; set; } = string.Empty;
    [JsonPropertyName("anchorOffset")] public int AnchorOffset { get; set; }
    [JsonPropertyName("inlineIndex")] public int InlineIndex { get; set; }
    [JsonPropertyName("altText")] public string AltText { get; set; } = string.Empty;
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("region")] public string Region { get; set; } = string.Empty;
    [JsonPropertyName("tableId")] public string TableId { get; set; } = string.Empty;
    [JsonPropertyName("cellId")] public string CellId { get; set; } = string.Empty;
    [JsonPropertyName("headerFooterId")] public string HeaderFooterId { get; set; } = string.Empty;
    [JsonPropertyName("wrapMode")] public string WrapMode { get; set; } = string.Empty;
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
    [JsonPropertyName("cropLeft")] public double CropLeft { get; set; }
    [JsonPropertyName("cropTop")] public double CropTop { get; set; }
    [JsonPropertyName("cropRight")] public double CropRight { get; set; }
    [JsonPropertyName("cropBottom")] public double CropBottom { get; set; }
}

/// <summary>Collapsed caret diagnostic for image parity tests.</summary>
public sealed class DocumentEditorCaretProbe
{
    [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
    [JsonPropertyName("offset")] public int Offset { get; set; }
    [JsonPropertyName("rect")] public DocumentEditorRectProbe Rect { get; set; } = new();
}

/// <summary>Image anchor diagnostic read from the runtime document model.</summary>
public sealed class DocumentEditorImageAnchorProbe
{
    [JsonPropertyName("imageId")] public string ImageId { get; set; } = string.Empty;
    [JsonPropertyName("anchorBlockId")] public string AnchorBlockId { get; set; } = string.Empty;
    [JsonPropertyName("anchorOffset")] public int AnchorOffset { get; set; }
}

/// <summary>Visible text interval near an image object.</summary>
public sealed class DocumentEditorLineIntervalProbe
{
    [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
    [JsonPropertyName("leftAvailable")] public double LeftAvailable { get; set; }
    [JsonPropertyName("rightAvailable")] public double RightAvailable { get; set; }
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
        => entry.Contains("favicon", StringComparison.OrdinalIgnoreCase)
            || (entry.Contains("/hubs/document-editor-collaboration/negotiate", StringComparison.OrdinalIgnoreCase)
                && entry.Contains("net::ERR_ABORTED", StringComparison.OrdinalIgnoreCase));

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
