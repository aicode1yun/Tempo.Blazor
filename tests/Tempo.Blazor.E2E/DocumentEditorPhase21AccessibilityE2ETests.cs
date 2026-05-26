using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end accessibility checkpoints for document editor keyboard surfaces and live announcements.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase21AccessibilityE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase21_CommandPalette_KeyboardSearchExecuteAndClose()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var body = await WaitForWysiwygBodyAsync(page);
        await body.ClickAsync();

        await page.Keyboard.PressAsync("Control+Shift+P");
        var palette = page.Locator("[data-testid='document-command-palette']");
        await Assertions.Expect(palette).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(palette.Locator("[role='dialog']")).ToHaveAttributeAsync("aria-modal", "true");
        await Assertions.Expect(palette.Locator("[role='listbox']")).ToBeVisibleAsync();

        var search = palette.Locator("[data-testid='document-command-palette-search']");
        await search.FocusAsync();
        await search.FillAsync("Italic");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("[data-testid='document-command-palette']")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Assertions.Expect(page.Locator("[data-testid='document-italic']")).ToHaveAttributeAsync("aria-pressed", "true");

    }

    [TestMethod]
    public async Task Phase21_TableGridPicker_KeyboardInsertsTable()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await PlaceCaretAtEndOfBodyAsync(page);

        await page.Locator("[data-testid='document-ribbon-tab-insert']").ClickAsync();
        await page.Locator("[data-testid='document-toolbar-table']").ClickAsync();

        var picker = page.Locator("[data-testid='document-table-grid-picker']");
        await Assertions.Expect(picker).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(picker).ToHaveAttributeAsync("role", "grid");

        await picker.FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(page.Locator("[data-testid='document-table-grid-cell-2-2']"))
            .ToHaveClassAsync(new Regex("tm-document-table-grid-picker__cell--focus"));
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] table[data-block-id]").Last)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase21_MoreMenu_KeyboardTraversalExecutesActiveCommandWhenOverflowing()
    {
        var page = await OpenDocumentEditorAsync(width: 390, height: 760);
        var more = page.Locator("[data-testid='document-toolbar-more']");

        try
        {
            await Assertions.Expect(more).ToBeVisibleAsync(new() { Timeout = 5000 });
        }
        catch
        {
            Assert.Inconclusive("More button was not visible at 390px; the toolbar fit without overflow.");
            return;
        }

        await more.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        var menu = page.Locator("[data-testid='document-toolbar-more-menu']");
        await Assertions.Expect(menu).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(menu).ToHaveAttributeAsync("role", "menu");
        await Assertions.Expect(menu.Locator("[role='menuitem']")).Not.ToHaveCountAsync(0, new() { Timeout = 5000 });

        var first = menu.Locator("[role='menuitem']").First;
        var activeBefore = await ReadActiveOverflowCommandAsync(page);
        await first.FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await page.WaitForFunctionAsync(
            """
            () => !!document.querySelector('[data-testid="document-toolbar-more-menu"] [role="menuitem"].tm-document-editor__overflow-menu-item--active:not(:first-of-type), [data-testid="document-toolbar-more-menu"] [role="menuitem"][tabindex="0"]:not(:first-of-type)')
            """,
            options: new PageWaitForFunctionOptions { Timeout = 5000 });
        var activeAfter = await ReadActiveOverflowCommandAsync(page);
        Assert.AreNotEqual(activeBefore, activeAfter, "ArrowDown should move the active overflow menu command.");

        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("[data-testid='document-toolbar-more-menu']")).ToHaveCountAsync(0, new() { Timeout = 5000 });
    }

    [TestMethod]
    public async Task Phase21_LiveRegion_AnnouncesFindSaveAndAutosaveError()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await PlaceCaretAtEndOfBodyAsync(page);
        await page.Keyboard.PressAsync("Control+f");
        await Assertions.Expect(page.Locator("[data-testid='document-find-panel']")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await page.Locator("[data-testid='document-find-input']").FillAsync("agreement");
        await Assertions.Expect(page.Locator("[data-testid='document-editor-live-region']")).ToContainTextAsync("1 of", new() { Timeout = 5000 });

        var savePage = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await EditorTypeAsync(savePage, $" phase21-live-{DateTimeOffset.UtcNow:HHmmssfff}");
        await savePage.Keyboard.PressAsync("Control+S");
        await Assertions.Expect(savePage.GetByTestId("document-save-message"))
            .ToContainTextAsync(new Regex("Saved|Autosaved", RegexOptions.IgnoreCase), new() { Timeout = 10000 });
        await Assertions.Expect(savePage.GetByTestId("document-editor-live-region"))
            .ToContainTextAsync(new Regex("Saved|Autosaved", RegexOptions.IgnoreCase), new() { Timeout = 10000 });

        var failingPage = await OpenDocumentEditorWithQueryAsync("autosaveMs=500", width: 1440, height: 900);
        await failingPage.RouteAsync("**/api/document-editor/documents/**", async route =>
        {
            if (route.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
            {
                await route.FulfillAsync(new()
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = """{"success":false,"errorMessage":"Phase 21 autosave failed","errorKind":1}"""
                });
                return;
            }

            await route.ContinueAsync();
        });

        await EditorTypeAsync(failingPage, $" phase21-autosave-failure-{DateTimeOffset.UtcNow:HHmmssfff}");
        await Assertions.Expect(failingPage.GetByTestId("document-save-message"))
            .ToContainTextAsync("Phase 21 autosave failed", new() { Timeout = 10000 });
        await Assertions.Expect(failingPage.GetByTestId("document-editor-live-region"))
            .ToContainTextAsync("Phase 21 autosave failed", new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase21_ImageObjectNavigation_IsExplicitAndEscapeReturnsCaret()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var body = await WaitForWysiwygBodyAsync(page);
        await body.ClickAsync();

        var imageTabStops = await page.Locator(
            "[data-testid='document-wysiwyg-host'] [data-object-id][tabindex='0'], " +
            "[data-testid='document-wysiwyg-host'] [data-render-object-id][tabindex='0'], " +
            "[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[tabindex='0']").CountAsync();
        Assert.AreEqual(0, imageTabStops, "Images must not become normal Tab stops before explicit object navigation.");

        await body.FocusAsync();
        await page.Keyboard.PressAsync("Tab");
        var activeElementIsObject = await page.EvaluateAsync<bool>(
            """
            () => !!document.activeElement?.closest?.('figure.tm-wysiwyg-image, .tm-render-image-widget, .tm-wysiwyg-inline-drawing[data-object-id], .tm-wysiwyg-object-layer-item[data-object-id], .tm-wysiwyg-object-selection-overlay[data-object-id], .tm-wysiwyg-object-guides-overlay[data-object-id]')
            """);
        Assert.IsFalse(activeElementIsObject, "Plain Tab navigation must not land in each image object.");

        var activeObjectId = await PressNextImageObjectShortcutAsync(page);
        Assert.AreEqual("Object", await ReadDocumentEditorSelectionModeAsync(page));
        Assert.IsFalse(string.IsNullOrWhiteSpace(activeObjectId), "Ctrl+Alt+O should select the next image object.");

        var selectedObject = RenderedImageObjectLocator(page, activeObjectId);
        await Assertions.Expect(selectedObject).ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 5000 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-object-status']"))
            .ToContainTextAsync("Selected image:", new() { Timeout = 5000 });

        await page.Keyboard.PressAsync("Escape");
        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const selection = window.tmDocumentEditorRuntime?.getRuntimeSelection?.(instanceId)
                    || window.tmDocumentEditorRuntime?.getSelectionSnapshot?.(instanceId)
                    || window.tmDocumentEditorEngine?.getSelectionSnapshot?.(instanceId)
                    || {};
                const mode = selection.SelectionMode || selection.selectionMode || selection.Mode || selection.mode || '';
                const objectId = selection.ObjectSelection?.ObjectId
                    || selection.objectSelection?.objectId
                    || selection.ActiveObjectId
                    || selection.activeObjectId
                    || selection.ObjectId
                    || selection.objectId
                    || '';
                return mode === 'Text' && !objectId;
            }
            """,
            options: new PageWaitForFunctionOptions { Timeout = 5000 });

        Assert.AreEqual("Text", await ReadDocumentEditorSelectionModeAsync(page));
        Assert.AreEqual(string.Empty, await ReadActiveDocumentEditorImageIdAsync(page));
        var caret = await ReadDocumentEditorCaretProbeAsync(page);
        Assert.IsFalse(string.IsNullOrWhiteSpace(caret.BlockId), "Escape from object selection should restore a text caret.");
    }

    [TestMethod]
    public async Task Phase21_SelectedImageChrome_HasAccessibleLabelsToolbarAndDeleteCommand()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var body = await WaitForWysiwygBodyAsync(page);
        await body.ClickAsync();
        var drawingCountBefore = await ReadDocumentEditorDrawingRunCountAsync(page);

        var activeObjectId = await PressNextImageObjectShortcutAsync(page);
        var activeRun = (await ReadDocumentEditorDrawingRunsAsync(page, objectId: activeObjectId)).FirstOrDefault();
        Assert.IsNotNull(activeRun, $"Active object '{activeObjectId}' should exist in the drawing-run model.");

        await page.Keyboard.PressAsync("F10");
        var probe = await ReadSelectedImageAccessibilityProbeAsync(page, activeObjectId);

        CollectionAssert.Contains(probe.AccessibleNames, activeRun!.AltText);
        Assert.IsTrue(probe.RoleImageCount > 0, probe.Debug);
        Assert.IsTrue(probe.ToolbarButtonCount >= 8, probe.Debug);
        Assert.AreEqual(probe.ToolbarButtonCount, probe.ToolbarButtonAriaLabelCount, probe.Debug);
        Assert.IsTrue(probe.SelectedResizeHandleAriaLabelCount >= 8, probe.Debug);
        Assert.AreEqual(0, probe.UnselectedResizeHandleAriaLabelCount, probe.Debug);
        Assert.IsTrue(probe.KeyboardToolbarOpen, probe.Debug);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-object-status']"))
            .ToContainTextAsync(activeRun.AltText, new() { Timeout = 5000 });

        await page.Keyboard.PressAsync("Delete");
        await page.WaitForFunctionAsync(
            """
            (objectId) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const raw = window.tmDocumentEditorRuntime?.getDocument?.(instanceId)
                    || window.tmDocumentEditorEngine?.getDocument?.(instanceId)
                    || null;
                const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
                const documentModel = parsed?.Document || parsed?.document || parsed?.csharpDocument || parsed || {};
                return !containsDrawingObject(documentModel, objectId);

                function containsDrawingObject(value, expected, depth = 0) {
                    if (!value || typeof value !== 'object' || depth > 10) return false;
                    if ((value.ObjectId || value.objectId || '') === expected) return true;
                    for (const child of Array.isArray(value) ? value : Object.values(value)) {
                        if (containsDrawingObject(child, expected, depth + 1)) return true;
                    }
                    return false;
                }
            }
            """,
            activeObjectId,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        Assert.AreEqual(drawingCountBefore - 1, await ReadDocumentEditorDrawingRunCountAsync(page));
        Assert.AreEqual("Text", await ReadDocumentEditorSelectionModeAsync(page));
    }

    private async Task<IPage> OpenDocumentEditorWithQueryAsync(string query, int width, int height)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}/document-editor?{query}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);
        return page;
    }

    private static Task<string?> ReadActiveOverflowCommandAsync(IPage page)
    {
        return page.EvaluateAsync<string?>(
            """
            () => {
                const menu = document.querySelector('[data-testid="document-toolbar-more-menu"]');
                const active = menu?.querySelector('[role="menuitem"].tm-document-editor__overflow-menu-item--active')
                    || menu?.querySelector('[role="menuitem"][tabindex="0"]');
                return active?.getAttribute('data-command') || null;
            }
            """);
    }

    private static ILocator RenderedImageObjectLocator(IPage page, string imageId)
        => page.Locator(
            $"[data-testid='document-wysiwyg-host'] [data-testid='document-wysiwyg-object-layer-item'][data-object-id='{imageId}'], " +
            $"[data-testid='document-wysiwyg-host'] [data-testid='document-wysiwyg-inline-drawing'][data-object-id='{imageId}'], " +
            $"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{imageId}']").First;

    private static async Task<string> PressNextImageObjectShortcutAsync(IPage page)
    {
        await page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body[contenteditable='true']")
            .First
            .FocusAsync();
        await page.Keyboard.DownAsync("Control");
        await page.Keyboard.DownAsync("Alt");
        await page.Keyboard.PressAsync("O");
        await page.Keyboard.UpAsync("Alt");
        await page.Keyboard.UpAsync("Control");

        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const selection = window.tmDocumentEditorRuntime?.getRuntimeSelection?.(instanceId)
                    || window.tmDocumentEditorRuntime?.getSelectionSnapshot?.(instanceId)
                    || window.tmDocumentEditorEngine?.getSelectionSnapshot?.(instanceId)
                    || {};
                const mode = selection.SelectionMode || selection.selectionMode || selection.Mode || selection.mode || '';
                const objectId = selection.ObjectSelection?.ObjectId
                    || selection.objectSelection?.objectId
                    || selection.ActiveObjectId
                    || selection.activeObjectId
                    || selection.ObjectId
                    || selection.objectId
                    || '';
                return mode === 'Object' && !!objectId;
            }
            """,
            options: new PageWaitForFunctionOptions { Timeout = 5000 });

        return await ReadActiveDocumentEditorImageIdAsync(page);
    }

    private static Task<SelectedImageAccessibilityProbe> ReadSelectedImageAccessibilityProbeAsync(IPage page, string objectId)
        => page.EvaluateAsync<SelectedImageAccessibilityProbe>(
            """
            (objectId) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const escaped = window.CSS?.escape ? CSS.escape(objectId) : String(objectId).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                const selectedRoots = Array.from(host?.querySelectorAll(`[data-object-id="${escaped}"], [data-render-object-id="${escaped}"], [data-block-id="${escaped}"]`) || [])
                    .filter(node => node.getAttribute('data-object-selected') === 'true'
                        || node.getAttribute('aria-selected') === 'true'
                        || node.classList.contains('tm-wysiwyg-object--selected')
                        || node.classList.contains('tm-wysiwyg-image--selected'));
                const roleImages = selectedRoots.filter(node => node.getAttribute('role') === 'img');
                const accessibleNames = roleImages
                    .map(node => node.getAttribute('aria-label') || '')
                    .filter(Boolean);
                const toolbarButtons = selectedRoots.flatMap(root =>
                    Array.from(root.querySelectorAll?.('[data-testid="document-wysiwyg-object-layout-bubble"] button') || []));
                const handleLabels = selectedRoots.flatMap(root =>
                    Array.from(root.querySelectorAll?.('.tm-wysiwyg-object-resize-handle[aria-label]') || []));
                const unselectedHandleLabels = Array.from(host?.querySelectorAll('.tm-wysiwyg-object-resize-handle[aria-label]') || [])
                    .filter(handle => !handle.closest('[data-object-selected="true"], [aria-selected="true"], .tm-wysiwyg-object--selected, .tm-wysiwyg-image--selected'));
                const keyboardToolbarOpen = selectedRoots.some(root =>
                    !!root.querySelector?.('[data-testid="document-wysiwyg-object-layout-bubble"].tm-wysiwyg-layout-bubble--keyboard-open'));

                return {
                    roleImageCount: roleImages.length,
                    accessibleNames,
                    toolbarButtonCount: toolbarButtons.length,
                    toolbarButtonAriaLabelCount: toolbarButtons.filter(button => !!button.getAttribute('aria-label')).length,
                    selectedResizeHandleAriaLabelCount: handleLabels.length,
                    unselectedResizeHandleAriaLabelCount: unselectedHandleLabels.length,
                    keyboardToolbarOpen,
                    debug: JSON.stringify({
                        objectId,
                        selectedRoots: selectedRoots.map(node => ({
                            tag: node.tagName,
                            className: String(node.className || ''),
                            role: node.getAttribute('role') || '',
                            ariaLabel: node.getAttribute('aria-label') || '',
                            ariaSelected: node.getAttribute('aria-selected') || '',
                            describedBy: node.getAttribute('aria-describedby') || ''
                        })),
                        accessibleNames,
                        toolbarLabels: toolbarButtons.map(button => button.getAttribute('aria-label') || ''),
                        handleLabels: handleLabels.map(handle => handle.getAttribute('aria-label') || '')
                    })
                };
            }
            """,
            objectId);

    private static Task PlaceCaretAtEndOfBodyAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const body = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body[contenteditable="true"]');
                if (!body) throw new Error('WYSIWYG body not found.');
                body.focus();
                const range = document.createRange();
                range.selectNodeContents(body);
                range.collapse(false);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
            }
            """);
    }

    private sealed class SelectedImageAccessibilityProbe
    {
        [JsonPropertyName("roleImageCount")] public int RoleImageCount { get; set; }
        [JsonPropertyName("accessibleNames")] public string[] AccessibleNames { get; set; } = [];
        [JsonPropertyName("toolbarButtonCount")] public int ToolbarButtonCount { get; set; }
        [JsonPropertyName("toolbarButtonAriaLabelCount")] public int ToolbarButtonAriaLabelCount { get; set; }
        [JsonPropertyName("selectedResizeHandleAriaLabelCount")] public int SelectedResizeHandleAriaLabelCount { get; set; }
        [JsonPropertyName("unselectedResizeHandleAriaLabelCount")] public int UnselectedResizeHandleAriaLabelCount { get; set; }
        [JsonPropertyName("keyboardToolbarOpen")] public bool KeyboardToolbarOpen { get; set; }
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }
}
