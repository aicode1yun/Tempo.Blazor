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
    [Description("PDF template designer supports dragging a palette field directly onto the document")]
    public async Task PdfTemplateDesigner_DragsPaletteFieldOntoPage()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);
        var surface = designer.Locator("[data-page-key='designer-nda:0'] .tm-pdf-template-designer__page-surface").First;
        var surfaceBox = await surface.BoundingBoxAsync();
        Assert.IsNotNull(surfaceBox);

        await designer.Locator("[data-field-type='Signature']").DragToAsync(surface, new LocatorDragToOptions
        {
            TargetPosition = new TargetPosition
            {
                X = (float)(surfaceBox.Width * 0.72),
                Y = (float)(surfaceBox.Height * 0.72)
            }
        });

        await Expect(page.Locator("[data-testid='pdf-template-designer-status']")).ToContainTextAsync("3 designer fields");
        await Expect(designer.Locator(".tm-signing-field")).ToHaveCountAsync(3);
        await Expect(designer.Locator(".tm-signing-field--selected")).ToHaveCountAsync(1);
    }

    [TestMethod]
    [Description("PDF template designer palette items stay inside the palette column")]
    public async Task PdfTemplateDesigner_PaletteDoesNotOverflowColumn()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);

        var overflowing = await designer.Locator(".tm-pdf-template-designer__palette").EvaluateAsync<string[]>(
            """
            palette => {
                const paletteBox = palette.getBoundingClientRect();
                return Array.from(palette.querySelectorAll('.tm-pdf-template-designer__palette-item'))
                    .filter(item => {
                        const box = item.getBoundingClientRect();
                        return box.left < paletteBox.left - 1 || box.right > paletteBox.right + 1;
                    })
                    .map(item => item.textContent.trim());
            }
            """);

        Assert.AreEqual(0, overflowing.Length, $"Palette items overflow the palette column: {string.Join(", ", overflowing)}");
    }

    [TestMethod]
    [Description("PDF template designer demo keeps delivery field away from the body paragraph")]
    public async Task PdfTemplateDesigner_DeliveryFieldIsPlacedBesideRecipient()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);

        var nameBox = await designer.Locator("[data-field-uuid='designer-name']").First.BoundingBoxAsync();
        var deliveryBox = await designer.Locator("[data-field-uuid='designer-delivery']").First.BoundingBoxAsync();

        Assert.IsNotNull(nameBox);
        Assert.IsNotNull(deliveryBox);
        Assert.IsTrue(deliveryBox.X > nameBox.X + nameBox.Width, "Delivery should sit beside the recipient field, not over the body text.");
        Assert.IsTrue(Math.Abs(deliveryBox.Y - nameBox.Y) < nameBox.Height, "Delivery should stay aligned with the recipient row.");
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
    [Description("PDF template designer field context menu actions stay clickable above document fields")]
    public async Task PdfTemplateDesigner_ContextMenuActionsAreClickable()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);
        var field = designer.Locator("[data-field-uuid='designer-name']").First;

        await field.ClickAsync(new LocatorClickOptions { Button = MouseButton.Right });
        await Expect(designer.Locator(".tm-pdf-template-designer__context-actions")).ToBeVisibleAsync();
        await designer.Locator(".tm-pdf-template-designer__context-actions .tm-pdf-template-designer__delete-field").ClickAsync();

        await Expect(page.Locator("[data-testid='pdf-template-designer-status']")).ToContainTextAsync("1 designer field");
    }

    [TestMethod]
    [Description("PDF template designer moving a field does not start page rectangle selection")]
    public async Task PdfTemplateDesigner_MoveDoesNotStartSelectionBox()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);
        var field = designer.Locator("[data-field-uuid='designer-name']").First;
        var before = await field.BoundingBoxAsync();
        Assert.IsNotNull(before);

        await page.Mouse.MoveAsync(before.X + 10, before.Y + 10);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(before.X + 90, before.Y + 60);

        await Expect(designer.Locator(".tm-pdf-template-designer__draft")).ToHaveCountAsync(0);

        await page.Mouse.UpAsync();
        await Expect(designer.Locator(".tm-signing-field--selected")).ToHaveCountAsync(1);
    }

    [TestMethod]
    [Description("PDF template designer deletes the selected field with the Delete key")]
    public async Task PdfTemplateDesigner_DeleteKeyDeletesSelectedField()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);

        await designer.Locator("[data-field-uuid='designer-name']").ClickAsync();
        await page.Keyboard.PressAsync("Delete");

        await Expect(page.Locator("[data-testid='pdf-template-designer-status']")).ToContainTextAsync("1 designer field");
        await Expect(designer.Locator("[data-field-uuid='designer-name']")).ToHaveCountAsync(0);
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
    [Description("PDF template designer field condition editor stays inside the right settings panel")]
    public async Task PdfTemplateDesigner_ConditionEditorFitsSettingsPanel()
    {
        var page = await OpenDesignerAsync();
        var designer = GetDesigner(page);

        await designer.Locator("[data-field-uuid='designer-name']").ClickAsync();
        await designer.Locator(".tm-signing-field-editor-panel__open-conditions").ClickAsync();
        await designer.Locator(".tm-condition-builder__add").ClickAsync();

        var overflowing = await designer.Locator(".tm-pdf-template-designer__panel").EvaluateAsync<string[]>(
            """
            panel => {
                const panelBox = panel.getBoundingClientRect();
                return Array.from(panel.querySelectorAll('input, select, textarea, button, .tm-condition-builder__row'))
                    .filter(element => {
                        const box = element.getBoundingClientRect();
                        return box.left < panelBox.left - 1 || box.right > panelBox.right + 1;
                    })
                    .map(element => element.className || element.tagName);
            }
            """);

        Assert.AreEqual(0, overflowing.Length, $"Settings panel controls overflow: {string.Join(", ", overflowing)}");
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
