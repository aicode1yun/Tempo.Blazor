using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for image selection, toolbar, and side-panel parity.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase7E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task RecoveryImageSelection_ClickShowsOutlineHandlesToolbarAndInspector()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var figure = await ClickFirstVisibleImageAsync(page);
        await Assertions.Expect(figure).ToHaveClassAsync(new Regex("tm-wysiwyg-image--selected"), new() { Timeout = 5000 });
        await Assertions.Expect(figure).ToHaveAttributeAsync("aria-selected", "true");
        await ExpectImageToolbarVisibleAsync(page);
        await Assertions.Expect(page.GetByTestId("document-image-inspector")).ToBeVisibleAsync(new() { Timeout = 5000 });

        var handleCount = await CountVisibleResizeHandlesAsync(page);
        Assert.AreEqual(8, handleCount, "Selected images must expose all eight resize handles.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryImageSelection_ClickShowsOutlineHandlesToolbarAndInspector));
    }

    [TestMethod]
    public async Task RecoveryImageToolbar_CommandsUpdateVisibleStateAndInspector()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var figure = await ClickFirstVisibleImageAsync(page);
        await ExpectImageToolbarVisibleAsync(page);

        await page.GetByTestId("document-image-wrap-top-bottom").ClickAsync();
        try
        {
            await Assertions.Expect(figure).ToHaveAttributeAsync("data-wrap-mode", "TopBottom", new() { Timeout = 5000 });
        }
        catch (PlaywrightException ex)
        {
            var debug = await ReadImageCommandDebugAsync(page);
            throw new AssertFailedException($"{ex.Message}\nImage command debug: {System.Text.Json.JsonSerializer.Serialize(debug)}");
        }
        await Assertions.Expect(page.GetByTestId("document-image-wrap-top-bottom")).ToHaveAttributeAsync("aria-pressed", "true");

        await page.GetByTestId("document-image-position-center").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-image-position-center")).ToHaveAttributeAsync("aria-pressed", "true", new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-image-position-left")).ToHaveAttributeAsync("aria-pressed", "false");
        await Assertions.Expect(page.GetByTestId("document-image-inspector")).ToBeVisibleAsync();

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryImageToolbar_CommandsUpdateVisibleStateAndInspector));
    }

    [TestMethod]
    public async Task RecoveryImageToolbar_GeometryDoesNotOverlapSidePanelAndTextClickHidesImageTools()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickFirstVisibleImageAsync(page);
        await ExpectImageToolbarVisibleAsync(page);
        await Assertions.Expect(page.GetByTestId("document-image-inspector")).ToBeVisibleAsync(new() { Timeout = 5000 });

        var geometry = await ReadImageUiGeometryAsync(page);
        Assert.IsTrue(geometry.Toolbar.Width > 1, "Image toolbar must have visible geometry.");
        Assert.IsTrue(geometry.Toolbar.X >= 0, "Image toolbar must stay inside the left viewport edge.");
        Assert.IsTrue(geometry.Toolbar.X + geometry.Toolbar.Width <= geometry.ViewportWidth + 0.5,
            "Image toolbar must stay inside the right viewport edge.");
        if (geometry.SidePanel is not null)
        {
            Assert.IsTrue(geometry.Toolbar.X + geometry.Toolbar.Width <= geometry.SidePanel.X - 2,
                $"Image toolbar must not overlap the right properties panel. Toolbar right={geometry.Toolbar.X + geometry.Toolbar.Width:0.##}, side panel left={geometry.SidePanel.X:0.##}.");
        }

        await ClickRecoveryTextParagraphAsync(page);
        await Assertions.Expect(page.GetByTestId("document-image-inspector")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-image-wrap-panel")).ToHaveCountAsync(0, new() { Timeout = 5000 });

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryImageToolbar_GeometryDoesNotOverlapSidePanelAndTextClickHidesImageTools));
    }

    private static async Task<ILocator> ClickFirstVisibleImageAsync(IPage page)
    {
        var figure = page.Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id]").First;
        await Assertions.Expect(figure).ToBeVisibleAsync(new() { Timeout = 5000 });
        await figure.ScrollIntoViewIfNeededAsync();
        await figure.ClickAsync();
        return figure;
    }

    private static async Task ExpectImageToolbarVisibleAsync(IPage page)
    {
        try
        {
            await Assertions.Expect(page.GetByTestId("document-image-wrap-panel")).ToBeVisibleAsync(new() { Timeout = 5000 });
        }
        catch (PlaywrightException ex)
        {
            var debug = await page.EvaluateAsync<object>(
                """
                () => {
                    const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                    const instanceId = host?.getAttribute('data-instance-id') || '';
                    const figure = document.querySelector('figure.tm-wysiwyg-image.tm-wysiwyg-image--selected, figure.tm-wysiwyg-image[aria-selected="true"]');
                    return {
                        hostActiveRegion: host?.getAttribute('data-active-region') || '',
                        hostFocusOwner: host?.getAttribute('data-focus-owner') || '',
                        selectedFigureBlockId: figure?.getAttribute('data-block-id') || '',
                        selectedFigureClass: figure?.className || '',
                        floatingRootExists: !!document.querySelector('[data-testid="document-wysiwyg-floating-root"]'),
                        toolbarCount: document.querySelectorAll('[data-testid="document-image-wrap-panel"]').length,
                        inspectorCount: document.querySelectorAll('[data-testid="document-image-inspector"]').length,
                        engineSelection: window.tmDocumentEditorEngine?.getSelectionSnapshot?.(instanceId) || null
                    };
                }
                """);
            throw new AssertFailedException($"{ex.Message}\nImage toolbar debug: {System.Text.Json.JsonSerializer.Serialize(debug)}");
        }
    }

    private static Task<int> CountVisibleResizeHandlesAsync(IPage page)
        => page.EvaluateAsync<int>(
            """
            () => Array.from(document.querySelectorAll('[data-testid^="document-wysiwyg-object-resize-handle-"]'))
                .filter(handle => {
                    const rect = handle.getBoundingClientRect();
                    const style = getComputedStyle(handle);
                    return rect.width > 1
                        && rect.height > 1
                        && style.visibility !== 'hidden'
                        && style.display !== 'none'
                        && Number(style.opacity) > 0.5;
                }).length
            """);

    private static Task<object> ReadImageCommandDebugAsync(IPage page)
        => page.EvaluateAsync<object>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const debug = window.tmDocumentEditorEngine?.getDebugSnapshot?.(instanceId) || {};
                return {
                    selection: window.tmDocumentEditorEngine?.getSelectionSnapshot?.(instanceId) || null,
                    commands: (debug.commands || []).slice(-5),
                    figureWrapMode: document.querySelector('figure.tm-wysiwyg-image[aria-selected="true"]')?.getAttribute('data-wrap-mode') || '',
                    toolbarHtml: document.querySelector('[data-testid="document-image-wrap-panel"]')?.outerHTML?.slice(0, 1000) || ''
                };
            }
            """);

    private static Task ClickRecoveryTextParagraphAsync(IPage page)
        => page.EvaluateAsync(
            """
            () => {
                const block = Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] [data-block-id="recovery-comment-paragraph"]'))
                    .find(node => {
                        const rect = node.getBoundingClientRect();
                        return rect.width > 1 && rect.height > 1 && !node.closest('.tm-wysiwyg-page--virtual');
                    });
                if (!block) throw new Error('Could not find recovery text paragraph.');
                block.scrollIntoView({ block: 'center', inline: 'nearest' });
                const rect = block.getBoundingClientRect();
                const target = document.elementFromPoint(rect.left + Math.min(24, rect.width / 2), rect.top + rect.height / 2) || block;
                target.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, composed: true, clientX: rect.left + 16, clientY: rect.top + rect.height / 2 }));
                target.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, composed: true, clientX: rect.left + 16, clientY: rect.top + rect.height / 2 }));
                target.dispatchEvent(new MouseEvent('click', { bubbles: true, composed: true, clientX: rect.left + 16, clientY: rect.top + rect.height / 2 }));
            }
            """);

    private static Task<ImageUiGeometry> ReadImageUiGeometryAsync(IPage page)
        => page.EvaluateAsync<ImageUiGeometry>(
            """
            () => {
                const rectOf = selector => {
                    const node = document.querySelector(selector);
                    if (!node) return null;
                    const rect = node.getBoundingClientRect();
                    return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
                };
                return {
                    viewportWidth: window.innerWidth,
                    viewportHeight: window.innerHeight,
                    toolbar: rectOf('[data-testid="document-image-wrap-panel"]'),
                    sidePanel: rectOf('[data-testid="document-editor-side-panel"], [data-testid="document-properties-panel"], [data-testid="document-image-inspector"]')
                };
            }
            """);

    private sealed class ImageUiGeometry
    {
        [JsonPropertyName("viewportWidth")] public double ViewportWidth { get; set; }
        [JsonPropertyName("viewportHeight")] public double ViewportHeight { get; set; }
        [JsonPropertyName("toolbar")] public UiRect Toolbar { get; set; } = new();
        [JsonPropertyName("sidePanel")] public UiRect? SidePanel { get; set; }
    }

    private sealed class UiRect
    {
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("width")] public double Width { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
    }
}
