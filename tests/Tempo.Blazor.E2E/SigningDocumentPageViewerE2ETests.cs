using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class SigningDocumentPageViewerE2ETests : WasmTestBase
{
    [TestMethod]
    [Description("Signing demo page renders document page image and overlay fields")]
    public async Task DocumentPageViewer_RendersPageImageAndOverlay()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);

        var viewer = page.Locator("[data-testid='signing-document-viewer']").First;
        await viewer.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var image = viewer.Locator("img.tm-document-page-viewer__image").First;
        Assert.IsTrue(await image.IsVisibleAsync(), "Document page image should be visible.");
        Assert.AreEqual("NDA page 1", await image.GetAttributeAsync("alt"));

        var pageElement = viewer.Locator(".tm-document-page-viewer__page").First;
        Assert.AreEqual("0", await pageElement.GetAttributeAsync("data-page-index"));

        var overlay = viewer.Locator("[data-testid='signature-overlay']").First;
        Assert.IsTrue(await overlay.IsVisibleAsync(), "Signature overlay should be visible.");

        var pageBox = await pageElement.BoundingBoxAsync();
        var overlayBox = await overlay.BoundingBoxAsync();

        Assert.IsNotNull(pageBox, "Document page should have a bounding box.");
        Assert.IsNotNull(overlayBox, "Signature overlay should have a bounding box.");
        Assert.IsTrue(overlayBox!.X >= pageBox!.X, "Overlay should start inside the page horizontally.");
        Assert.IsTrue(overlayBox.Y >= pageBox.Y, "Overlay should start inside the page vertically.");
        Assert.IsTrue(overlayBox.X + overlayBox.Width <= pageBox.X + pageBox.Width, "Overlay should end inside the page horizontally.");
        Assert.IsTrue(overlayBox.Y + overlayBox.Height <= pageBox.Y + pageBox.Height, "Overlay should end inside the page vertically.");

        await TakeScreenshotAsync(page, "signing_document_page_viewer_desktop");

        await page.SetViewportSizeAsync(390, 844);
        await page.WaitForTimeoutAsync(500);

        Assert.IsTrue(await viewer.IsVisibleAsync(), "Viewer should remain visible on mobile viewport.");
        Assert.IsTrue(await overlay.IsVisibleAsync(), "Signature overlay should remain visible on mobile viewport.");
        await TakeScreenshotAsync(page, "signing_document_page_viewer_mobile");
    }
}
