using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 18 developer debug surfaces.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase18DebugE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase18_DebugJsonInspector_OpensWithCanonicalDocumentAndRuntimeState()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await page.Locator("[data-testid='document-ribbon-tab-view']").ClickAsync();
        await page.Locator("[data-testid='document-view-json']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-json-debug-modal']"))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-json-debug-content']"))
            .ToContainTextAsync("contract-demo", new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-runtime-debug-content']"))
            .ToContainTextAsync("JsCanonicalBoundary", new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-json-debug-copy']"))
            .ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task Phase18_ClipboardDebugView_ShowsRawNormalizedAndWarnings()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        const string marker = "Phase18 clipboard debug marker";
        const string html = """
            <p>Phase18 clipboard debug marker <a href="javascript:alert(1)">unsafe link</a></p>
            <script>alert('phase18')</script>
            """;

        await DispatchClipboardPasteAsync(page, html, marker);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']").Filter(new() { HasText = marker }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        await page.Locator("[data-testid='document-ribbon-tab-view']").ClickAsync();
        await page.Locator("[data-testid='document-view-clipboard-html']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-clipboard-html-debug-modal']"))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-clipboard-html-debug-content']"))
            .ToContainTextAsync(marker, new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-clipboard-normalized-debug-content']"))
            .ToContainTextAsync(marker, new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-clipboard-warnings-debug-content']"))
            .ToContainTextAsync("unsafe-link-removed", new() { Timeout = 10000 });
    }

    private static Task DispatchClipboardPasteAsync(IPage page, string html, string plain)
    {
        return page.EvaluateAsync(
            """
            ({ html, plain }) => {
                const data = new DataTransfer();
                data.setData('text/html', html);
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
}
