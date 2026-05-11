using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Odt;

namespace Tempo.Blazor.E2E;

[TestClass]
[DoNotParallelize]
public class DocumentEditorE2ETests : WasmTestBase
{
    [TestMethod]
    public async Task DocumentEditor_DemoPage_RendersWordLikeShell()
    {
        var page = await OpenDocumentEditorPageAsync();

        var editor = page.Locator("[data-testid='document-editor-demo']");
        await Assertions.Expect(editor).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator(".tm-document-editor__ribbon")).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator(".tm-document-editor__page-surface")).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator(".tm-document-editor__comment-rail")).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator(".tm-document-editor__version-panel")).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator(".tm-document-editor__document-title")).ToContainTextAsync("Service agreement");
        var firstParagraph = await editor.Locator("[data-testid='document-paragraph-editor']").First.InputValueAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(firstParagraph), "Expected the first document paragraph to be editable and non-empty.");
        var imageCount = await editor.Locator("img.tm-document-image__media").CountAsync();
        Assert.IsTrue(imageCount >= 2, $"Expected at least 2 document images, found {imageCount}.");
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanSwitchDocumentsAndReadOnlyMode()
    {
        var page = await OpenDocumentEditorPageAsync();

        await page.Locator("[data-testid='document-editor-filing']").ClickAsync();

        var editor = page.Locator("[data-testid='document-editor-demo']");
        await Assertions.Expect(editor.Locator(".tm-document-editor__document-title")).ToContainTextAsync("Court filing");

        await page.Locator("[data-testid='document-editor-readonly']").CheckAsync();
        await Assertions.Expect(editor).ToHaveClassAsync(new Regex("tm-document-editor--readonly"));

        var buttons = editor.Locator(".tm-document-editor__ribbon-button");
        await Assertions.Expect(buttons.First).ToBeDisabledAsync();

        await page.Locator("[data-testid='document-editor-exhibits']").ClickAsync();
        await Assertions.Expect(editor.Locator(".tm-document-editor__document-title")).ToContainTextAsync("Evidence exhibit");
        var imageCount = await editor.Locator("img.tm-document-image__media").CountAsync();
        Assert.IsTrue(imageCount >= 2, $"Expected at least 2 exhibit images, found {imageCount}.");
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_RendersInDarkModeAndMobileViewport()
    {
        var page = await OpenDocumentEditorPageAsync();

        await page.Locator("button[aria-label='Switch to dark mode']").Last.ClickAsync();
        await Assertions.Expect(page.Locator("[data-theme='dark']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".tm-document-surface")).ToBeVisibleAsync();

        await page.SetViewportSizeAsync(390, 900);
        await WaitForAppReadyAsync(page);

        var surface = page.Locator(".tm-document-surface");
        await Assertions.Expect(surface).ToBeVisibleAsync();
        var firstParagraph = await page.Locator("[data-testid='document-paragraph-editor']").First.InputValueAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(firstParagraph), "Expected the first document paragraph to be editable and non-empty.");
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanEditSaveAndReloadThroughDemoApi()
    {
        var page = await OpenDocumentEditorPageAsync();

        var editedText = $"Saved paragraph {DateTimeOffset.UtcNow:HHmmss}";
        await page.Locator("[data-testid='document-paragraph-editor']").First.FillAsync(editedText);

        await page.Locator("[data-testid='document-insert-menu']").ClickAsync();
        await page.Locator("[data-testid='document-open-image-dialog']").ClickAsync();
        await page.Locator("[data-testid='document-image-url-input']").FillAsync("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        await page.Locator("[data-testid='document-insert-image-url']").ClickAsync();

        await page.Locator("[data-testid='document-save']").ClickAsync();
        await Assertions.Expect(page.Locator(".tm-document-editor__save-message")).ToContainTextAsync("Saved");

        await ReloadDocumentEditorPageAsync(page);

        await Assertions.Expect(page.Locator("[data-testid='document-paragraph-editor']").First).ToHaveValueAsync(editedText);
        var imageCount = await page.Locator("img.tm-document-image__media").CountAsync();
        Assert.IsTrue(imageCount >= 3, $"Expected at least 3 document images after reload, found {imageCount}.");
    }

    [TestMethod]
    [Description("Document editor supports click-to-edit typing and Ctrl+S save status")]
    public async Task DocumentEditor_DemoPage_CanTypeParagraphAndSaveWithCtrlS()
    {
        var page = await OpenDocumentEditorPageAsync();

        var typedText = $"Typed with keyboard {DateTimeOffset.UtcNow:HHmmss}";
        var paragraph = page.Locator("[data-testid='document-paragraph-editor']").First;
        await paragraph.ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.InsertTextAsync(typedText);
        await page.Keyboard.PressAsync("Control+S");

        await Assertions.Expect(page.Locator("[data-testid='document-save-message']")).ToContainTextAsync("Saved");

        await ReloadDocumentEditorPageAsync(page);
        await Assertions.Expect(page.Locator("[data-testid='document-paragraph-editor']").First).ToHaveValueAsync(typedText);
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_ToolbarUndoRedoAndKeyboardShortcutsWork()
    {
        var page = await OpenDocumentEditorPageAsync();

        var paragraph = page.Locator("[data-testid='document-paragraph-editor']").First;
        var originalText = await paragraph.InputValueAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(originalText), "Expected the first paragraph to contain editable text.");

        await paragraph.FillAsync("Toolbar undo text");
        await paragraph.EvaluateAsync("element => element.blur()");
        await page.Locator("[data-testid='document-undo']").ClickAsync();
        await Assertions.Expect(paragraph).ToHaveValueAsync(originalText);

        await page.Locator("[data-testid='document-redo']").ClickAsync();
        await Assertions.Expect(paragraph).ToHaveValueAsync("Toolbar undo text");

        await paragraph.FillAsync("Keyboard undo text");
        await paragraph.EvaluateAsync("element => element.blur()");
        await page.Keyboard.PressAsync("Control+Z");
        await Assertions.Expect(paragraph).ToHaveValueAsync("Toolbar undo text");

        await page.Keyboard.PressAsync("Control+Y");
        await Assertions.Expect(paragraph).ToHaveValueAsync("Keyboard undo text");

        var savedText = $"Keyboard saved {DateTimeOffset.UtcNow:HHmmss}";
        await paragraph.FillAsync(savedText);
        await page.Keyboard.PressAsync("Control+S");
        await Assertions.Expect(page.Locator("[data-testid='document-save-message']")).ToContainTextAsync("Saved");

        await ReloadDocumentEditorPageAsync(page);
        await Assertions.Expect(page.Locator("[data-testid='document-paragraph-editor']").First).ToHaveValueAsync(savedText);
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanCreateMajorVersionThroughDemoApi()
    {
        var page = await OpenDocumentEditorPageAsync();
        var label = $"E2E major {DateTimeOffset.UtcNow:HHmmss}";

        await page.Locator("[data-testid='document-version-create-open']").ClickAsync();
        await page.Locator("[data-testid='document-version-kind']").SelectOptionAsync("Major");
        await page.Locator("[data-testid='document-version-label']").FillAsync(label);
        await page.Locator("[data-testid='document-version-description']").FillAsync("Major version created by Playwright.");
        await page.Locator("[data-testid='document-version-create-submit']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-version-status']")).ToContainTextAsync("Version created");
        await Assertions.Expect(page.Locator("[data-testid='document-version-list']")).ToContainTextAsync(label);

        await ReloadDocumentEditorPageAsync(page);
        await Assertions.Expect(page.Locator("[data-testid='document-version-list']")).ToContainTextAsync(label);
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanCreateSigningTemplateFromRendition()
    {
        var page = await OpenDocumentEditorPageAsync();

        await page.Locator("[data-testid='document-create-signing-template']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-signing-message']")).ToContainTextAsync("Signing rendition ready");
        await page.Locator("[data-testid='document-open-signing-designer']").ClickAsync();
        await page.WaitForURLAsync("**/signing-components?renditionId=**", new PageWaitForURLOptions { Timeout = 60000 });
        await Assertions.Expect(page.Locator("[data-testid='pdf-template-designer']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='pdf-template-designer-status']")).ToContainTextAsync("Loaded rendition");
        await Assertions.Expect(page.Locator("[data-testid='pdf-template-designer-status']")).ToContainTextAsync("designer field");
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanAddCommentAndReloadThroughDemoApi()
    {
        var page = await OpenDocumentEditorPageAsync();
        var text = $"E2E comment {DateTimeOffset.UtcNow:HHmmss}";

        var paragraph = page.Locator("[data-testid='document-paragraph-editor']").First;
        await BeginBlockCommentAsync(page, paragraph);
        await page.Locator("[data-testid='document-comment-input']").FillAsync(text);
        await page.Locator("[data-testid='document-comment-submit']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-comment-status-message']")).ToContainTextAsync("Comment added");
        await Assertions.Expect(page.Locator("[data-testid='document-comment-list']")).ToContainTextAsync(text);

        await ReloadDocumentEditorPageAsync(page);
        await Assertions.Expect(page.Locator("[data-testid='document-comment-list']")).ToContainTextAsync(text);
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_ClientCanCommentInReadOnlyMode()
    {
        var page = await OpenDocumentEditorPageAsync();
        var text = $"Client review {DateTimeOffset.UtcNow:HHmmss}";

        await page.Locator("[data-testid='document-editor-readonly']").CheckAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-editor-demo']")).ToHaveClassAsync(new Regex("tm-document-editor--readonly"));

        await page.Locator("[data-testid='document-comment-new']").ClickAsync();
        await page.Locator("[data-testid='document-comment-input']").FillAsync(text);
        await page.Locator("[data-testid='document-comment-submit']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-comment-status-message']")).ToContainTextAsync("Comment added");
        await Assertions.Expect(page.Locator("[data-testid='document-comment-list']")).ToContainTextAsync(text);
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanResolveCommentThroughDemoApi()
    {
        var page = await OpenDocumentEditorPageAsync();
        var text = $"Resolve me {DateTimeOffset.UtcNow:HHmmss}";

        var paragraph = page.Locator("[data-testid='document-paragraph-editor']").First;
        await BeginBlockCommentAsync(page, paragraph);
        await page.Locator("[data-testid='document-comment-input']").FillAsync(text);
        await page.Locator("[data-testid='document-comment-submit']").ClickAsync();

        var thread = page.Locator("[data-testid='document-comment-thread']").Filter(new() { HasText = text }).First;
        await Assertions.Expect(thread).ToBeVisibleAsync();
        await thread.Locator("[data-testid='document-comment-resolve']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-comment-status-message']")).ToContainTextAsync("Comment resolved");
        await Assertions.Expect(thread.Locator("[data-testid='document-comment-status']")).ToContainTextAsync("Resolved");
    }

    [TestMethod]
    [Description("Document editor comments support block selection, replies, and resolving threads")]
    public async Task DocumentEditor_DemoPage_CanReplyAndResolveCommentThreadThroughDemoApi()
    {
        var page = await OpenDocumentEditorPageAsync();
        var text = $"Thread root {DateTimeOffset.UtcNow:HHmmss}";
        var reply = $"Thread reply {DateTimeOffset.UtcNow:HHmmss}";

        var paragraph = page.Locator("[data-testid='document-paragraph-editor']").First;
        await BeginBlockCommentAsync(page, paragraph);
        await page.Locator("[data-testid='document-comment-input']").FillAsync(text);
        await page.Locator("[data-testid='document-comment-submit']").ClickAsync();

        var thread = page.Locator("[data-testid='document-comment-thread']").Filter(new() { HasText = text }).First;
        await Assertions.Expect(thread).ToBeVisibleAsync();
        await thread.Locator("[data-testid='document-comment-reply-input']").FillAsync(reply);
        await thread.Locator("[data-testid='document-comment-reply-submit']").ClickAsync();

        await Assertions.Expect(thread).ToContainTextAsync(reply);
        await Assertions.Expect(page.Locator("[data-testid='document-comment-status-message']")).ToContainTextAsync("Reply added");

        await thread.Locator("[data-testid='document-comment-resolve']").ClickAsync();
        await Assertions.Expect(thread.Locator("[data-testid='document-comment-status']")).ToContainTextAsync("Resolved");
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanInsertTokenAndPreviewResolvedValue()
    {
        var page = await OpenDocumentEditorPageAsync();
        var paragraph = page.Locator("[data-testid='document-paragraph-editor']").First;

        await paragraph.FillAsync("Dear {{cl");

        var tokenItem = page.Locator(".tm-rte-token-item").First;
        await Assertions.Expect(tokenItem).ToContainTextAsync("client.name");
        await tokenItem.DispatchEventAsync("mousedown");

        await Assertions.Expect(page.Locator("[data-testid='document-edit-token-chip']").First).ToContainTextAsync("Client name");
        await Assertions.Expect(paragraph).ToHaveValueAsync("Dear Client name");

        await page.Locator("[data-testid='document-template-preview']").ClickAsync();
        await Assertions.Expect(page.Locator(".tm-document-surface")).ToContainTextAsync("ACME Ltd.");
        await Assertions.Expect(page.Locator("[data-testid='document-template-preview-message']")).ToContainTextAsync("Template preview");

        await page.Locator("[data-testid='document-template-preview']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-paragraph-editor']").First).ToHaveValueAsync("Dear Client name");
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanShowVersionDiff()
    {
        var page = await OpenDocumentEditorPageAsync();
        var paragraph = page.Locator("[data-testid='document-paragraph-editor']").First;
        var suffix = DateTimeOffset.UtcNow.ToString("HHmmss");

        await paragraph.FillAsync($"Diff base {suffix}");
        await page.Locator("[data-testid='document-save']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-save-message']")).ToContainTextAsync("Saved");
        await CreateMajorVersionAsync(page, $"Base {suffix}", "Base version for diff.");

        await paragraph.FillAsync($"Diff compare {suffix}");
        await page.Locator("[data-testid='document-save']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-save-message']")).ToContainTextAsync("Saved");
        await CreateMajorVersionAsync(page, $"Compare {suffix}", "Compare version for diff.");

        await Assertions.Expect(page.Locator("[data-testid='document-version-panel']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-version-list']")).ToBeVisibleAsync();
        await page.Locator("[data-testid='document-version-item']").First.ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-version-preview-state']")).ToContainTextAsync("Previewing");
        await page.Locator("[data-testid='document-version-current']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-version-preview-state']")).ToBeHiddenAsync();

        await page.Locator("[data-testid='document-version-diff-compare']").First.ClickAsync();
        await page.Locator("[data-testid='document-version-diff-base']").Nth(1).ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-diff-viewer']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-diff-summary']")).ToContainTextAsync("added");
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanInsertImageThroughProvider()
    {
        var page = await OpenDocumentEditorPageAsync();
        var before = await page.Locator("img.tm-document-image__media").CountAsync();

        await page.Locator("[data-testid='document-insert-menu']").ClickAsync();
        await page.Locator("[data-testid='document-open-image-dialog']").ClickAsync();
        await page.Locator("[data-testid='document-upload-demo-image']").ClickAsync();

        await page.WaitForFunctionAsync(
            "count => document.querySelectorAll('img.tm-document-image__media').length > count",
            before);
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanPasteClipboardImage()
    {
        var page = await OpenDocumentEditorPageAsync();
        var before = await page.Locator("img.tm-document-image__media").CountAsync();

        await page.Locator(".tm-document-surface").EvaluateAsync(
            """
            element => {
                const bytes = Uint8Array.from(atob("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="), c => c.charCodeAt(0));
                const file = new File([bytes], "clipboard.png", { type: "image/png" });
                const data = new DataTransfer();
                data.items.add(file);
                const event = new Event("paste", { bubbles: true, cancelable: true });
                Object.defineProperty(event, "clipboardData", { value: data });
                element.dispatchEvent(event);
            }
            """);

        await page.WaitForFunctionAsync(
            "count => document.querySelectorAll('img.tm-document-image__media').length > count",
            before);
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanExportDocxAndOdt()
    {
        var page = await OpenDocumentEditorPageAsync();

        var docxDownloadTask = page.WaitForDownloadAsync();
        await page.Locator("[data-testid='document-export-docx']").ClickAsync();
        var docx = await docxDownloadTask;
        Assert.IsTrue(docx.SuggestedFilename.EndsWith(".docx", StringComparison.OrdinalIgnoreCase));

        var odtDownloadTask = page.WaitForDownloadAsync();
        await page.Locator("[data-testid='document-export-odt']").ClickAsync();
        var odt = await odtDownloadTask;
        Assert.IsTrue(odt.SuggestedFilename.EndsWith(".odt", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanImportDocxAndOdt()
    {
        var page = await OpenDocumentEditorPageAsync();
        var docxPath = await CreateDocxFixtureAsync("Imported E2E DOCX");
        var odtPath = await CreateOdtFixtureAsync("Imported E2E ODT");

        await page.Locator("[data-testid='document-import-docx']").SetInputFilesAsync(docxPath);
        await Assertions.Expect(page.Locator("[data-testid='document-format-message']")).ToContainTextAsync("Imported");
        await Assertions.Expect(page.Locator("[data-testid='document-editor-loaded']")).ToContainTextAsync("Imported E2E DOCX");

        await page.Locator("[data-testid='document-import-odt']").SetInputFilesAsync(odtPath);
        await Assertions.Expect(page.Locator("[data-testid='document-format-message']")).ToContainTextAsync("Imported");
        await Assertions.Expect(page.Locator("[data-testid='document-editor-loaded']")).ToContainTextAsync("Imported E2E ODT");
    }

    [TestMethod]
    [Description("Document editor desktop view remains screenshotable")]
    public async Task DocumentEditor_DemoPage_DesktopScreenshot()
    {
        var page = await OpenDocumentEditorPageAsync(1366, 900);

        await Assertions.Expect(page.Locator("[data-testid='document-editor-demo']")).ToBeVisibleAsync();
        await TakeScreenshotAsync(page, "document-editor-phase18-desktop");
    }

    [TestMethod]
    [Description("Document editor tablet view remains screenshotable")]
    public async Task DocumentEditor_DemoPage_TabletScreenshot()
    {
        var page = await OpenDocumentEditorPageAsync(834, 1112);

        await Assertions.Expect(page.Locator("[data-testid='document-editor-demo']")).ToBeVisibleAsync();
        await TakeScreenshotAsync(page, "document-editor-phase18-tablet");
    }

    [TestMethod]
    [Description("Document editor mobile view remains screenshotable")]
    public async Task DocumentEditor_DemoPage_MobileScreenshot()
    {
        var page = await OpenDocumentEditorPageAsync(390, 844);

        await Assertions.Expect(page.Locator("[data-testid='document-editor-demo']")).ToBeVisibleAsync();
        await TakeScreenshotAsync(page, "document-editor-phase18-mobile");
    }

    [TestMethod]
    [Description("Document editor visual layout keeps ribbon, comment rail, captions, and large images from overlapping")]
    public async Task DocumentEditor_DemoPage_VisualLayoutKeepsPanelsAndImagesStable()
    {
        var page = await OpenDocumentEditorPageAsync(1366, 900);

        await InsertLargeImageAsync(page);
        await Assertions.Expect(page.Locator("img.tm-document-image__media").Last).ToBeVisibleAsync();

        var layoutProblems = await page.EvaluateAsync<string[]>(
            """
            () => {
                const problems = [];
                const intersects = (a, b) =>
                    a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;
                const visibleRect = selector => {
                    const element = document.querySelector(selector);
                    if (!element) {
                        return null;
                    }
                    const rect = element.getBoundingClientRect();
                    return rect.width > 0 && rect.height > 0 ? rect : null;
                };

                const ribbon = visibleRect('.tm-document-editor__ribbon');
                const pageSurface = visibleRect('.tm-document-editor__page-surface');
                const commentRail = visibleRect('.tm-document-editor__comment-rail');

                if (ribbon && pageSurface && intersects(ribbon, pageSurface)) {
                    problems.push('Ribbon overlaps the document page.');
                }

                if (commentRail && pageSurface && intersects(commentRail, pageSurface)) {
                    problems.push('Comment rail overlaps the document page.');
                }

                document.querySelectorAll('img.tm-document-image__media').forEach((image, index) => {
                    const rect = image.getBoundingClientRect();
                    if (pageSurface && rect.right > pageSurface.right + 1) {
                        problems.push(`Image ${index + 1} overflows the page horizontally.`);
                    }
                    if (pageSurface && rect.width > pageSurface.width + 1) {
                        problems.push(`Image ${index + 1} is wider than the page surface.`);
                    }
                });

                document.querySelectorAll('.tm-document-image').forEach((figure, figureIndex) => {
                    const caption = figure.querySelector('figcaption');
                    if (!caption) {
                        return;
                    }
                    const captionRect = caption.getBoundingClientRect();
                    figure.querySelectorAll('.tm-document-image__resize-handle, [data-testid="document-image-resize-handle"]').forEach((handle, handleIndex) => {
                        if (intersects(captionRect, handle.getBoundingClientRect())) {
                            problems.push(`Image ${figureIndex + 1} resize handle ${handleIndex + 1} overlaps the caption.`);
                        }
                    });
                });

                return problems;
            }
            """);

        Assert.AreEqual(0, layoutProblems.Length, string.Join(Environment.NewLine, layoutProblems));
    }

    private async Task<IPage> OpenDocumentEditorPageAsync(int width = 1280, int height = 720)
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

    private static async Task ReloadDocumentEditorPageAsync(IPage page)
    {
        await page.ReloadAsync(new PageReloadOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);
    }

    private static async Task WaitForDocumentEditorReadyAsync(IPage page)
    {
        await page.WaitForSelectorAsync("[data-testid='document-editor-demo']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
        await page.WaitForSelectorAsync("[data-testid='document-paragraph-editor']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
    }

    private static async Task CreateMajorVersionAsync(IPage page, string label, string description)
    {
        await page.Locator("[data-testid='document-version-create-open']").ClickAsync();
        await page.Locator("[data-testid='document-version-kind']").SelectOptionAsync("Major");
        await page.Locator("[data-testid='document-version-label']").FillAsync(label);
        await page.Locator("[data-testid='document-version-description']").FillAsync(description);
        await page.Locator("[data-testid='document-version-create-submit']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-version-status']")).ToContainTextAsync("Version created");
    }

    private static async Task BeginBlockCommentAsync(IPage page, ILocator blockEditor)
    {
        await blockEditor.ClickAsync();
        var opened = await blockEditor.EvaluateAsync<bool>(
            """
            element => {
                const block = element.closest('.tm-document-editable-block');
                const button = block?.querySelector('[data-testid="document-block-comment"]');
                if (button instanceof HTMLElement) {
                    button.click();
                    return true;
                }

                return false;
            }
            """);

        Assert.IsTrue(opened, "Expected the active document block to expose the comment command.");
        await Assertions.Expect(page.Locator("[data-testid='document-comment-input']")).ToBeVisibleAsync();
    }

    private static async Task InsertLargeImageAsync(IPage page)
    {
        const string imageUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";
        var before = await page.Locator("img.tm-document-image__media").CountAsync();

        await page.Locator("[data-testid='document-insert-menu']").ClickAsync();
        await page.Locator("[data-testid='document-open-image-dialog']").ClickAsync();
        await page.Locator("[data-testid='document-image-url-input']").FillAsync(imageUrl);
        await page.Locator("[data-testid='document-insert-image-url']").ClickAsync();

        await page.WaitForFunctionAsync(
            "count => document.querySelectorAll('img.tm-document-image__media').length > count",
            before);

        var widthInput = page.Locator(".tm-document-image-editor__fields input[type='number']").Last;
        await widthInput.FillAsync("2400");
        await widthInput.EvaluateAsync("element => element.blur()");
    }

    private static async Task<string> CreateDocxFixtureAsync(string title)
    {
        var path = Path.Combine(Path.GetTempPath(), $"tempo-doc-editor-{Guid.NewGuid():N}.docx");
        var exported = await new DocumentDocxExporter().ExportAsync(CreateImportFixture(title));
        await File.WriteAllBytesAsync(path, exported.Content);
        return path;
    }

    private static async Task<string> CreateOdtFixtureAsync(string title)
    {
        var path = Path.Combine(Path.GetTempPath(), $"tempo-doc-editor-{Guid.NewGuid():N}.odt");
        var exported = await new DocumentOdtExporter().ExportAsync(CreateImportFixture(title));
        await File.WriteAllBytesAsync(path, exported.Content);
        return path;
    }

    private static DocumentEditorDocument CreateImportFixture(string title)
    {
        var document = DocumentEditorDocument.Empty();
        document.Metadata.Title = title;
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Heading,
            Order = 0,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines = [new TextRun { Text = title }]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "Imported from Playwright." }]
            }
        });
        return document;
    }
}
