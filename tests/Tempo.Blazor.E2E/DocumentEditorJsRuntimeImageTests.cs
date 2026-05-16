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
    public async Task Phase11_ArrowLeavesImageSelectionWithoutBlazorRender()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var imageId = $"phase11-image-arrow-{Guid.NewGuid():N}";

        await InsertDataImageBlockAsync(page, imageId, "Arrow leaves image", 140, 90);
        var figure = page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{imageId}']").First;
        await Assertions.Expect(figure).ToBeVisibleAsync();
        await figure.ClickAsync();
        await Assertions.Expect(figure).ToHaveClassAsync(new Regex("tm-wysiwyg-image--selected"));
        var fullRenderBefore = await ReadFullRenderCountAsync(page);

        await page.Keyboard.PressAsync("ArrowRight");

        await Assertions.Expect(figure).Not.ToHaveClassAsync(new Regex("tm-wysiwyg-image--selected"));
        var selection = await ReadRuntimeSelectionAsync(page);
        var debug = await ReadImageSelectionDebugAsync(page, imageId);
        Assert.AreNotEqual("Image", selection.Region, debug);
        Assert.AreEqual(fullRenderBefore, await ReadFullRenderCountAsync(page), "Leaving image selection should be handled by the JS runtime without a full Blazor render.");
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

    private static Task<int> ReadFullRenderCountAsync(IPage page)
    {
        return page.EvaluateAsync<int>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const stats = window.tmDocumentWysiwygDebug?.getRenderStats?.(instanceId) || {};
                return Number(stats.FullRenderCount || 0);
            }
            """);
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
