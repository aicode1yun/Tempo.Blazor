using FluentAssertions;
using Tempo.Blazor.Abstractions.PivotTable;
using Tempo.Blazor.Components.PivotTable;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.PivotTable;

public class TmPivotFieldPanelTests : LocalizationTestBase
{
    private record Transaction(string Category, string Month, decimal Amount);

    private static readonly List<PivotField<Transaction>> Fields =
    [
        new() { Key = "Category", Title = "Category", Accessor = t => t.Category },
        new() { Key = "Month", Title = "Month", Accessor = t => t.Month },
        new() { Key = "Amount", Title = "Amount", Accessor = t => t.Amount },
    ];

    // ═══════════════════════════════════════════════════════════════
    //  4.1  Basic rendering
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotFieldPanel_Render_DisplaysAllZones()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
        );

        var panel = cut.Find(".tm-pivot-field-panel");
        panel.Should().NotBeNull();

        var zones = cut.FindAll(".tm-pivot-zone");
        zones.Should().HaveCount(5); // Unused, Row, Column, Data, Filter
    }

    [Fact]
    public void TmPivotFieldPanel_Render_UnusedFields_ShowsAllAvailableFields()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
        );

        var unusedZone = cut.FindAll(".tm-pivot-zone--unused .tm-pivot-field-chip");
        unusedZone.Should().HaveCount(3);
        unusedZone.Select(c => c.TextContent).Should().Contain("Category");
        unusedZone.Select(c => c.TextContent).Should().Contain("Month");
        unusedZone.Select(c => c.TextContent).Should().Contain("Amount");
    }

    [Fact]
    public void TmPivotFieldPanel_WithRowFields_ShowsInRowZone()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
        );

        var rowZone = cut.FindAll(".tm-pivot-zone--row .tm-pivot-field-chip");
        rowZone.Should().HaveCount(1);
        rowZone[0].TextContent.Should().Contain("Category");

        var unusedZone = cut.FindAll(".tm-pivot-zone--unused .tm-pivot-field-chip");
        unusedZone.Should().HaveCount(2);
        unusedZone.Select(c => c.TextContent).Should().NotContain("Category");
    }

    [Fact]
    public void TmPivotFieldPanel_WithColumnFields_ShowsInColumnZone()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.ColumnFieldKeys, ["Month"])
        );

        var colZone = cut.FindAll(".tm-pivot-zone--column .tm-pivot-field-chip");
        colZone.Should().HaveCount(1);
        colZone[0].TextContent.Should().Contain("Month");
    }

    [Fact]
    public void TmPivotFieldPanel_WithValueFields_ShowsInDataZone()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        var dataZone = cut.FindAll(".tm-pivot-zone--value .tm-pivot-field-chip");
        dataZone.Should().HaveCount(1);
        dataZone[0].TextContent.Should().Contain("Amount");
    }

    [Fact]
    public void TmPivotFieldPanel_WithFilterFields_ShowsInFilterZone()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.FilterFields, new Dictionary<string, List<object?>> { ["Category"] = ["Food"] })
        );

        var filterZone = cut.FindAll(".tm-pivot-zone--filter .tm-pivot-field-chip");
        filterZone.Should().HaveCount(1);
        filterZone[0].TextContent.Should().Contain("Category");
    }

    // ═══════════════════════════════════════════════════════════════
    //  4.2  Remove button interaction
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotFieldPanel_RemoveRowField_MovesToUnused()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
        );

        var removeBtn = cut.Find(".tm-pivot-zone--row .tm-pivot-field-chip-btn--remove");
        removeBtn.Click();

        var rowZone = cut.FindAll(".tm-pivot-zone--row .tm-pivot-field-chip");
        rowZone.Should().BeEmpty();

        var unusedZone = cut.FindAll(".tm-pivot-zone--unused .tm-pivot-field-chip");
        unusedZone.Should().Contain(c => c.TextContent.Contains("Category"));
    }

    [Fact]
    public void TmPivotFieldPanel_RemoveValueField_MovesToUnused()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        var removeBtn = cut.Find(".tm-pivot-zone--value .tm-pivot-field-chip-btn--remove");
        removeBtn.Click();

        var dataZone = cut.FindAll(".tm-pivot-zone--value .tm-pivot-field-chip");
        dataZone.Should().BeEmpty();

        var unusedZone = cut.FindAll(".tm-pivot-zone--unused .tm-pivot-field-chip");
        unusedZone.Should().Contain(c => c.TextContent.Contains("Amount"));
    }

    // ═══════════════════════════════════════════════════════════════
    //  4.3  Apply configuration
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotFieldPanel_Apply_FiresConfigurationChanged()
    {
        PivotTableConfiguration? capturedConfig = null;

        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.OnConfigurationChanged, config => { capturedConfig = config; })
        );

        var applyBtn = cut.Find(".tm-pivot-field-panel-actions button:first-child");
        applyBtn.Click();

        capturedConfig.Should().NotBeNull();
        capturedConfig!.RowFieldKeys.Should().ContainSingle().Which.Should().Be("Category");
        capturedConfig.ValueFields.Should().HaveCount(1);
        capturedConfig.ValueFields[0].FieldKey.Should().Be("Amount");
        capturedConfig.ValueFields[0].Aggregation.Should().Be("Sum");
    }

    [Fact]
    public void TmPivotFieldPanel_ClearAll_EmptiesAllZones()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        var clearBtn = cut.FindAll(".tm-pivot-field-panel-actions button").Last();
        clearBtn.Click();

        cut.FindAll(".tm-pivot-zone--row .tm-pivot-field-chip").Should().BeEmpty();
        cut.FindAll(".tm-pivot-zone--column .tm-pivot-field-chip").Should().BeEmpty();
        cut.FindAll(".tm-pivot-zone--value .tm-pivot-field-chip").Should().BeEmpty();

        var unusedZone = cut.FindAll(".tm-pivot-zone--unused .tm-pivot-field-chip");
        unusedZone.Should().HaveCount(3);
    }

    [Fact]
    public void TmPivotFieldPanel_Reset_RestoresOriginalConfiguration()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
        );

        // Remove the row field
        var removeBtn = cut.Find(".tm-pivot-zone--row .tm-pivot-field-chip-btn--remove");
        removeBtn.Click();

        cut.FindAll(".tm-pivot-zone--row .tm-pivot-field-chip").Should().BeEmpty();

        // Reset
        var resetBtn = cut.FindAll(".tm-pivot-field-panel-actions button").ElementAt(1);
        resetBtn.Click();

        var rowZone = cut.FindAll(".tm-pivot-zone--row .tm-pivot-field-chip");
        rowZone.Should().HaveCount(1);
        rowZone[0].TextContent.Should().Contain("Category");
    }

    // ═══════════════════════════════════════════════════════════════
    //  4.4  Value field settings
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotFieldPanel_ValueFieldSettings_TogglesEditor()
    {
        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        // Initially no editor
        cut.FindAll(".tm-pivot-value-field-editor").Should().BeEmpty();

        // Click settings button
        var settingsBtn = cut.Find(".tm-pivot-field-chip-btn");
        settingsBtn.Click();

        cut.FindAll(".tm-pivot-value-field-editor").Should().HaveCount(1);

        // Click again to close
        settingsBtn.Click();
        cut.FindAll(".tm-pivot-value-field-editor").Should().BeEmpty();
    }

    [Fact]
    public void TmPivotFieldPanel_ChangeAggregation_UpdatesValueField()
    {
        PivotTableConfiguration? capturedConfig = null;

        var cut = RenderComponent<TmPivotFieldPanel<Transaction>>(parameters => parameters
            .Add(p => p.Fields, Fields)
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.OnConfigurationChanged, config => { capturedConfig = config; })
        );

        // Open settings
        var settingsBtn = cut.Find(".tm-pivot-field-chip-btn");
        settingsBtn.Click();

        // Change aggregation
        var select = cut.Find(".tm-pivot-value-field-editor select");
        select.Change("Count");

        // Apply
        var applyBtn = cut.Find(".tm-pivot-field-panel-actions button:first-child");
        applyBtn.Click();

        capturedConfig.Should().NotBeNull();
        capturedConfig!.ValueFields[0].Aggregation.Should().Be("Count");
    }
}
