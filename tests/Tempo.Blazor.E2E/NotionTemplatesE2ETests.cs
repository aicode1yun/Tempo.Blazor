using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionTemplatesE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF9: New page template gallery loads from Demo API, filters templates, applies selected blocks, and captures UX baseline.")]
    public async Task CF9_TemplateGallery_SelectsTemplateAndCreatesBlocks()
    {
        var page = await OpenNotionEditorAsync();
        await SeedEmptyPageAsync();

        await OpenTemplateGalleryAsync(page);
        await page.Locator("[data-template-id='project-plan']").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await page.Locator(".tm-ntg__search-input").FillAsync("project");
        await Assertions.Expect(page.Locator(".tm-ntg__card")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-template-id='project-plan']")).ToContainTextAsync("Project plan");

        var galleryCapture = await CaptureBaselineAsync("templates", "cf9-template-gallery-filtered", page.Locator(".tm-ntg").First);
        Assert.IsTrue(File.Exists(galleryCapture.RegionPath), "CF9 filtered template gallery baseline should be written.");

        await page.Locator("[data-template-id='project-plan'] .tm-ntg__use").ClickAsync();
        await page.WaitForSelectorAsync(".tm-ntg", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10000
        });

        await Assertions.Expect(page.Locator(".tm-notion-page")).ToContainTextAsync("Project plan", new LocatorAssertionsToContainTextOptions
        {
            Timeout = 10000
        });
        await Assertions.Expect(page.Locator(".tm-notion-page")).ToContainTextAsync("Launch checklist");
        Assert.IsTrue(await page.Locator(".tm-notion-block").CountAsync() >= 2, "Template should create multiple page blocks.");

        var pageCapture = await CaptureBaselineAsync("templates", "cf9-template-page-created", page.Locator(".tm-notion-editor").First);
        Assert.IsTrue(File.Exists(pageCapture.RegionPath), "CF9 created template page baseline should be written.");
    }

    [TestMethod]
    [Description("CF9: Template gallery handles no provider, no matching search, and empty fixed categories.")]
    public async Task CF9_TemplateGallery_EdgeStates()
    {
        var page = await OpenNotionEditorAsync("?disableTemplateProvider=true");
        await SeedEmptyPageAsync();

        await OpenTemplateGalleryAsync(page);
        await Assertions.Expect(page.Locator(".tm-ntg__card")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator(".tm-ntg")).ToContainTextAsync("Blank page");
        Assert.AreEqual(0, await page.Locator("[data-template-id='project-plan']").CountAsync(),
            "Providerless gallery should not render API-backed templates.");
        await page.Locator("[data-template-id='blank'] .tm-ntg__use").ClickAsync();
        await page.WaitForSelectorAsync(".tm-ntg", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10000
        });
        await Assertions.Expect(page.Locator(".tm-notion-page__empty-hint")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
        {
            Timeout = 10000
        });

        page = await OpenNotionEditorAsync();
        await SeedEmptyPageAsync();
        await OpenTemplateGalleryAsync(page);
        await page.Locator(".tm-ntg__search-input").FillAsync("zz-no-template");
        await Assertions.Expect(page.Locator(".tm-ntg__state")).ToContainTextAsync("No templates match");

        await page.Locator(".tm-ntg__search-input").FillAsync(string.Empty);
        await page.Locator(".tm-ntg__category", new PageLocatorOptions { HasTextString = "Knowledge" }).First.ClickAsync();
        await Assertions.Expect(page.Locator(".tm-ntg__state")).ToContainTextAsync("No templates match");
    }

    private static async Task OpenTemplateGalleryAsync(IPage page)
    {
        await page.Locator(".tm-ns-btn-new").First.ClickAsync();
        await page.WaitForSelectorAsync(".tm-ntg", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }
}
