using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class SigningStepsE2ETests : WasmTestBase
{
    [TestMethod]
    [Description("Signing steps accept text, number, and date values")]
    public async Task SigningSteps_FillsTextNumberAndDate()
    {
        var page = await OpenStepsAsync();
        var steps = GetSteps(page);

        await steps.Locator("[data-testid='signing-step-text'] input").FillAsync("Alex Johnson");
        await steps.Locator("[data-testid='signing-step-number'] input").FillAsync("125.5");
        await steps.Locator("[data-testid='signing-step-date'] input").FillAsync("2026-05-08");

        await Assertions.Expect(steps.Locator("[data-testid='signing-step-text'] input")).ToHaveValueAsync("Alex Johnson");
        await Assertions.Expect(steps.Locator("[data-testid='signing-step-number'] input")).ToHaveValueAsync("125.5");
        await Assertions.Expect(steps.Locator("[data-testid='signing-step-date'] input")).ToHaveValueAsync("2026-05-08");
    }

    [TestMethod]
    [Description("Signing steps select single, radio, and multiple choices")]
    public async Task SigningSteps_SelectsChoices()
    {
        var page = await OpenStepsAsync();
        var steps = GetSteps(page);

        await steps.Locator("[data-testid='signing-step-select'] select").SelectOptionAsync("option-2");
        await steps.Locator("[data-testid='signing-step-radio'] input[type='radio'][value='option-1']").CheckAsync();
        await steps.Locator("[data-testid='signing-step-multiple'] input[type='checkbox'][value='option-1']").CheckAsync();
        await steps.Locator("[data-testid='signing-step-multiple'] input[type='checkbox'][value='option-3']").CheckAsync();

        await Assertions.Expect(steps.Locator("[data-testid='signing-step-select'] select")).ToHaveValueAsync("option-2");
        await Assertions.Expect(page.Locator("[data-testid='signing-steps-status']")).ToContainTextAsync("Multiple: 2");
    }

    [TestMethod]
    [Description("Signing image step exposes a file upload and remove flow")]
    public async Task SigningSteps_UploadsAndRemovesImage()
    {
        var page = await OpenStepsAsync();
        var steps = GetSteps(page);
        var image = steps.Locator("[data-testid='signing-step-image']");

        await Assertions.Expect(image.Locator("input[type='file']")).ToHaveCountAsync(1);
        await image.Locator(".tm-signing-attachment-step__remove").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='signing-steps-status']")).ToContainTextAsync("Attachments: 0");
    }

    [TestMethod]
    [Description("Signing phone step sends code and shows OTP state")]
    public async Task SigningSteps_PhoneShowsOtpAfterSendCode()
    {
        var page = await OpenStepsAsync();
        var steps = GetSteps(page);
        var phone = steps.Locator("[data-testid='signing-step-phone']");

        await phone.Locator("input[type='tel']").ClickAsync();
        await page.Keyboard.TypeAsync("777 123 456");
        await page.WaitForTimeoutAsync(300);
        await phone.Locator(".tm-signing-phone-step__send").ClickAsync();

        await Assertions.Expect(phone.Locator(".tm-signing-phone-step__otp")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='signing-steps-status']")).ToContainTextAsync("+420777123456");
    }

    [TestMethod]
    [Description("Signing payment placeholder invokes checkout callback")]
    public async Task SigningSteps_PaymentCheckoutUpdatesStatus()
    {
        var page = await OpenStepsAsync();
        var steps = GetSteps(page);

        await steps.Locator("[data-testid='signing-step-payment'] .tm-signing-external-step__checkout").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='signing-steps-status']")).ToContainTextAsync("Checkout: Payment");
    }

    private async Task<IPage> OpenStepsAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);
        var steps = GetSteps(page);
        await steps.ScrollIntoViewIfNeededAsync();
        return page;
    }

    private static ILocator GetSteps(IPage page)
    {
        return page.Locator("[data-testid='signing-steps-demo']").First;
    }
}
