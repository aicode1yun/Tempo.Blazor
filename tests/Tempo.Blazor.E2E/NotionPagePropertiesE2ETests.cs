using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionPagePropertiesE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF15: Page Properties edit rows, Page Properties Report aggregates labelled pages, handles empty and missing-key states, captures UX baseline, and navigates to a source page.")]
    public async Task PageProperties_EditRowsReportEdgesAndNavigate()
    {
        var page = await OpenNotionEditorAsync();
        await SeedPagePropertiesPageAsync();

        var properties = page.Locator("[data-block-id='cf150000-0000-0000-0000-000000000010'] .tm-page-props").First;
        await properties.Locator(".tm-page-props__row").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual("Status", await properties.Locator(".tm-page-props__key-input").First.InputValueAsync());
        Assert.AreEqual("Docs team", await properties.Locator(".tm-page-props__value-input").Nth(1).InputValueAsync());
        Assert.IsTrue(await properties.Locator(".tm-page-props__table").EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Page properties table should fit the editor column at desktop width.");
        await CaptureBaselineAsync("page-properties", "cf15-page-properties-filled", properties);

        var emptyProperties = page.Locator("[data-block-id='cf150000-0000-0000-0000-000000000020'] .tm-page-props").First;
        await emptyProperties.Locator(".tm-page-props__empty").Filter(new() { HasText = "No properties yet" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await emptyProperties.Locator(".tm-page-props__add").ClickAsync();
        await emptyProperties.Locator(".tm-page-props__key-input").Last.FillAsync("Reviewer");
        await emptyProperties.Locator(".tm-page-props__key-input").Last.DispatchEventAsync("change");
        await emptyProperties.Locator(".tm-page-props__value-input").Last.FillAsync("Architecture");
        await emptyProperties.Locator(".tm-page-props__value-input").Last.DispatchEventAsync("change");
        Assert.AreEqual("Architecture", await emptyProperties.Locator(".tm-page-props__value-input").Last.InputValueAsync());
        await CaptureBaselineAsync("page-properties", "cf15-page-properties-added-row", emptyProperties);

        var report = page.Locator("[data-block-id='cf150000-0000-0000-0000-000000000030'] .tm-props-report").First;
        await report.Locator(".tm-props-report__page-link").Filter(new() { HasText = "CF15 Alpha Project" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await report.Locator(".tm-props-report__page-link").Filter(new() { HasText = "CF15 Beta Project" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual(2, await report.Locator(".tm-props-report__page-link").CountAsync());
        Assert.IsTrue(await report.Locator(".tm-props-report__value").Filter(new() { HasText = "Green" }).CountAsync() > 0);
        Assert.IsTrue(await report.Locator(".tm-props-report__value").Filter(new() { HasText = "Platform" }).CountAsync() > 0);
        Assert.IsTrue(await report.Locator(".tm-props-report__value").Filter(new() { HasText = "Medium" }).CountAsync() > 0);
        Assert.IsTrue(await report.Locator(".tm-props-report__missing").Filter(new() { HasText = "Not set" }).CountAsync() > 0);
        Assert.AreEqual(0, await report.Locator("text=CF15 Unmatched Archive").CountAsync());
        Assert.IsTrue(await report.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Properties report frame should not overflow horizontally.");
        await CaptureBaselineAsync("page-properties", "cf15-properties-report-labelled-pages", report);

        var missingValueRow = report.Locator("tbody tr").Filter(new() { HasText = "CF15 Beta Project" }).First;
        await missingValueRow.Locator(".tm-props-report__missing").Filter(new() { HasText = "Not set" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureBaselineAsync("page-properties", "cf15-missing-property-value", missingValueRow);

        var emptyReport = page.Locator("[data-block-id='cf150000-0000-0000-0000-000000000040'] .tm-props-report").First;
        await emptyReport.Locator(".tm-props-report__empty").Filter(new() { HasText = "No pages match this report." }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureBaselineAsync("page-properties", "cf15-properties-report-empty", emptyReport);

        var capture = await CaptureBaselineAsync("page-properties", "cf15-page-properties-report-baseline", page.Locator(".tm-notion-page").First);
        TestContext.WriteLine($"UX CF15 baseline captured: {capture.FullPagePath} / {capture.RegionPath}");

        await report.Locator(".tm-props-report__page-link").Filter(new() { HasText = "CF15 Alpha Project" }).ClickAsync();
        await page.Locator(".tm-notion-header-title").Filter(new() { HasText = "CF15 Alpha Project" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }
}
