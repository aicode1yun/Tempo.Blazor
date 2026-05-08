using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class SignatureCaptureE2ETests : WasmTestBase
{
    [TestMethod]
    [Description("Signature capture draw mode captures a mouse-drawn signature")]
    public async Task SignatureCapture_Draw_CapturesSignature()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);

        await DrawSignatureAsync(page, "[data-testid='signature-draw-capture']");

        await Expect(page.Locator("[data-testid='signature-draw-value']")).ToContainTextAsync("Value captured");
    }

    [TestMethod]
    [Description("Signature capture clear button clears a drawn signature")]
    public async Task SignatureCapture_Clear_ClearsSignature()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);

        await DrawSignatureAsync(page, "[data-testid='signature-draw-capture']");
        await Expect(page.Locator("[data-testid='signature-draw-value']")).ToContainTextAsync("Value captured");

        await page.Locator("[data-testid='signature-draw-capture'] .tm-signature-capture__clear").ClickAsync();

        await Expect(page.Locator("[data-testid='signature-draw-value']")).ToContainTextAsync("No value");
    }

    [TestMethod]
    [Description("Signature capture typed mode captures typed signature text")]
    public async Task SignatureCapture_Typed_CapturesTypedSignature()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);

        var input = page.Locator("[data-testid='signature-typed-capture'] .tm-signature-capture__typed-input").First;
        await input.ScrollIntoViewIfNeededAsync();
        await input.FillAsync("Alex Johnson");
        await input.PressAsync("Tab");

        await Expect(page.Locator("[data-testid='signature-typed-value']")).ToContainTextAsync("Typed value captured");
        await Expect(page.Locator("[data-testid='signature-typed-capture'] .tm-signature-capture__typed-preview")).ToContainTextAsync("Alex Johnson");
    }

    [TestMethod]
    [Description("Signature capture stays usable on mobile viewport")]
    public async Task SignatureCapture_MobileViewport_CapturesPointerSignature()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);

        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);

        await DrawSignatureAsync(page, "[data-testid='signature-draw-capture']");

        var box = await page.Locator("[data-testid='signature-draw-capture'] .tm-signature-capture__canvas").BoundingBoxAsync();
        Assert.IsNotNull(box, "Signature canvas should have a bounding box on mobile.");
        Assert.IsTrue(box!.Width > 250, "Signature canvas should remain wide enough on mobile.");
        Assert.IsTrue(box.Height > 100, "Signature canvas should remain tall enough on mobile.");
        await Expect(page.Locator("[data-testid='signature-draw-value']")).ToContainTextAsync("Value captured");
    }

    private static async Task DrawSignatureAsync(IPage page, string rootSelector)
    {
        var canvas = page.Locator($"{rootSelector} .tm-signature-capture__canvas").First;
        await canvas.ScrollIntoViewIfNeededAsync();
        var box = await canvas.BoundingBoxAsync();
        Assert.IsNotNull(box, "Signature canvas should have a bounding box.");

        var startX = (float)(box!.X + box.Width * 0.18);
        var startY = (float)(box.Y + box.Height * 0.62);
        await page.Mouse.MoveAsync(startX, startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(box.X + box.Width * 0.38), (float)(box.Y + box.Height * 0.34), new MouseMoveOptions { Steps = 4 });
        await page.Mouse.MoveAsync((float)(box.X + box.Width * 0.62), (float)(box.Y + box.Height * 0.58), new MouseMoveOptions { Steps = 4 });
        await page.Mouse.MoveAsync((float)(box.X + box.Width * 0.82), (float)(box.Y + box.Height * 0.36), new MouseMoveOptions { Steps = 4 });
        await page.Mouse.UpAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
}
