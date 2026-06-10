using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionMediaRecoveryE2ETests : NotionE2ETestBase
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [TestMethod]
    [Description("EB6 captures empty and loaded media blocks plus the image resize affordance.")]
    public async Task EB6_MediaBlocksEmptyLoadedAndResize_CaptureBaseline()
    {
        await OpenNotionEditorAsync();
        await SeedMediaPageAsync();

        await CaptureBlockAsync("eb600000-0000-0000-0000-000000000011", "eb6-image-empty");
        await CaptureBlockAsync("eb600000-0000-0000-0000-000000000021", "eb6-image-loaded");

        var imageLoaded = await GetBlockAsync("eb600000-0000-0000-0000-000000000021");
        await imageLoaded.Locator(".tm-notion-image-block__img-wrap").HoverAsync();
        await imageLoaded.Locator(".tm-notion-resize-handle").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureBaselineAsync("media", "eb6-image-resize-handle", imageLoaded);

        await CaptureBlockAsync("eb600000-0000-0000-0000-000000000031", "eb6-video-empty");
        await CaptureBlockAsync("eb600000-0000-0000-0000-000000000041", "eb6-video-loaded");
        await CaptureBlockAsync("eb600000-0000-0000-0000-000000000051", "eb6-audio-empty");
        await CaptureBlockAsync("eb600000-0000-0000-0000-000000000061", "eb6-audio-loaded");
        await CaptureBlockAsync("eb600000-0000-0000-0000-000000000071", "eb6-file-empty");
        await CaptureBlockAsync("eb600000-0000-0000-0000-000000000081", "eb6-file-loaded");
        await CaptureBlockAsync("eb600000-0000-0000-0000-000000000091", "eb6-pdf-empty");
        await WaitForPdfCanvasContentAsync("eb600000-0000-0000-0000-000000000101");
        await CaptureBlockAsync("eb600000-0000-0000-0000-000000000101", "eb6-pdf-loaded");
    }

    [TestMethod]
    [Description("EB6 captures upload dialog, drag-active styling, invalid type, and too-large file validation states.")]
    public async Task EB6_UploadDialogDragAndValidationErrors_CaptureBaseline()
    {
        await OpenNotionEditorAsync();
        await SeedMediaPageAsync();

        var imageEmpty = await GetBlockAsync("eb600000-0000-0000-0000-000000000011");
        await imageEmpty.Locator(".tm-notion-media-upload-zone--image").ClickAsync();
        var dialog = await GetDialogAsync();
        await CaptureBaselineAsync("media", "eb6-upload-dialog", dialog);

        var dropzone = dialog.Locator(".tm-media-dialog__dropzone").First;
        await DispatchDialogDragEnterAsync(dropzone);
        await Assertions.Expect(dropzone).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("tm-media-dialog__dropzone--dragging"));
        await CaptureBaselineAsync("media", "eb6-upload-dialog-drag-active", dialog);

        var input = dialog.Locator("input[type='file']").First;
        await input.SetInputFilesAsync(new FilePayload
        {
            Name = "not-an-image.txt",
            MimeType = "text/plain",
            Buffer = "EB6 invalid image upload"u8.ToArray()
        });
        await dialog.Locator(".tm-media-dialog__error").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureBaselineAsync("media", "eb6-drop-invalid-type-error", dialog);

        var largeFilePath = CreateTooLargeImageFile();
        try
        {
            await input.SetInputFilesAsync(largeFilePath);
            await Assertions.Expect(dialog.Locator(".tm-media-dialog__error"))
                .ToContainTextAsync("Maximum size is");
            await CaptureBaselineAsync("media", "eb6-drop-too-large-error", dialog);
        }
        finally
        {
            if (File.Exists(largeFilePath))
                File.Delete(largeFilePath);
        }
    }

    [TestMethod]
    [Description("EB6 captures image, HTML, plain text paste flows and the no-file-provider dialog state.")]
    public async Task EB6_PasteAndNoFileProviderStates_CaptureBaseline()
    {
        await OpenNotionEditorAsync();
        await SeedMediaPageAsync();

        var imageEmpty = await GetBlockAsync("eb600000-0000-0000-0000-000000000011");
        await FocusElementAsync(imageEmpty.Locator(".tm-notion-image-block").First);
        await DispatchImagePasteAsync(imageEmpty.Locator(".tm-notion-image-block").First, "eb6-pasted.png");
        await imageEmpty.Locator(".tm-notion-image-block__img[src^='data:image/']").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureBaselineAsync("media", "eb6-paste-image-uploaded", imageEmpty);

        await SeedEmptyPageAsync();
        var editable = Page.Locator("[data-block-id='eb000000-0000-0000-0000-000000000001'] .tm-notion-paragraph").First;
        await FocusElementAsync(editable);
        await DispatchClipboardPasteAsync(
            editable,
            "<p><strong>EB6 rich paste</strong> with <em>safe formatting</em><script>window.__eb6Unsafe = true</script></p>",
            "EB6 rich paste with safe formatting");
        await Assertions.Expect(editable.Locator("strong")).ToContainTextAsync("EB6 rich paste");
        await CaptureBaselineAsync("media", "eb6-paste-html", editable);

        await SeedEmptyPageAsync();
        editable = Page.Locator("[data-block-id='eb000000-0000-0000-0000-000000000001'] .tm-notion-paragraph").First;
        await FocusElementAsync(editable);
        await DispatchClipboardPasteAsync(editable, null, "EB6 plain text paste\nSecond line remains readable");
        await Assertions.Expect(editable).ToContainTextAsync("EB6 plain text paste");
        await CaptureBaselineAsync("media", "eb6-paste-plain-text", editable);

        await OpenNotionEditorAsync("?disableFileProvider=true");
        await SeedMediaPageAsync();
        var providerless = await GetBlockAsync("eb600000-0000-0000-0000-000000000011");
        await providerless.Locator(".tm-notion-media-upload-zone--image").ClickAsync();
        var providerlessDialog = await GetDialogAsync();
        await Assertions.Expect(providerlessDialog.Locator(".tm-media-dialog__notice")).ToBeVisibleAsync();
        await CaptureBaselineAsync("media", "eb6-no-file-provider-dialog", providerlessDialog);
    }

    private async Task CaptureBlockAsync(string blockId, string state)
    {
        var block = await GetBlockAsync(blockId);
        await CaptureBaselineAsync("media", state, block);
    }

    private async Task<ILocator> GetBlockAsync(string blockId)
    {
        var block = Page.Locator($"[data-block-id='{blockId}']").First;
        await block.ScrollIntoViewIfNeededAsync();
        await block.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        return block;
    }

    private async Task<ILocator> GetDialogAsync()
    {
        var dialog = Page.Locator(".tm-media-dialog").First;
        await dialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        return dialog;
    }

    private async Task FocusElementAsync(ILocator locator)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        var handle = await locator.ElementHandleAsync();
        Assert.IsNotNull(handle);
        await Page.EvaluateAsync("element => element.focus()", handle);
        await Page.WaitForTimeoutAsync(150);
    }

    private Task WaitForPdfCanvasContentAsync(string blockId) =>
        Page.WaitForFunctionAsync(
            """
            blockId => {
                const block = document.querySelector(`[data-block-id='${blockId}']`);
                const canvas = block?.querySelector('.tm-pdf-viewer__canvas');
                if (!canvas || canvas.width < 100 || canvas.height < 100) return false;
                const context = canvas.getContext('2d');
                if (!context) return false;

                try {
                    const data = context.getImageData(0, 0, canvas.width, canvas.height).data;
                    const step = Math.max(4, Math.floor(data.length / 4000 / 4) * 4);
                    for (let i = 0; i < data.length; i += step) {
                        const alpha = data[i + 3];
                        if (alpha > 0 && (data[i] < 245 || data[i + 1] < 245 || data[i + 2] < 245)) {
                            return true;
                        }
                    }
                } catch {
                    return false;
                }

                return false;
            }
            """,
            blockId,
            new PageWaitForFunctionOptions { Timeout = 60000 });

    private static Task DispatchDialogDragEnterAsync(ILocator locator) =>
        locator.EvaluateAsync(
            """
            element => {
                const data = new DataTransfer();
                element.dispatchEvent(new DragEvent('dragenter', {
                    bubbles: true,
                    cancelable: true,
                    dataTransfer: data
                }));
            }
            """);

    private async Task DispatchImagePasteAsync(ILocator locator, string fileName)
    {
        var handle = await locator.ElementHandleAsync();
        Assert.IsNotNull(handle);
        await Page.EvaluateAsync(
            """
            args => {
                const byteString = atob(args.base64);
                const bytes = new Uint8Array(byteString.length);
                for (let i = 0; i < byteString.length; i++) {
                    bytes[i] = byteString.charCodeAt(i);
                }
                const file = new File([bytes], args.fileName, { type: 'image/png' });
                const data = new DataTransfer();
                data.items.add(file);
                const event = new ClipboardEvent('paste', { bubbles: true, cancelable: true });
                Object.defineProperty(event, 'clipboardData', { value: data });
                args.element.dispatchEvent(event);
            }
            """,
            new { element = handle, base64 = TinyPngBase64, fileName });
    }

    private async Task DispatchClipboardPasteAsync(ILocator locator, string? html, string plainText)
    {
        var handle = await locator.ElementHandleAsync();
        Assert.IsNotNull(handle);
        await Page.EvaluateAsync(
            """
            args => {
                const data = new DataTransfer();
                if (args.html) data.setData('text/html', args.html);
                data.setData('text/plain', args.plainText);
                const event = new ClipboardEvent('paste', { bubbles: true, cancelable: true });
                Object.defineProperty(event, 'clipboardData', { value: data });
                args.element.dispatchEvent(event);
            }
            """,
            new { element = handle, html, plainText });
        await Page.WaitForTimeoutAsync(250);
    }

    private static string CreateTooLargeImageFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tempo-eb6-too-large-{Guid.NewGuid():N}.png");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.SetLength(100L * 1024 * 1024 + 1);
        return path;
    }
}
