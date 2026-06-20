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

        await CaptureBaselineAndAssertAsync("cf9-template-gallery-grid", page.Locator(".tm-ntg").First);
        await CaptureBaselineAndAssertAsync("cf9-template-card-preview", page.Locator("[data-template-id='project-plan']").First);

        await page.Locator(".tm-ntg__category", new PageLocatorOptions { HasTextString = "Planning" }).First.ClickAsync();
        await Assertions.Expect(page.Locator(".tm-ntg__category--active")).ToContainTextAsync("Planning");
        await Assertions.Expect(page.Locator("[data-template-id='project-plan']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-template-id='decision-record']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-template-id='meeting-notes']")).ToHaveCountAsync(0);
        await CaptureBaselineAndAssertAsync("cf9-template-gallery-category-planning", page.Locator(".tm-ntg").First);

        await page.Locator(".tm-ntg__search-input").FillAsync("project");
        await Assertions.Expect(page.Locator(".tm-ntg__card")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-template-id='project-plan']")).ToContainTextAsync("Project plan");
        await CaptureBaselineAndAssertAsync("cf9-template-gallery-filtered", page.Locator(".tm-ntg").First);

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

        await CaptureBaselineAndAssertAsync("cf9-template-page-created", page.Locator(".tm-notion-editor").First);
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
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
        await CaptureBaselineAndAssertAsync("cf9-template-providerless-blank", page.Locator(".tm-ntg").First);

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
        await CaptureBaselineAndAssertAsync("cf9-template-search-empty-state", page.Locator(".tm-ntg").First);

        await page.Locator(".tm-ntg__search-input").FillAsync(string.Empty);
        await page.Locator(".tm-ntg__category", new PageLocatorOptions { HasTextString = "Knowledge" }).First.ClickAsync();
        await Assertions.Expect(page.Locator(".tm-ntg__state")).ToContainTextAsync("No templates match");
        await CaptureBaselineAndAssertAsync("cf9-template-category-empty-state", page.Locator(".tm-ntg").First);
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

    private async Task CaptureBaselineAndAssertAsync(string state, ILocator region)
    {
        await region.ScrollIntoViewIfNeededAsync();
        var capture = await CaptureBaselineAsync("templates", state, region);
        Assert.IsTrue(File.Exists(capture.FullPagePath), $"CF9 full-page baseline should be written for {state}.");
        Assert.IsTrue(File.Exists(capture.RegionPath), $"CF9 region baseline should be written for {state}.");
    }
}
