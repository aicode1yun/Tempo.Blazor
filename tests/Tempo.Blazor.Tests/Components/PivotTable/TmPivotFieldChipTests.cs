using FluentAssertions;
using Tempo.Blazor.Abstractions.PivotTable;
using Tempo.Blazor.Components.PivotTable;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.PivotTable;

public class TmPivotFieldChipTests : LocalizationTestBase
{
    private record Transaction(string Category, decimal Amount);

    private static readonly PivotField<Transaction> CategoryField = new()
    {
        Key = "Category",
        Title = "Category",
        Accessor = t => t.Category
    };

    // ═══════════════════════════════════════════════════════════════
    //  4.10  Basic rendering
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotFieldChip_Render_DisplaysFieldTitle()
    {
        var cut = Render<TmPivotFieldChip<Transaction>>(parameters => parameters
            .Add(p => p.Field, CategoryField)
            .Add(p => p.Area, PivotArea.Unused)
        );

        var chip = cut.Find(".tm-pivot-field-chip");
        chip.TextContent.Should().Contain("Category");
    }

    [Theory]
    [InlineData(PivotArea.Unused, "tm-pivot-field-chip--unused")]
    [InlineData(PivotArea.Row, "tm-pivot-field-chip--row")]
    [InlineData(PivotArea.Column, "tm-pivot-field-chip--column")]
    [InlineData(PivotArea.Data, "tm-pivot-field-chip--value")]
    [InlineData(PivotArea.Filter, "tm-pivot-field-chip--filter")]
    public void TmPivotFieldChip_Area_AppliesCorrectCssClass(PivotArea area, string expectedClass)
    {
        var cut = Render<TmPivotFieldChip<Transaction>>(parameters => parameters
            .Add(p => p.Field, CategoryField)
            .Add(p => p.Area, area)
        );

        var chip = cut.Find(".tm-pivot-field-chip");
        chip.ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void TmPivotFieldChip_WithShowRemove_DisplaysRemoveButton()
    {
        var cut = Render<TmPivotFieldChip<Transaction>>(parameters => parameters
            .Add(p => p.Field, CategoryField)
            .Add(p => p.Area, PivotArea.Row)
            .Add(p => p.ShowRemove, true)
        );

        var removeBtn = cut.Find(".tm-pivot-field-chip-btn--remove");
        removeBtn.Should().NotBeNull();
    }

    [Fact]
    public void TmPivotFieldChip_WithoutShowRemove_HidesRemoveButton()
    {
        var cut = Render<TmPivotFieldChip<Transaction>>(parameters => parameters
            .Add(p => p.Field, CategoryField)
            .Add(p => p.Area, PivotArea.Row)
            .Add(p => p.ShowRemove, false)
        );

        cut.FindAll(".tm-pivot-field-chip-btn--remove").Should().BeEmpty();
    }

    [Fact]
    public void TmPivotFieldChip_WithShowSettings_DisplaysSettingsButton()
    {
        var cut = Render<TmPivotFieldChip<Transaction>>(parameters => parameters
            .Add(p => p.Field, CategoryField)
            .Add(p => p.Area, PivotArea.Data)
            .Add(p => p.ShowSettings, true)
        );

        var settingsBtn = cut.Find(".tm-pivot-field-chip-btn");
        settingsBtn.Should().NotBeNull();
    }

    [Fact]
    public void TmPivotFieldChip_WithoutShowSettings_HidesSettingsButton()
    {
        var cut = Render<TmPivotFieldChip<Transaction>>(parameters => parameters
            .Add(p => p.Field, CategoryField)
            .Add(p => p.Area, PivotArea.Data)
            .Add(p => p.ShowSettings, false)
        );

        cut.FindAll(".tm-pivot-field-chip-btn").Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════
    //  4.11  Interaction
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotFieldChip_RemoveClick_FiresOnRemoveClick()
    {
        var clicked = false;

        var cut = Render<TmPivotFieldChip<Transaction>>(parameters => parameters
            .Add(p => p.Field, CategoryField)
            .Add(p => p.Area, PivotArea.Row)
            .Add(p => p.ShowRemove, true)
            .Add(p => p.OnRemoveClick, () => clicked = true)
        );

        var removeBtn = cut.Find(".tm-pivot-field-chip-btn--remove");
        removeBtn.Click();

        clicked.Should().BeTrue();
    }

    [Fact]
    public void TmPivotFieldChip_SettingsClick_FiresOnSettingsClick()
    {
        var clicked = false;

        var cut = Render<TmPivotFieldChip<Transaction>>(parameters => parameters
            .Add(p => p.Field, CategoryField)
            .Add(p => p.Area, PivotArea.Data)
            .Add(p => p.ShowSettings, true)
            .Add(p => p.OnSettingsClick, () => clicked = true)
        );

        var settingsBtn = cut.Find(".tm-pivot-field-chip-btn");
        settingsBtn.Click();

        clicked.Should().BeTrue();
    }

    [Fact]
    public void TmPivotFieldChip_WithAllowDragDrop_SetsDraggableTrue()
    {
        var cut = Render<TmPivotFieldChip<Transaction>>(parameters => parameters
            .Add(p => p.Field, CategoryField)
            .Add(p => p.Area, PivotArea.Unused)
            .Add(p => p.AllowDragDrop, true)
        );

        var chip = cut.Find(".tm-pivot-field-chip");
        chip.GetAttribute("draggable").Should().Be("true");
    }

    [Fact]
    public void TmPivotFieldChip_WithoutAllowDragDrop_SetsDraggableFalse()
    {
        var cut = Render<TmPivotFieldChip<Transaction>>(parameters => parameters
            .Add(p => p.Field, CategoryField)
            .Add(p => p.Area, PivotArea.Unused)
            .Add(p => p.AllowDragDrop, false)
        );

        var chip = cut.Find(".tm-pivot-field-chip");
        chip.GetAttribute("draggable").Should().Be("false");
    }
}
