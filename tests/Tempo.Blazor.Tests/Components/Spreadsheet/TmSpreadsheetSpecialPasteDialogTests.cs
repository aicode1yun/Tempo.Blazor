using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Dialogs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetSpecialPasteDialogTests : LocalizationTestBase
{
    [Fact]
    public void Renders_AllContentOptions_Localized()
    {
        var cut = RenderComponent<TmSpreadsheetSpecialPasteDialog>();

        var markup = cut.Markup;
        markup.Should().Contain("Paste special");
        markup.Should().Contain("Values");
        markup.Should().Contain("Formulas");
        markup.Should().Contain("All except borders");
        markup.Should().Contain("Transpose");
        markup.Should().Contain("Skip blanks");
    }

    [Fact]
    public void Apply_DefaultsToAll_NoOperation()
    {
        SpreadsheetPasteSpecialOptions? applied = null;
        var cut = RenderComponent<TmSpreadsheetSpecialPasteDialog>(p => p
            .Add(c => c.OnApply, EventCallback.Factory.Create<SpreadsheetPasteSpecialOptions>(this, o => applied = o)));

        cut.Find(".tm-spreadsheet-pastespecial__btn--ok").Click();

        applied.Should().NotBeNull();
        applied!.Content.Should().Be(SpreadsheetPasteContent.All);
        applied.Operation.Should().Be(SpreadsheetPasteOperation.None);
        applied.Transpose.Should().BeFalse();
    }

    [Fact]
    public void Apply_ReflectsSelectedContent_AndTranspose()
    {
        SpreadsheetPasteSpecialOptions? applied = null;
        var cut = RenderComponent<TmSpreadsheetSpecialPasteDialog>(p => p
            .Add(c => c.OnApply, EventCallback.Factory.Create<SpreadsheetPasteSpecialOptions>(this, o => applied = o)));

        // Select the "Values" radio (2nd option).
        cut.FindAll("input[name=ps-content]")[1].Change(true);
        // Tick transpose (2nd toggle).
        cut.FindAll(".tm-spreadsheet-pastespecial__toggles input[type=checkbox]")[1].Change(true);

        cut.Find(".tm-spreadsheet-pastespecial__btn--ok").Click();

        applied!.Content.Should().Be(SpreadsheetPasteContent.Values);
        applied.Transpose.Should().BeTrue();
    }
}
