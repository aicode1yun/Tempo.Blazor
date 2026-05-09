using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class SigningFormRunnerE2ETests : WasmTestBase
{
    [TestMethod]
[Description("Signing form runner completes the required text, delivery, number, conditional note, and signature flow")]
    public async Task SigningFormRunner_CompletesRequiredFlow()
    {
        var page = await OpenRunnerAsync();
        var runner = GetRunner(page);
        var panel = GetDesktopPanel(runner);

        await panel.Locator("input.tm-signing-text-step__input").FillAsync("Alex Johnson");
        await panel.Locator(".tm-signing-form-runner__next").ClickAsync();

        await Assertions.Expect(panel.Locator(".tm-signing-step-shell__title")).ToContainTextAsync("Delivery method");
        await panel.Locator("select.tm-signing-choice-step__select").SelectOptionAsync("paper");
        await panel.Locator(".tm-signing-form-runner__next").ClickAsync();

        await panel.Locator("input.tm-signing-number-step__input").FillAsync("100");
        await panel.Locator(".tm-signing-form-runner__next").ClickAsync();

        await panel.Locator("input.tm-signing-choice-step__checkbox").CheckAsync();
        await panel.Locator(".tm-signing-form-runner__next").ClickAsync();

        await Assertions.Expect(panel.Locator(".tm-signing-step-shell__title")).ToContainTextAsync("Internal note");
        await panel.Locator("input.tm-signing-text-step__input").FillAsync("Board approved");
        await panel.Locator(".tm-signing-form-runner__next").ClickAsync();

        await Assertions.Expect(panel.Locator(".tm-signing-step-shell__title")).ToContainTextAsync("Signing date");
        await panel.Locator("input.tm-signing-date-step__input").FillAsync("2026-05-08");
        await panel.Locator(".tm-signing-form-runner__next").ClickAsync();

        var signatureInput = panel.Locator("input.tm-signature-capture__typed-input");
        await signatureInput.FillAsync("Alex Johnson");
        await signatureInput.PressAsync("Tab");
        await Assertions.Expect(panel.Locator(".tm-signing-form-runner__complete")).ToBeEnabledAsync();
        await panel.Locator(".tm-signing-form-runner__complete").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='signing-runner-status']")).ToContainTextAsync("Completed");
    }

    [TestMethod]
    [Description("Signing form runner keeps the signer on the first invalid required step")]
    public async Task SigningFormRunner_BlocksEmptyRequiredStep()
    {
        var page = await OpenRunnerAsync();
        var panel = GetDesktopPanel(GetRunner(page));

        await panel.Locator(".tm-signing-form-runner__next").ClickAsync();

        await Assertions.Expect(panel.Locator(".tm-signing-form-runner__validation")).ToContainTextAsync("Full name is required.");
        await Assertions.Expect(panel.Locator(".tm-signing-step-shell__title")).ToContainTextAsync("Full name");
    }

    [TestMethod]
    [Description("Signing form runner updates formula overlays as dependent numeric values change")]
    public async Task SigningFormRunner_UpdatesFormulaOverlay()
    {
        var page = await OpenRunnerAsync();
        var runner = GetRunner(page);
        var panel = GetDesktopPanel(runner);

        await panel.Locator("input.tm-signing-text-step__input").FillAsync("Alex Johnson");
        await panel.Locator(".tm-signing-form-runner__next").ClickAsync();
        await panel.Locator("select.tm-signing-choice-step__select").SelectOptionAsync("paper");
        await panel.Locator(".tm-signing-form-runner__next").ClickAsync();
        await panel.Locator("input.tm-signing-number-step__input").FillAsync("100");
        await panel.Locator(".tm-signing-form-runner__next").ClickAsync();

        await Assertions.Expect(runner.Locator(".tm-signing-field:has-text('Total with tax')")).ToContainTextAsync("121");
        await Assertions.Expect(page.Locator("[data-testid='signing-runner-status']")).ToContainTextAsync("total: 121");
    }

    [TestMethod]
    [Description("Signing form runner accessibility list can jump directly to a selected step")]
    public async Task SigningFormRunner_AccessibilityListJumpsToStep()
    {
        var page = await OpenRunnerAsync();
        var panel = GetDesktopPanel(GetRunner(page));

        await panel.Locator(".tm-signing-form-runner__accessibility-entry").ClickAsync();
        await panel.Locator(".tm-signing-form-runner__accessibility-field:has-text('Signature')").ClickAsync();

        await Assertions.Expect(panel.Locator("input.tm-signature-capture__typed-input")).ToBeVisibleAsync();
    }

    [TestMethod]
    [Description("Signing form runner keeps draw signature mode selected after completing a stroke")]
    public async Task SigningFormRunner_DrawSignatureModePersistsAfterMouseUp()
    {
        var page = await OpenRunnerAsync();
        var panel = GetDesktopPanel(GetRunner(page));

        await panel.Locator(".tm-signing-form-runner__accessibility-entry").ClickAsync();
        await panel.Locator(".tm-signing-form-runner__accessibility-field:has-text('Signature')").ClickAsync();
        await panel.GetByRole(AriaRole.Tab, new() { Name = "Draw" }).ClickAsync();

        var signatureCapture = panel.Locator(".tm-signature-capture").First;
        await Assertions.Expect(signatureCapture).ToHaveAttributeAsync("data-mode", "Draw");
        await DrawSignatureAsync(page, signatureCapture);

        await Assertions.Expect(signatureCapture).ToHaveAttributeAsync("data-mode", "Draw");
        await Assertions.Expect(panel.GetByRole(AriaRole.Tab, new() { Name = "Draw" })).ToHaveAttributeAsync("aria-selected", "true");
    }

    [TestMethod]
    [Description("Signing form runner exposes a collapsible mobile signing panel")]
    public async Task SigningFormRunner_MobilePanelCollapsesAndExpands()
    {
        var page = await OpenRunnerAsync(390, 820);
        var runner = GetRunner(page);

        await runner.Locator(".tm-signing-form-runner__mobile-minimize").ClickAsync();
        await Assertions.Expect(runner.Locator(".tm-signing-form-runner__mobile-expand")).ToBeVisibleAsync();

        await runner.Locator(".tm-signing-form-runner__mobile-expand").ClickAsync();
        await Assertions.Expect(runner.Locator(".tm-signing-form-runner__mobile-panel .tm-signing-form-runner__progress")).ToBeVisibleAsync();
    }

    private async Task<IPage> OpenRunnerAsync(int width = 1280, int height = 720)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);
        var runner = GetRunner(page);
        await runner.ScrollIntoViewIfNeededAsync();
        return page;
    }

    private static ILocator GetRunner(IPage page)
    {
        return page.Locator("[data-testid='signing-runner-demo']").First;
    }

    private static ILocator GetDesktopPanel(ILocator runner)
    {
        return runner.Locator(".tm-signing-form-runner__steps").First;
    }

    private static async Task DrawSignatureAsync(IPage page, ILocator signatureCapture)
    {
        var canvas = signatureCapture.Locator(".tm-signature-capture__canvas").First;
        await canvas.ScrollIntoViewIfNeededAsync();
        var box = await canvas.BoundingBoxAsync();
        Assert.IsNotNull(box, "Signature canvas should have a bounding box.");

        await page.Mouse.MoveAsync((float)(box!.X + box.Width * 0.2), (float)(box.Y + box.Height * 0.35));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(box.X + box.Width * 0.42), (float)(box.Y + box.Height * 0.25), new MouseMoveOptions { Steps = 4 });
        await page.Mouse.MoveAsync((float)(box.X + box.Width * 0.7), (float)(box.Y + box.Height * 0.45), new MouseMoveOptions { Steps = 4 });
        await page.Mouse.UpAsync();
    }
}
