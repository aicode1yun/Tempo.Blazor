using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.E2E;

[TestClass]
[DoNotParallelize]
public class DocumentEditorE2ETests : WasmTestBase
{
    [TestMethod]
    public async Task DocumentEditor_DemoPage_RendersWysiwygShell()
    {
        var page = await OpenDocumentEditorPageAsync();
        var editor = page.Locator("[data-testid='document-editor-demo']");
        var host = editor.Locator("[data-testid='document-wysiwyg-host']");

        await Assertions.Expect(editor.Locator(".tm-document-editor__ribbon")).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator(".tm-document-editor__page-surface")).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator(".tm-document-editor__comment-rail")).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator(".tm-document-editor__version-panel")).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator(".tm-document-editor__document-title")).ToContainTextAsync("Service agreement");
        await WaitForWysiwygBodyAsync(host);
        await Assertions.Expect(host.Locator(".tm-wysiwyg-block").First).ToContainTextAsync(new Regex(@"\S"));
        await Assertions.Expect(page.Locator("[data-testid='document-editor-wysiwyg-mode']")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("[data-testid='document-paragraph-editor']")).ToHaveCountAsync(0);
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_CanSwitchDocumentsAndReadOnlyMode()
    {
        var page = await OpenDocumentEditorPageAsync();

        await page.Locator("[data-testid='document-editor-filing']").ClickAsync();
        await Assertions.Expect(page.Locator(".tm-document-editor__document-title")).ToContainTextAsync("Court filing");
        await WaitForWysiwygBodyAsync(page.Locator("[data-testid='document-wysiwyg-host']"));

        await page.Locator("[data-testid='document-editor-readonly']").CheckAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-editor-demo']")).ToHaveClassAsync(new Regex("tm-document-editor--readonly"));
        await Assertions.Expect(page.Locator(".tm-wysiwyg-page__body").First).ToHaveAttributeAsync("contenteditable", "false");

        await page.Locator("[data-testid='document-editor-exhibits']").ClickAsync();
        await Assertions.Expect(page.Locator(".tm-document-editor__document-title")).ToContainTextAsync("Evidence exhibit");
    }

    [TestMethod]
    public async Task DocumentEditor_DemoPage_RendersInDarkModeAndMobileViewport()
    {
        var page = await OpenDocumentEditorPageAsync();

        await page.Locator("button[aria-label='Switch to dark mode']").Last.ClickAsync();
        await Assertions.Expect(page.Locator("[data-theme='dark']")).ToBeVisibleAsync();
        await WaitForWysiwygBodyAsync(page.Locator("[data-testid='document-wysiwyg-host']"));

        await page.SetViewportSizeAsync(390, 900);
        await WaitForAppReadyAsync(page);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".tm-wysiwyg-page__body").First).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task DocumentEditor_Wysiwyg_CanTypeSaveAndReloadThroughDemoApi()
    {
        var page = await OpenDocumentEditorPageAsync();
        var host = page.Locator("[data-testid='document-wysiwyg-host']");
        var body = await WaitForWysiwygBodyAsync(host);
        var uniqueText = $" WYSIWYG saved {DateTimeOffset.UtcNow:HHmmssfff}";

        await body.ClickAsync();
        await page.Keyboard.TypeAsync(uniqueText);
        await Assertions.Expect(host).ToContainTextAsync(uniqueText);
        await page.WaitForTimeoutAsync(800);
        await Assertions.Expect(host).ToContainTextAsync(uniqueText);

        await page.Locator("[data-testid='document-save']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-save-message']")).ToContainTextAsync("Saved");

        await ReloadDocumentEditorPageAsync(page);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(uniqueText);
    }

    [TestMethod]
    public async Task DocumentEditor_Wysiwyg_CanPasteHtmlTable()
    {
        var page = await OpenDocumentEditorPageAsync();
        var host = page.Locator("[data-testid='document-wysiwyg-host']");
        await WaitForWysiwygBodyAsync(host);

        const string html = """
            <table>
              <tr><td colspan="2" rowspan="2">Excel merged</td><td>Right</td></tr>
              <tr><td>Bottom right</td></tr>
            </table>
            """;

        await DispatchClipboardPasteAsync(page, html, "Excel merged\tRight\nBottom right");

        var merged = host.Locator(".tm-wysiwyg-table td[colspan='2'][rowspan='2']").Filter(new() { HasText = "Excel merged" });
        await Assertions.Expect(merged).ToBeVisibleAsync();
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
        await page.WaitForSelectorAsync("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-block", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 60000
        });
    }

    private static async Task<ILocator> WaitForWysiwygBodyAsync(ILocator host)
    {
        await Assertions.Expect(host).ToBeVisibleAsync();
        var body = host.Locator(".tm-wysiwyg-page__body[contenteditable]").First;
        await body.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 60000 });
        return body;
    }

    private static async Task DispatchClipboardPasteAsync(IPage page, string? html, string plain)
    {
        await page.EvaluateAsync(
            """
            ({ html, plain }) => {
                const data = new DataTransfer();
                if (html) data.setData("text/html", html);
                data.setData("text/plain", plain || "");
                const event = new ClipboardEvent("paste", { bubbles: true, cancelable: true });
                Object.defineProperty(event, "clipboardData", { value: data });
                const target = document.querySelector("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__body")
                    || document.querySelector("[data-testid='document-wysiwyg-host']");
                target.dispatchEvent(event);
            }
            """,
            new { html, plain });
    }
}
