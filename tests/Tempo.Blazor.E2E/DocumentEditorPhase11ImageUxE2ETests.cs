using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 11 image UX.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase11ImageUxE2ETests : DocumentEditorE2ETestBase
{
    private const string TinyPngDataUrl =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    [TestMethod]
    public async Task Phase11_InsertImageUrlThroughSplitFlow()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        await page.Locator("[data-testid='document-ribbon-tab-insert']").ClickAsync();
        await page.Locator("[data-testid='document-toolbar-image']").ClickAsync();
        await page.Locator("[data-testid='document-image-insert-url']").ClickAsync();
        await page.Locator("[data-testid='document-wysiwyg-image-url-input']").FillAsync(TinyPngDataUrl);
        await page.Locator("[data-testid='document-wysiwyg-image-alt-input']").FillAsync("Phase 11 URL image");
        await page.Locator("[data-testid='document-wysiwyg-insert-image-url']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image img[alt='Phase 11 URL image']").First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase11_ImageInspectorUpdatesAltAndWrap()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);

        await InsertImageViaUrlFlowAsync(page, "Phase 11 original alt");

        var insertedFigure = page.Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image")
            .Filter(new() { Has = page.Locator("img[alt='Phase 11 original alt']") })
            .First;
        var objectId = await insertedFigure.GetAttributeAsync("data-object-id")
            ?? await insertedFigure.GetAttributeAsync("data-render-object-id")
            ?? throw new InvalidOperationException("Inserted image did not expose a stable object id.");
        var figure = page.Locator(
            $"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-object-id='{objectId}'], " +
            $"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-render-object-id='{objectId}']").First;
        await figure.ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-image-inspector']"))
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        await page.Locator("[data-testid='document-image-inspector-alt']").FillAsync("Phase 11 inspector alt");
        await page.Locator("[data-testid='document-image-inspector-alt']").DispatchEventAsync("change");
        await Assertions.Expect(figure.Locator("img"))
            .ToHaveAttributeAsync("alt", "Phase 11 inspector alt", new() { Timeout = 10000 });

        await figure.ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-image-inspector']"))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.Locator("[data-testid='document-image-inspector-wrap-square']").ClickAsync();
        await Assertions.Expect(figure)
            .ToHaveClassAsync(new System.Text.RegularExpressions.Regex("tm-wysiwyg-image--wrap-square"), new() { Timeout = 10000 });
    }

    private static async Task InsertImageViaUrlFlowAsync(IPage page, string altText)
    {
        await page.Locator("[data-testid='document-ribbon-tab-insert']").ClickAsync();
        await page.Locator("[data-testid='document-toolbar-image']").ClickAsync();
        await page.Locator("[data-testid='document-image-insert-url']").ClickAsync();
        await page.Locator("[data-testid='document-wysiwyg-image-url-input']").FillAsync(TinyPngDataUrl);
        await page.Locator("[data-testid='document-wysiwyg-image-alt-input']").FillAsync(altText);
        await page.Locator("[data-testid='document-wysiwyg-insert-image-url']").ClickAsync();
        await Assertions.Expect(page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image img[alt='{altText}']").First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
