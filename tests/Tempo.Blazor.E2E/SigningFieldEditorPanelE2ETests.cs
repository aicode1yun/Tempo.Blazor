using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class SigningFieldEditorPanelE2ETests : WasmTestBase
{
    [TestMethod]
    [Description("Signing field editor renames a field and updates the live overlay preview")]
    public async Task SigningFieldEditor_RenamesFieldAndUpdatesPreview()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);

        var panel = await GetPanelAsync(page);
        var name = panel.Locator(".tm-signing-field-editor-panel__name").First;

        await name.FillAsync("Residence country");
        await name.PressAsync("Tab");

        await Expect(page.Locator("[data-testid='field-editor-preview']")).ToContainTextAsync("Residence country");
        await Expect(page.Locator("[data-testid='field-editor-status']")).ToContainTextAsync("Residence country");
    }

    [TestMethod]
    [Description("Signing field editor adds a choice option and keeps the demo status in sync")]
    public async Task SigningFieldEditor_AddsChoiceOption()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);

        var panel = await GetPanelAsync(page);
        await panel.Locator(".tm-signing-field-editor-panel__add-option").ClickAsync();

        await Expect(panel.Locator(".tm-signing-field-editor-panel__option-row")).ToHaveCountAsync(3);
        await Expect(page.Locator("[data-testid='field-editor-status']")).ToContainTextAsync("options: 3");
    }

    [TestMethod]
    [Description("Signing field editor toggles required and edits validation constraints")]
    public async Task SigningFieldEditor_SetsRequiredAndValidation()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);

        var panel = await GetPanelAsync(page);
        await panel.Locator(".tm-signing-field-editor-panel__type").SelectOptionAsync("Text");
        await panel.Locator(".tm-signing-field-editor-panel__required").CheckAsync();
        await panel.Locator(".tm-signing-field-editor-panel__validation-min").FillAsync("2");
        await panel.Locator(".tm-signing-field-editor-panel__validation-min").PressAsync("Tab");
        await panel.Locator(".tm-signing-field-editor-panel__validation-message").FillAsync("Choose at least two characters");
        await panel.Locator(".tm-signing-field-editor-panel__validation-message").PressAsync("Tab");

        await Expect(page.Locator("[data-testid='field-editor-status']")).ToContainTextAsync("required: True");
        await Expect(panel.Locator(".tm-signing-field-editor-panel__validation-min")).ToHaveValueAsync("2");
        await Expect(panel.Locator(".tm-signing-field-editor-panel__validation-message")).ToHaveValueAsync("Choose at least two characters");
    }

    [TestMethod]
    [Description("Signing field editor saves a condition through the embedded condition builder")]
    public async Task SigningFieldEditor_SavesCondition()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/signing-components");
        await WaitForAppReadyAsync(page);

        var panel = await GetPanelAsync(page);
        await panel.Locator(".tm-signing-field-editor-panel__open-conditions").ClickAsync();
        var builder = panel.Locator(".tm-signing-field-editor-panel__condition-builder").First;
        await Expect(builder.Locator(".tm-condition-builder")).ToBeVisibleAsync();
        await builder.Locator(".tm-condition-builder__field").First.SelectOptionAsync("editor-consent");

        await Expect(page.Locator("[data-testid='field-editor-status']")).ToContainTextAsync("conditions: 1");
    }

    private static async Task<ILocator> GetPanelAsync(IPage page)
    {
        var panel = page.Locator("[data-testid='field-editor-panel']").First;
        await panel.ScrollIntoViewIfNeededAsync();
        return panel;
    }

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
}
