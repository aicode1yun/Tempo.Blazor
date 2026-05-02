using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetFormulaBarTests : LocalizationTestBase
{
    [Fact]
    public void Render_Default_DisplaysCellReferenceAndValue()
    {
        var cut = RenderComponent<TmSpreadsheetFormulaBar>(parameters => parameters
            .Add(p => p.ActiveCellRef, "B2")
            .Add(p => p.DisplayValue, "Hello"));

        cut.Find(".tm-spreadsheet-formula-bar__ref").TextContent.Trim().Should().Be("B2");
        cut.Find(".tm-spreadsheet-formula-bar__display").TextContent.Trim().Should().Be("Hello");
    }

    [Fact]
    public void Render_EmptyCell_ShowsPlaceholder()
    {
        var cut = RenderComponent<TmSpreadsheetFormulaBar>(parameters => parameters
            .Add(p => p.ActiveCellRef, "A1")
            .Add(p => p.DisplayValue, (string?)null));

        cut.Find(".tm-spreadsheet-formula-bar__display").TextContent.Trim().Should().Contain("Enter formula or value");
    }

    [Fact]
    public void Click_Display_StartsEditing()
    {
        var cut = RenderComponent<TmSpreadsheetFormulaBar>(parameters => parameters
            .Add(p => p.ActiveCellRef, "A1")
            .Add(p => p.DisplayValue, "Test"));

        cut.Find(".tm-spreadsheet-formula-bar__display").Click();

        cut.Instance.IsEditing.Should().BeTrue();
        cut.Find(".tm-spreadsheet-formula-bar__input").Should().NotBeNull();
    }

    [Fact]
    public void Enter_CommitsValue()
    {
        string? committed = null;
        var cut = RenderComponent<TmSpreadsheetFormulaBar>(parameters => parameters
            .Add(p => p.ActiveCellRef, "A1")
            .Add(p => p.DisplayValue, "Old")
            .Add(p => p.IsEditing, true)
            .Add(p => p.OnValueCommitted, EventCallback.Factory.Create<string?>(this, v => committed = v)));

        var input = cut.Find(".tm-spreadsheet-formula-bar__input");
        input.Input("New Value");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        committed.Should().Be("New Value");
    }

    [Fact]
    public void Escape_CancelsEdit()
    {
        bool cancelled = false;
        var cut = RenderComponent<TmSpreadsheetFormulaBar>(parameters => parameters
            .Add(p => p.ActiveCellRef, "A1")
            .Add(p => p.DisplayValue, "Old")
            .Add(p => p.IsEditing, true)
            .Add(p => p.OnEditCancelled, EventCallback.Factory.Create(this, () => cancelled = true)));

        var input = cut.Find(".tm-spreadsheet-formula-bar__input");
        input.Input("Changed");
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cancelled.Should().BeTrue();
    }

    [Fact]
    public void EditStarted_EventFires()
    {
        bool started = false;
        var cut = RenderComponent<TmSpreadsheetFormulaBar>(parameters => parameters
            .Add(p => p.ActiveCellRef, "A1")
            .Add(p => p.DisplayValue, "Test")
            .Add(p => p.OnEditStarted, EventCallback.Factory.Create(this, () => started = true)));

        cut.Find(".tm-spreadsheet-formula-bar__display").Click();

        started.Should().BeTrue();
    }

    [Fact]
    public void ValueChanged_EventFiresOnInput()
    {
        string? changed = null;
        var cut = RenderComponent<TmSpreadsheetFormulaBar>(parameters => parameters
            .Add(p => p.ActiveCellRef, "A1")
            .Add(p => p.DisplayValue, "Test")
            .Add(p => p.IsEditing, true)
            .Add(p => p.OnValueChanged, EventCallback.Factory.Create<string?>(this, v => changed = v)));

        cut.Find(".tm-spreadsheet-formula-bar__input").Input("X");

        changed.Should().Be("X");
    }
}
