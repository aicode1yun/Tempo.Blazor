using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class PdfTemplateDesignerE2ETests : WasmTestBase
{
    [TestMethod]
    [Description("PDF template designer demo renders the two-page designer")]
    public async Task PdfTemplateDesigner_OpensDemo()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);

        await Expect(designer).ToBeVisibleAsync();
        await Expect(designer.Locator(".tm-document-page-viewer")).ToHaveCountAsync(2);
        await TakeScreenshotAsync(page, "pdf-template-designer-desktop");
    }

    [TestMethod]
    [Description("PDF template designer draws a new text field")]
    public async Task PdfTemplateDesigner_DrawsTextField()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);

        await designer.Locator("[data-field-type='Text']").ClickAsync();
        var surface = designer.Locator("[data-page-key='designer-nda:0'] .tm-pdf-template-designer__page-surface").First;
        var box = await surface.BoundingBoxAsync();
        Assert.IsNotNull(box);

        await page.Mouse.MoveAsync(box.X + 80, box.Y + 120);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(box.X + 220, box.Y + 170);
        await page.Mouse.UpAsync();

        await Expect(page.Locator("[data-testid='pdf-template-designer-status']")).ToContainTextAsync("3 designer fields");
        await Expect(designer.Locator(".tm-signing-field")).ToHaveCountAsync(3);
    }

    [TestMethod]
    [Description("PDF template designer moves and resizes a selected field")]
    public async Task PdfTemplateDesigner_MovesAndResizesField()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);
        var field = designer.Locator("[data-field-uuid='designer-name']").First;
        var before = await field.BoundingBoxAsync();
        Assert.IsNotNull(before);

        await page.Mouse.MoveAsync(before.X + 10, before.Y + 10);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(before.X + 80, before.Y + 50);
        await page.Mouse.UpAsync();

        var moved = await field.BoundingBoxAsync();
        Assert.IsNotNull(moved);
        Assert.IsTrue(moved.X > before.X);

        await field.ClickAsync();
        var handle = designer.Locator("[data-field-uuid='designer-name'] [data-handle='SouthEast']").First;
        var handleBox = await handle.BoundingBoxAsync();
        Assert.IsNotNull(handleBox);

        await page.Mouse.MoveAsync(handleBox.X + 2, handleBox.Y + 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(handleBox.X + 80, handleBox.Y + 60);
        await page.Mouse.UpAsync();

        var resized = await field.BoundingBoxAsync();
        Assert.IsNotNull(resized);
        Assert.IsTrue(resized.Width > moved.Width);
    }

    [TestMethod]
    [Description("PDF template designer opens field settings and edits select options")]
    public async Task PdfTemplateDesigner_OpensSettingsAndAddsSelectOption()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);

        await designer.Locator("[data-field-uuid='designer-delivery']").ClickAsync();

        await Expect(designer.Locator(".tm-signing-field-editor-panel__title")).ToContainTextAsync("Delivery");
        await designer.Locator(".tm-signing-field-editor-panel__add-option").ClickAsync();
        await Expect(designer.Locator(".tm-signing-field-editor-panel__option-row")).ToHaveCountAsync(3);
    }

    [TestMethod]
    [Description("PDF template designer multi-selects two fields and deletes them")]
    public async Task PdfTemplateDesigner_MultiSelectDeletesFields()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);

        await designer.Locator("[data-field-uuid='designer-name']").ClickAsync();
        await designer.Locator("[data-field-uuid='designer-delivery']").DispatchEventAsync("click", new Dictionary<string, object>
        {
            ["bubbles"] = true,
            ["cancelable"] = true,
            ["ctrlKey"] = true
        });

        await Expect(designer.Locator(".tm-signing-field--selected")).ToHaveCountAsync(2);
        await designer.Locator(".tm-pdf-template-designer__delete-selected").ClickAsync();

        await Expect(page.Locator("[data-testid='pdf-template-designer-status']")).ToContainTextAsync("0 designer fields");
    }

    [TestMethod]
    [Description("PDF template designer keeps the mobile layout renderable")]
    public async Task PdfTemplateDesigner_MobileScreenshot()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);

        var designer = GetDesigner(page);
        await designer.ScrollIntoViewIfNeededAsync();
        await Expect(designer).ToBeVisibleAsync();
        await TakeScreenshotAsync(page, "pdf-template-designer-mobile");
    }

    private async Task<IPage> OpenDesignerAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);
        var designer = GetDesigner(page);
        await designer.ScrollIntoViewIfNeededAsync();
        return page;
    }

    private static ILocator GetDesigner(IPage page)
    {
        return page.Locator("[data-testid='pdf-template-designer']").First;
    }

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
}
