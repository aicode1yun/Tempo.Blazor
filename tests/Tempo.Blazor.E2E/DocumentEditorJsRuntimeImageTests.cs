using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for JS-owned image runtime objects.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeImageTests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase11_ClickingImageReportsImageRuntimeSelection()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var imageId = $"phase11-image-selection-{Guid.NewGuid():N}";

        await InsertDataImageBlockAsync(page, imageId, "Runtime selected image", 140, 90);
        var figure = page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{imageId}']").First;
        await Assertions.Expect(figure).ToBeVisibleAsync();

        await figure.ClickAsync();

        await Assertions.Expect(figure).ToHaveClassAsync(new Regex("tm-wysiwyg-image--selected"));
        var selection = await ReadRuntimeSelectionAsync(page);
        Assert.AreEqual("Image", selection.Region);
        Assert.AreEqual(imageId, selection.ActiveImageBlockId);
    }

    [TestMethod]
    public async Task Phase16_ArrowKeysMoveSelectedImageAndUndoRedoRestoresPosition()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var imageId = $"phase16-image-arrow-{Guid.NewGuid():N}";

        await InsertDataImageBlockAsync(page, imageId, "Keyboard movable image", 140, 90);
        var figure = page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{imageId}']").First;
        await Assertions.Expect(figure).ToBeVisibleAsync();
        await FocusImageWithTabAsync(page, imageId);
        await Assertions.Expect(figure).ToHaveClassAsync(new Regex("tm-wysiwyg-image--selected"));
        var xBefore = await ReadImageCoordinateAsync(page, imageId, "data-image-x");

        await page.Keyboard.PressAsync("ArrowRight");

        await page.WaitForFunctionAsync(
            """
            ({ imageId, xBefore }) => {
                const figure = document.querySelector(`[data-testid="document-wysiwyg-host"] figure.tm-wysiwyg-image[data-block-id="${CSS.escape(imageId)}"]`);
                return Number(figure?.getAttribute('data-image-x') || 0) === xBefore + 1;
            }
            """,
            new { imageId, xBefore });
        var xAfterArrow = await ReadImageCoordinateAsync(page, imageId, "data-image-x");
        xAfterArrow.Should().Be(xBefore + 1);

        await page.Keyboard.PressAsync("Control+Z");
        await page.WaitForFunctionAsync(
            """
            ({ imageId, xBefore }) => {
                const figure = document.querySelector(`[data-testid="document-wysiwyg-host"] figure.tm-wysiwyg-image[data-block-id="${CSS.escape(imageId)}"]`);
                return Number(figure?.getAttribute('data-image-x') || 0) === xBefore;
            }
            """,
            new { imageId, xBefore });

        await page.Keyboard.PressAsync("Control+Y");
        await page.WaitForFunctionAsync(
            """
            ({ imageId, xAfterArrow }) => {
                const figure = document.querySelector(`[data-testid="document-wysiwyg-host"] figure.tm-wysiwyg-image[data-block-id="${CSS.escape(imageId)}"]`);
                return Number(figure?.getAttribute('data-image-x') || 0) === xAfterArrow;
            }
            """,
            new { imageId, xAfterArrow });

        await page.Keyboard.PressAsync("Shift+ArrowDown");
        (await ReadImageCoordinateAsync(page, imageId, "data-image-y")).Should().Be(10);

        await page.Keyboard.PressAsync("Control+ArrowLeft");
        (await ReadImageCoordinateAsync(page, imageId, "data-image-x")).Should().Be(xAfterArrow - 0.25);

        var selection = await ReadRuntimeSelectionAsync(page);
        var debug = await ReadImageSelectionDebugAsync(page, imageId);
        Assert.AreEqual("Image", selection.Region, debug);
        Assert.AreEqual(imageId, selection.ActiveImageBlockId, debug);
    }

    [TestMethod]
    public async Task Phase11_ImageSnapshotKeepsNaturalAndDisplaySize()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var imageId = $"phase11-image-size-{Guid.NewGuid():N}";

        await InsertDataImageBlockAsync(page, imageId, "Natural size image", 140, 90);
        var figure = page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{imageId}']").First;
        await Assertions.Expect(figure).ToBeVisibleAsync();
        await page.WaitForFunctionAsync(
            """
            imageId => {
                const figure = document.querySelector(`[data-testid="document-wysiwyg-host"] figure.tm-wysiwyg-image[data-block-id="${CSS.escape(imageId)}"]`);
                return Number(figure?.getAttribute('data-image-natural-width') || 0) > 0
                    && Number(figure?.getAttribute('data-image-natural-height') || 0) > 0;
            }
            """,
            imageId);

        var content = await ReadImageContentAsync(page, imageId);

        Assert.IsNotNull(content.Size);
        Assert.IsNotNull(content.NaturalSize);
        Assert.AreEqual(140, content.Size.Width);
        Assert.AreEqual(90, content.Size.Height);
        Assert.IsTrue(content.NaturalSize.Width > 0, "The JS snapshot should keep the loaded natural image width.");
        Assert.IsTrue(content.NaturalSize.Height > 0, "The JS snapshot should keep the loaded natural image height.");
    }

    [TestMethod]
    public async Task Phase16_KeyboardFocusOpensLayoutBubbleAndChangesWrapMode()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var imageId = $"phase16-image-bubble-{Guid.NewGuid():N}";

        await InsertDataImageBlockAsync(page, imageId, "Keyboard layout image", 140, 90);
        var figure = page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{imageId}']").First;
        await FocusImageWithTabAsync(page, imageId);

        await Assertions.Expect(figure).ToBeFocusedAsync();
        await Assertions.Expect(figure).ToHaveAttributeAsync("tabindex", "0");
        var ariaLabel = await figure.GetAttributeAsync("aria-label");
        StringAssert.Contains(ariaLabel, "Keyboard layout image");
        StringAssert.Contains(ariaLabel, "Wrap mode");

        await page.Keyboard.PressAsync("Enter");
        var bubble = figure.Locator("[data-testid='document-wysiwyg-object-layout-bubble']");
        await Assertions.Expect(bubble).ToHaveClassAsync(new Regex("tm-wysiwyg-layout-bubble--keyboard-open"));

        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(figure).ToHaveAttributeAsync("data-wrap-mode", "1");
        await Assertions.Expect(figure.Locator("[data-testid='document-wysiwyg-layout-bubble-wrap']")).ToHaveAttributeAsync("aria-pressed", "true");
    }

    [TestMethod]
    public async Task Phase16_ShiftF10OpensImageContextMenuAndEscapeReturnsFocus()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var imageId = $"phase16-image-menu-{Guid.NewGuid():N}";

        await InsertDataImageBlockAsync(page, imageId, "Keyboard menu image", 140, 90);
        var figure = page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{imageId}']").First;
        await FocusImageWithTabAsync(page, imageId);

        await page.Keyboard.PressAsync("Shift+F10");
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-image-context-menu']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-image-replace']")).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-image-context-menu']")).ToHaveCountAsync(0);
        await Assertions.Expect(figure).ToBeFocusedAsync();
    }

    [TestMethod]
    public async Task Phase16_DeleteRemovesKeyboardSelectedImage()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var imageId = $"phase16-image-delete-{Guid.NewGuid():N}";

        await InsertDataImageBlockAsync(page, imageId, "Keyboard delete image", 140, 90);
        await FocusImageWithTabAsync(page, imageId);

        await page.Keyboard.PressAsync("Delete");

        await Assertions.Expect(page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{imageId}']")).ToHaveCountAsync(0);
    }

    [TestMethod]
    public async Task Phase16_MissingAltWarningAndDecorativeStateAreExposed()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var imageId = $"phase16-image-alt-{Guid.NewGuid():N}";

        await InsertDataImageBlockAsync(page, imageId, string.Empty, 140, 90);
        var figure = page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{imageId}']").First;
        await Assertions.Expect(figure).ToHaveAttributeAsync("data-image-alt-warning", "true");
        await Assertions.Expect(figure.Locator("[data-testid='document-wysiwyg-image-alt-warning']")).ToHaveCountAsync(1);
        StringAssert.Contains(await figure.GetAttributeAsync("aria-label"), "missing alternative text");

        await page.EvaluateAsync(
            """
            imageId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorRuntime.executeCommand(instanceId, 'setImageDecorative', {
                    BlockId: imageId,
                    IsDecorative: true
                });
            }
            """,
            imageId);

        await Assertions.Expect(figure).ToHaveAttributeAsync("data-image-alt-warning", "false");
        await Assertions.Expect(figure).ToHaveAttributeAsync("data-image-decorative", "true");
        await Assertions.Expect(figure.Locator("[data-testid='document-wysiwyg-image-alt-warning']")).ToHaveCountAsync(0);
        StringAssert.Contains(await figure.GetAttributeAsync("aria-label"), "Decorative image");
        (await ReadImageContentAsync(page, imageId)).IsDecorative.Should().BeTrue();
    }

    [TestMethod]
    public async Task Phase17_ImageOperationsAndSaveReloadNeverCreateLegacySidecars()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var imageId = $"phase17-image-no-sidecar-{Guid.NewGuid():N}";

        await InsertDataImageBlockAsync(page, imageId, "No legacy sidecar image", 140, 90);
        await AssertNoLegacySidecarAsync(page);

        await ExecuteImageCommandSeriesAsync(page, imageId);
        await AssertNoLegacySidecarAsync(page);

        await SaveDocumentAsync(page);
        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForDocumentEditorReadyAsync(page);

        await Assertions.Expect(page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{imageId}']")).ToBeVisibleAsync();
        await AssertNoLegacySidecarAsync(page);
    }

    private static Task InsertDataImageBlockAsync(IPage page, string imageId, string altText, double width, double height)
    {
        return page.EvaluateAsync(
            """
            ({ imageId, altText, width, height }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id');
                const body = Array.from(host?.querySelectorAll('.tm-wysiwyg-page__body[contenteditable]') || [])
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        const style = getComputedStyle(element);
                        return rect.width > 0
                            && rect.height > 0
                            && style.display !== 'none'
                            && style.visibility !== 'hidden'
                            && !element.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual');
                    });
                const anchor = Array.from(body?.querySelectorAll('.tm-wysiwyg-block[data-block-id]') || [])
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0;
                    });
                body?.focus();
                if (anchor) {
                    const range = document.createRange();
                    range.selectNodeContents(anchor);
                    range.collapse(false);
                    const selection = window.getSelection();
                    selection?.removeAllRanges();
                    selection?.addRange(range);
                }

                window.tmDocumentEditorWysiwyg.insertImageNode(instanceId, {
                    Id: imageId,
                    Type: 5,
                    Order: 25,
                    Content: {
                        $type: 'image',
                        Source: 0,
                        AssetId: `asset-${imageId}`,
                        Url: '/favicon.png',
                        AltText: altText,
                        Size: { Width: width, Height: height, LockAspectRatio: true },
                        Alignment: 1,
                        Caption: 'Runtime image'
                    }
                }, true);
            }
            """,
            new { imageId, altText, width, height });
    }

    private static Task ExecuteImageCommandSeriesAsync(IPage page, string imageId)
    {
        return page.EvaluateAsync(
            """
            imageId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const runtime = window.tmDocumentEditorRuntime;
                runtime.executeCommand(instanceId, 'setImageWrapMode', { BlockId: imageId, WrapMode: 'Square' });
                runtime.executeCommand(instanceId, 'setImageHorizontalPosition', { BlockId: imageId, HorizontalPosition: 'Left' });
                runtime.executeCommand(instanceId, 'setImageObjectPosition', {
                    BlockId: imageId,
                    X: 42,
                    Y: 28,
                    HorizontalRelativeTo: 'Margin',
                    VerticalRelativeTo: 'Paragraph',
                    HorizontalPosition: 'Left'
                });
                runtime.executeCommand(instanceId, 'setImageSize', {
                    BlockId: imageId,
                    Width: 180,
                    Height: 116,
                    LockAspectRatio: false
                });
                runtime.executeCommand(instanceId, 'setImageWrapMode', { BlockId: imageId, WrapMode: 'Tight' });
                runtime.executeCommand(instanceId, 'setImageHorizontalPosition', { BlockId: imageId, HorizontalPosition: 'Right' });
            }
            """,
            imageId);
    }

    private static async Task AssertNoLegacySidecarAsync(IPage page)
    {
        var count = await page.Locator("[data-testid='document-wysiwyg-host'] [data-wrap-sidecar-for], [data-testid='document-wysiwyg-host'] .tm-wysiwyg-image-sidecar-text").CountAsync();
        Assert.AreEqual(0, count, "The editor must not render legacy image sidecar paragraphs.");
    }

    private static async Task SaveDocumentAsync(IPage page)
    {
        await page.Locator("[data-testid='document-save']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-save-message']"))
            .ToContainTextAsync("Saved", new() { Timeout = 10000 });
    }

    private static Task<RuntimeSelectionSnapshot> ReadRuntimeSelectionAsync(IPage page)
    {
        return page.EvaluateAsync<RuntimeSelectionSnapshot>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentEditorRuntime?.getRuntimeSelection?.(instanceId) || {};
            }
            """);
    }

    private static async Task FocusImageWithTabAsync(IPage page, string imageId)
    {
        await page.EvaluateAsync(
            """
            imageId => {
                const figure = document.querySelector(`[data-testid="document-wysiwyg-host"] figure.tm-wysiwyg-image[data-block-id="${CSS.escape(imageId)}"]`);
                if (!figure) return;
                const marker = document.createElement('button');
                marker.type = 'button';
                marker.textContent = 'focus marker';
                marker.style.position = 'fixed';
                marker.style.left = '-10000px';
                marker.style.top = '0';
                marker.setAttribute('data-testid', `phase16-focus-marker-${imageId}`);
                figure.parentElement?.insertBefore(marker, figure);
                marker.focus();
            }
            """,
            imageId);

        await page.Keyboard.PressAsync("Tab");
        await page.WaitForFunctionAsync(
            """
            imageId => document.activeElement?.matches?.(`[data-testid="document-wysiwyg-host"] figure.tm-wysiwyg-image[data-block-id="${CSS.escape(imageId)}"]`)
            """,
            imageId);
    }

    private static Task<double> ReadImageCoordinateAsync(IPage page, string imageId, string attribute)
    {
        return page.EvaluateAsync<double>(
            """
            ({ imageId, attribute }) => {
                const figure = document.querySelector(`[data-testid="document-wysiwyg-host"] figure.tm-wysiwyg-image[data-block-id="${CSS.escape(imageId)}"]`);
                return Number(figure?.getAttribute(attribute) || 0);
            }
            """,
            new { imageId, attribute });
    }

    private static Task<ImageContentSnapshot> ReadImageContentAsync(IPage page, string imageId)
    {
        return page.EvaluateAsync<ImageContentSnapshot>(
            """
            imageId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const raw = window.tmDocumentEditorRuntime?.getDocument?.(instanceId);
                const snapshot = typeof raw === 'string' ? JSON.parse(raw) : raw;
                const blocks = snapshot?.Document?.Blocks || snapshot?.document?.blocks || [];
                const block = blocks.find(item => (item.Id || item.id) === imageId);
                return block?.Content || block?.content || {};
            }
            """,
            imageId);
    }

    private static Task<string> ReadImageSelectionDebugAsync(IPage page, string imageId)
    {
        return page.EvaluateAsync<string>(
            """
            imageId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const figure = host?.querySelector(`figure.tm-wysiwyg-image[data-block-id="${CSS.escape(imageId)}"]`);
                const runtime = window.tmDocumentEditorRuntime?.getRuntimeSelection?.(instanceId) || null;
                return JSON.stringify({
                    className: figure?.className || '',
                    ariaSelected: figure?.getAttribute('aria-selected') || '',
                    activeTag: document.activeElement?.tagName || '',
                    activeClass: document.activeElement?.className || '',
                    runtime
                });
            }
            """,
            imageId);
    }

    private sealed class RuntimeSelectionSnapshot
    {
        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("activeImageBlockId")]
        public string? ActiveImageBlockId { get; set; }
    }

    private sealed class ImageContentSnapshot
    {
        [JsonPropertyName("IsDecorative")]
        public bool IsDecorative { get; set; }

        [JsonPropertyName("Size")]
        public ImageSizeSnapshot? Size { get; set; }

        [JsonPropertyName("NaturalSize")]
        public ImageSizeSnapshot? NaturalSize { get; set; }
    }

    private sealed class ImageSizeSnapshot
    {
        [JsonPropertyName("Width")]
        public double Width { get; set; }

        [JsonPropertyName("Height")]
        public double Height { get; set; }
    }
}
