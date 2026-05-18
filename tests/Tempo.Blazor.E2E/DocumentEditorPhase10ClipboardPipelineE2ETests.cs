using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 10 clipboard pipeline behavior.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase10ClipboardPipelineE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase10_WordListPasteShowsReportAndUndoesAsSingleTransaction()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        const string marker = "Phase10 Word List Item";
        const string html = """
            <html xmlns:w="urn:schemas-microsoft-com:office:word">
            <body>
            <p class="MsoListParagraph"><span style="mso-list:Ignore">•</span>Phase10 Word List Item</p>
            <script>alert(1)</script>
            </body></html>
            """;

        await DispatchClipboardPasteAsync(page, html, marker);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-paste-report']"))
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        await page.Keyboard.PressAsync("Control+Z");
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']").Filter(new() { HasText = marker }))
            .ToHaveCountAsync(0, new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase10_GoogleDocsHeadingSheetsTableAndUrlPaste()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        await DispatchClipboardPasteAsync(
            page,
            "<h2 id=\"docs-internal-guid-phase10\">Phase10 Docs Heading</h2>",
            "Phase10 Docs Heading");
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']").Filter(new() { HasText = "Phase10 Docs Heading" }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        await DispatchClipboardPasteAsync(page, null, "Phase10 Name\tScore\nAda\t99");
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table td").Filter(new() { HasText = "Ada" }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        await DispatchClipboardPasteAsync(page, null, "https://example.com/phase10");
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']").Filter(new() { HasText = "https://example.com/phase10" }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase10_BlockPasteCreatesTrackChangesRevision()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);
        await EnableTrackChangesAsync(page);

        const string marker = "Phase10 tracked paste";
        await DispatchClipboardPasteAsync(page, "<p>Phase10 tracked paste</p><p>Phase10 tracked paste tail</p>", marker);

        await Assertions.Expect(page.Locator("[data-testid='document-revision-item']").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase10_ClipboardImagePastePersistsAfterSaveAndReload()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        await DispatchClipboardImagePasteAsync(page, "phase10-clipboard.png");

        var image = page.Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image img[alt='phase10-clipboard.png']").First;
        await Assertions.Expect(image).ToBeVisibleAsync(new() { Timeout = 10000 });

        await SaveDocumentAsync(page);
        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForDocumentEditorReadyAsync(page);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image img[alt='phase10-clipboard.png']").First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    private static async Task EnableTrackChangesAsync(IPage page)
    {
        await page.Locator("[data-testid='document-ribbon-tab-review']").ClickAsync();
        var button = page.Locator("[data-testid='document-track-changes']");
        if (await button.GetAttributeAsync("aria-pressed") != "true")
        {
            await button.ClickAsync();
        }

        await Assertions.Expect(button).ToHaveClassAsync(new Regex("tm-document-editor__ribbon-button--active"), new() { Timeout = 5000 });
    }

    private static Task DispatchClipboardPasteAsync(IPage page, string? html, string plain)
    {
        return page.EvaluateAsync(
            """
            ({ html, plain }) => {
                const data = new DataTransfer();
                if (html) data.setData('text/html', html);
                data.setData('text/plain', plain || '');
                const event = new ClipboardEvent('paste', { bubbles: true, cancelable: true });
                Object.defineProperty(event, 'clipboardData', { value: data });
                const target = document.querySelector("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__body")
                    || document.querySelector("[data-testid='document-wysiwyg-host']");
                target.dispatchEvent(event);
            }
            """,
            new { html, plain });
    }

    private static Task DispatchClipboardImagePasteAsync(IPage page, string fileName)
    {
        return page.EvaluateAsync(
            """
            fileName => {
                const bytes = Uint8Array.from(atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII='), c => c.charCodeAt(0));
                const file = new File([bytes], fileName, { type: 'image/png' });
                const data = new DataTransfer();
                data.items.add(file);
                const event = new ClipboardEvent('paste', { bubbles: true, cancelable: true });
                Object.defineProperty(event, 'clipboardData', { value: data });
                const target = document.querySelector("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-page__body")
                    || document.querySelector("[data-testid='document-wysiwyg-host']");
                target.dispatchEvent(event);
            }
            """,
            fileName);
    }

    private static async Task SaveDocumentAsync(IPage page)
    {
        await page.Locator("[data-testid='document-save']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-save-message']"))
            .ToContainTextAsync("Saved", new() { Timeout = 10000 });
    }
}
