using FluentAssertions;
using Tempo.Blazor.Abstractions.PivotTable;
using Tempo.Blazor.Components.PivotTable;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.PivotTable;

public class TmPivotTableTests : LocalizationTestBase
{
    private record Transaction(string Category, string Month, decimal Amount);

    private static readonly List<Transaction> TestData =
    [
        new("Food", "Jan", 500m),
        new("Food", "Feb", 600m),
        new("Transport", "Jan", 200m),
        new("Transport", "Feb", 220m),
    ];

    private static readonly List<PivotField<Transaction>> Fields =
    [
        new() { Key = "Category", Title = "Category", Accessor = t => t.Category },
        new() { Key = "Month", Title = "Month", Accessor = t => t.Month },
        new() { Key = "Amount", Title = "Amount", Accessor = t => t.Amount },
    ];

    // ═══════════════════════════════════════════════════════════════
    //  3.10  Basic rendering with parameters
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotTable_BasicRendering_DisplaysTable()
    {
        var cut = RenderComponent<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        var wrapper = cut.Find(".tm-pivot-table-wrapper");
        wrapper.Should().NotBeNull();

        var table = cut.Find(".tm-pivot-table");
        table.Should().NotBeNull();

        // Check column headers
        var headers = cut.FindAll(".tm-pivot-col-header");
        headers.Should().Contain(h => h.TextContent == "Jan");
        headers.Should().Contain(h => h.TextContent == "Feb");

        // Check row dimension cells
        var rowDims = cut.FindAll(".tm-pivot-row-dim");
        rowDims.Should().Contain(r => r.TextContent == "Food");
        rowDims.Should().Contain(r => r.TextContent == "Transport");
    }

    [Fact]
    public void TmPivotTable_BasicRendering_DisplaysCorrectCellValues()
    {
        var cut = RenderComponent<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        var cells = cut.FindAll(".tm-pivot-cell");
        cells.Should().NotBeEmpty();

        // Food row: values should include 500 (Jan) and 600 (Feb)
        var foodRow = cut.FindAll("tr").FirstOrDefault(tr => tr.QuerySelector(".tm-pivot-row-dim")?.TextContent == "Food");
        foodRow.Should().NotBeNull();

        var foodCells = foodRow!.QuerySelectorAll(".tm-pivot-cell");
        foodCells.Should().HaveCount(2); // Jan, Feb
        var foodValues = foodCells.Select(c => c.TextContent).ToList();
        foodValues.Should().Contain("500");
        foodValues.Should().Contain("600");
    }

    // ═══════════════════════════════════════════════════════════════
    //  3.11  Reacting to configuration changes
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotTable_ConfigurationChange_RecomputesData()
    {
        var cut = RenderComponent<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        // Initial: 2 rows (Food, Transport)
        var initialRows = cut.FindAll(".tm-pivot-row-dim");
        initialRows.Should().HaveCount(2);

        // Change to rows by Month
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.RowFieldKeys, ["Month"])
            .Add(p => p.ColumnFieldKeys, ["Category"])
        );

        var updatedRows = cut.FindAll(".tm-pivot-row-dim");
        updatedRows.Should().HaveCount(2); // Jan, Feb
        updatedRows.Should().Contain(r => r.TextContent == "Jan");
        updatedRows.Should().Contain(r => r.TextContent == "Feb");
    }

    [Fact]
    public void TmPivotTable_ValueFieldChange_UpdatesAggregation()
    {
        var cut = RenderComponent<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, [])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        // Initial: Sum = 500+600 = 1100 for Food
        var initialCells = cut.FindAll(".tm-pivot-cell");
        initialCells.Should().Contain(c => c.TextContent == "1100");

        // Change to Count
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Count" }])
        );

        var updatedCells = cut.FindAll(".tm-pivot-cell");
        updatedCells.Should().Contain(c => c.TextContent == "2"); // Count = 2 for Food
    }

    // ═══════════════════════════════════════════════════════════════
    //  3.12  Empty state
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotTable_EmptyData_ShowsEmptyState()
    {
        var cut = RenderComponent<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, Array.Empty<Transaction>())
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        var emptyState = cut.Find(".tm-empty-state");
        emptyState.Should().NotBeNull();
    }

    [Fact]
    public void TmPivotTable_NoConfiguration_ShowsEmptyState()
    {
        var cut = RenderComponent<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, [])
            .Add(p => p.ColumnFieldKeys, [])
            .Add(p => p.ValueFields, [])
        );

        var emptyState = cut.Find(".tm-empty-state");
        emptyState.Should().NotBeNull();
    }

    [Fact]
    public void TmPivotTable_LoadingState_ShowsSpinner()
    {
        var cut = RenderComponent<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.IsLoading, true)
        );

        var spinner = cut.Find(".tm-spinner");
        spinner.Should().NotBeNull();
    }

    [Fact]
    public void TmPivotTable_CustomEmptyTitle_ShowsCustomTitle()
    {
        var customTitle = "Custom empty message";
        var cut = RenderComponent<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, Array.Empty<Transaction>())
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.EmptyTitle, customTitle)
        );

        var emptyStateTitle = cut.Find(".tm-empty-state");
        emptyStateTitle.TextContent.Should().Contain(customTitle);
    }

    // ═══════════════════════════════════════════════════════════════
    //  3.13  Multi-level row dimensions (rowspan)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotTable_MultiRowDimensions_RendersCorrectRowSpans()
    {
        var data = new List<Transaction>
        {
            new("Food", "Jan", 100m),
            new("Food", "Feb", 200m),
            new("Transport", "Jan", 300m),
            new("Transport", "Feb", 400m),
        };

        var fields = new List<PivotField<Transaction>>
        {
            new() { Key = "Category", Title = "Category", Accessor = t => t.Category },
            new() { Key = "Month", Title = "Month", Accessor = t => t.Month },
            new() { Key = "Amount", Title = "Amount", Accessor = t => t.Amount },
        };

        var cut = RenderComponent<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, data)
            .Add(p => p.Fields, fields)
            .Add(p => p.RowFieldKeys, ["Category", "Month"])
            .Add(p => p.ColumnFieldKeys, [])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        // Each category has 2 months, so each category cell should have rowspan=2
        var rowDimCells = cut.FindAll(".tm-pivot-row-dim");

        // First level (Category): Food, Transport — each spans 2 rows
        // Second level (Month): Jan, Feb under each category
        rowDimCells.Should().HaveCount(6); // Food, Jan, Feb, Transport, Jan, Feb

        // Verify structure: Food at index 0, then Feb, Jan (alphabetical), then Transport, Feb, Jan
        rowDimCells[0].TextContent.Should().Be("Food");
        rowDimCells[1].TextContent.Should().Be("Feb");
        rowDimCells[2].TextContent.Should().Be("Jan");
        rowDimCells[3].TextContent.Should().Be("Transport");
        rowDimCells[4].TextContent.Should().Be("Feb");
        rowDimCells[5].TextContent.Should().Be("Jan");

        // Category cells should have rowspan=2
        rowDimCells[0].GetAttribute("rowspan").Should().Be("2");
        rowDimCells[3].GetAttribute("rowspan").Should().Be("2");

        // Month cells should not have rowspan (or rowspan=1)
        rowDimCells[1].GetAttribute("rowspan").Should().BeNullOrEmpty();
        rowDimCells[2].GetAttribute("rowspan").Should().BeNullOrEmpty();
    }

    [Fact]
    public void TmPivotTable_FilterChange_AutoAppliesAndRecomputesData()
    {
        var cut = RenderComponent<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.FilterFields, new Dictionary<string, List<object?>>())
        );

        // Initial: 2 rows (Food, Transport) x 2 cols (Jan, Feb)
        var initialCells = cut.FindAll(".tm-pivot-cell");
        initialCells.Should().HaveCount(4);

        // Change filter to only include Food
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.FilterFields, new Dictionary<string, List<object?>> { ["Category"] = ["Food"] })
        );

        // After filter: 1 row (Food) x 2 cols (Jan, Feb)
        var filteredCells = cut.FindAll(".tm-pivot-cell");
        filteredCells.Should().HaveCount(2);

        var rowDims = cut.FindAll(".tm-pivot-row-dim");
        rowDims.Should().ContainSingle().Which.TextContent.Should().Be("Food");
    }
}
