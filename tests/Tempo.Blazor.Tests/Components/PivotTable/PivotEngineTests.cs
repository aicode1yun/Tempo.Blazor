using FluentAssertions;
using Tempo.Blazor.Abstractions.PivotTable;
using Tempo.Blazor.Models;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Tests.Components.PivotTable;

public class PivotEngineTests
{
    private record Transaction(
        int Id,
        string Category,
        string SubCategory,
        string Month,
        decimal Amount,
        int Count);

    private static readonly List<Transaction> TestData =
    [
        new(1, "Food", "Groceries", "Jan", 500m, 10),
        new(2, "Food", "Groceries", "Feb", 600m, 12),
        new(3, "Food", "Restaurants", "Jan", 300m, 5),
        new(4, "Food", "Restaurants", "Feb", 350m, 6),
        new(5, "Transport", "Fuel", "Jan", 200m, 4),
        new(6, "Transport", "Fuel", "Feb", 220m, 5),
        new(7, "Transport", "Public", "Jan", 100m, 20),
        new(8, "Transport", "Public", "Feb", 110m, 22),
    ];

    private static readonly List<PivotField<Transaction>> Fields =
    [
        new() { Key = "Category", Title = "Category", Accessor = t => t.Category },
        new() { Key = "SubCategory", Title = "Sub Category", Accessor = t => t.SubCategory },
        new() { Key = "Month", Title = "Month", Accessor = t => t.Month },
        new() { Key = "Amount", Title = "Amount", Accessor = t => t.Amount },
        new() { Key = "Count", Title = "Count", Accessor = t => t.Count },
    ];

    // ═══════════════════════════════════════════════════════════════
    //  2.9  Simple pivot (1 row × 1 column × 1 value)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Transform_SimplePivot_ProducesCorrectMatrix()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = ["Month"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        result.Rows.Should().HaveCount(2); // Food, Transport
        result.LeafRowCount.Should().Be(2);
        result.LeafColumnCount.Should().Be(2); // Jan, Feb
        result.Cells.GetLength(0).Should().Be(2);
        result.Cells.GetLength(1).Should().Be(2);

        // Food/Jan = 500 + 300 = 800
        var foodJan = FindCell(result, "Food", "Jan", 0);
        foodJan.RawValue.Should().Be(800m);

        // Food/Feb = 600 + 350 = 950
        var foodFeb = FindCell(result, "Food", "Feb", 0);
        foodFeb.RawValue.Should().Be(950m);

        // Transport/Jan = 200 + 100 = 300
        var transJan = FindCell(result, "Transport", "Jan", 0);
        transJan.RawValue.Should().Be(300m);
    }

    // ═══════════════════════════════════════════════════════════════
    //  2.10  Multiple row dimensions + rowspan
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Transform_MultiRowDimensions_BuildsCorrectTree()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category", "SubCategory"],
            ColumnFieldKeys = ["Month"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        result.Rows.Should().HaveCount(2); // Food, Transport
        result.LeafRowCount.Should().Be(4); // 2 subcategories each

        var food = result.Rows.First(r => r.DisplayValue == "Food");
        food.Children.Should().HaveCount(2);
        food.RowSpan.Should().Be(2);

        var transport = result.Rows.First(r => r.DisplayValue == "Transport");
        transport.Children.Should().HaveCount(2);
        transport.RowSpan.Should().Be(2);
    }

    // ═══════════════════════════════════════════════════════════════
    //  2.11  Multiple column dimensions + colspan
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Transform_MultiColumnDimensions_BuildsCorrectTree()
    {
        // Not applicable with current test data (no nested column dims)
        // Use Category as column to test structure
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Month"],
            ColumnFieldKeys = ["Category", "SubCategory"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        result.Columns.Should().HaveCount(2); // Food, Transport
        result.LeafColumnCount.Should().Be(4); // 2 subcategories each

        var food = result.Columns.First(c => c.DisplayValue == "Food");
        food.Children.Should().HaveCount(2);
        food.ColSpan.Should().Be(2);
    }

    // ═══════════════════════════════════════════════════════════════
    //  2.12  Multiple value fields
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Transform_MultipleValueFields_CreatesMultipleColumns()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = ["Month"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" },
                new PivotValueFieldConfiguration { FieldKey = "Count", Aggregation = "Sum" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        result.ValueFieldCount.Should().Be(2);
        result.Cells.GetLength(1).Should().Be(4); // 2 months × 2 values

        var foodJanAmount = FindCell(result, "Food", "Jan", 0);
        foodJanAmount.RawValue.Should().Be(800m);

        var foodJanCount = FindCell(result, "Food", "Jan", 1);
        foodJanCount.RawValue.Should().Be(15); // 10 + 5
    }

    // ═══════════════════════════════════════════════════════════════
    //  2.13  All aggregation types
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Transform_SumAggregation_CalculatesCorrectly()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = [],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        var food = FindCellByRowOnly(result, "Food", 0);
        food.RawValue.Should().Be(1750m); // 500+600+300+350
    }

    [Fact]
    public void Transform_CountAggregation_CalculatesCorrectly()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = ["Month"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Count" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        var foodJan = FindCell(result, "Food", "Jan", 0);
        foodJan.RawValue.Should().Be(2); // 2 transactions
    }

    [Fact]
    public void Transform_AverageAggregation_CalculatesCorrectly()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = [],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Average" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        var food = FindCellByRowOnly(result, "Food", 0);
        food.RawValue.Should().Be(437.5m); // 1750 / 4
    }

    [Fact]
    public void Transform_MinMaxAggregation_CalculatesCorrectly()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = [],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Min" },
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Max" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        var foodMin = FindCellByRowOnly(result, "Food", 0);
        foodMin.RawValue.Should().Be(300m);

        var foodMax = FindCellByRowOnly(result, "Food", 1);
        foodMax.RawValue.Should().Be(600m);
    }

    // ═══════════════════════════════════════════════════════════════
    //  2.14  Filters
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Transform_WithFilters_ExcludesFilteredData()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = ["Month"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }
            ],
            FilterFields = new Dictionary<string, List<object?>>
            {
                ["Month"] = ["Jan"]
            }
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        result.LeafColumnCount.Should().Be(1); // Only Jan
        var foodJan = FindCell(result, "Food", "Jan", 0);
        foodJan.RawValue.Should().Be(800m);

        // Feb should not exist
        var flatCols = FlattenColumns(result.Columns);
        flatCols.Should().ContainSingle(c => c.DisplayValue == "Jan");
    }

    // ═══════════════════════════════════════════════════════════════
    //  2.15  Totals (row, column, grand)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Transform_RowTotals_CalculatesCorrectly()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = ["Month"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        var flatRows = FlattenRows(result.Rows);
        var foodRow = flatRows.First(r => r.DisplayValue == "Food");
        foodRow.Totals.Should().ContainKey(0);
        foodRow.Totals[0].RawValue.Should().Be(1750m); // 800 + 950
    }

    [Fact]
    public void Transform_ColumnTotals_CalculatesCorrectly()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = ["Month"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        var flatCols = FlattenColumns(result.Columns);
        var janCol = flatCols.First(c => c.DisplayValue == "Jan");
        janCol.Totals.Should().ContainKey(0);
        janCol.Totals[0].RawValue.Should().Be(1100m); // 800 + 300
    }

    [Fact]
    public void Transform_GrandTotal_CalculatesCorrectly()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = ["Month"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        result.GrandTotals.Should().ContainKey(0);
        result.GrandTotals[0].RawValue.Should().Be(2380m); // Total of all amounts
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    private static PivotCell FindCell(PivotTableResult result, string rowDisplay, string colDisplay, int valueIndex)
    {
        var flatRows = FlattenRows(result.Rows);
        var flatCols = FlattenColumns(result.Columns);

        var rowIndex = flatRows.FindIndex(r => r.DisplayValue == rowDisplay);
        var colIndex = flatCols.FindIndex(c => c.DisplayValue == colDisplay);

        rowIndex.Should().BeGreaterThanOrEqualTo(0, $"Row '{rowDisplay}' not found");
        colIndex.Should().BeGreaterThanOrEqualTo(0, $"Column '{colDisplay}' not found");

        return result.Cells[rowIndex, colIndex * result.ValueFieldCount + valueIndex];
    }

    private static PivotCell FindCellByRowOnly(PivotTableResult result, string rowDisplay, int valueIndex)
    {
        var flatRows = FlattenRows(result.Rows);
        var rowIndex = flatRows.FindIndex(r => r.DisplayValue == rowDisplay);
        rowIndex.Should().BeGreaterThanOrEqualTo(0, $"Row '{rowDisplay}' not found");
        return result.Cells[rowIndex, valueIndex];
    }

    private static List<PivotRowNode> FlattenRows(List<PivotRowNode> tree)
    {
        var result = new List<PivotRowNode>();
        CollectLeafRows(tree, result);
        return result;
    }

    private static void CollectLeafRows(List<PivotRowNode> nodes, List<PivotRowNode> result)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Count == 0)
                result.Add(node);
            else
                CollectLeafRows(node.Children, result);
        }
    }

    private static List<PivotColumnNode> FlattenColumns(List<PivotColumnNode> tree)
    {
        var result = new List<PivotColumnNode>();
        CollectLeafColumns(tree, result);
        return result;
    }

    private static void CollectLeafColumns(List<PivotColumnNode> nodes, List<PivotColumnNode> result)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Count == 0)
                result.Add(node);
            else
                CollectLeafColumns(node.Children, result);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  2.12  Format strings
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Transform_WithCurrencyFormat_FormatsCellValues()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum", Format = "#,##0.00 €" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        // Food total = 1750
        var foodRow = result.Rows.FirstOrDefault(r => r.Key == "Food");
        foodRow.Should().NotBeNull();
        result.Cells![foodRow!.RowIndex, 0].FormattedValue.Should().MatchRegex(@"1[\s.,]?750[.,]00");
    }

    [Fact]
    public void Transform_WithNumberFormat_FormatsCellValues()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum", Format = "N2" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        var foodRow = result.Rows.FirstOrDefault(r => r.Key == "Food");
        foodRow.Should().NotBeNull();
        result.Cells![foodRow!.RowIndex, 0].FormattedValue.Should().Contain("1").And.Contain("750");
    }

    [Fact]
    public void Transform_WithPercentFormat_FormatsCellValues()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Count", Aggregation = "Sum", Format = "P2" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        // Food total count = 33, with P2 format it becomes 3,300.00%
        var foodRow = result.Rows.FirstOrDefault(r => r.Key == "Food");
        foodRow.Should().NotBeNull();
        result.Cells![foodRow!.RowIndex, 0].FormattedValue.Should().Contain("3");
    }

    [Fact]
    public void Transform_WithoutFormat_RendersRawValue()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ValueFields =
            [
                new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }
            ]
        };

        var result = PivotEngine.Transform(TestData, config, Fields);

        var foodRow = result.Rows.FirstOrDefault(r => r.Key == "Food");
        foodRow.Should().NotBeNull();
        result.Cells![foodRow!.RowIndex, 0].FormattedValue.Should().Be("1750");
    }

    // ═══════════════════════════════════════════════════════════════
    //  2.13  Sorting
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Transform_SortRows_AscendingByDimension_OrdersAlphabetically()
    {
        var fields = new List<PivotField<Transaction>>(Fields)
        {
            [0] = new PivotField<Transaction>
            {
                Key = "Category",
                Title = "Category",
                Accessor = t => t.Category,
                SortDirection = PivotSortDirection.Ascending,
                SortBy = PivotSortBy.Value
            }
        };

        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ValueFields = [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }]
        };

        var result = PivotEngine.Transform(TestData, config, fields);

        var rowOrder = result.Rows.Select(r => r.DisplayValue).ToList();
        rowOrder.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Transform_SortRows_DescendingByDimension_OrdersReverseAlphabetically()
    {
        var fields = new List<PivotField<Transaction>>(Fields)
        {
            [0] = new PivotField<Transaction>
            {
                Key = "Category",
                Title = "Category",
                Accessor = t => t.Category,
                SortDirection = PivotSortDirection.Descending,
                SortBy = PivotSortBy.Value
            }
        };

        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ValueFields = [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }]
        };

        var result = PivotEngine.Transform(TestData, config, fields);

        var rowOrder = result.Rows.Select(r => r.DisplayValue).ToList();
        rowOrder.Should().BeInDescendingOrder();
    }

    [Fact]
    public void Transform_SortRows_AscendingByAggregate_OrdersByTotal()
    {
        var fields = new List<PivotField<Transaction>>(Fields)
        {
            [0] = new PivotField<Transaction>
            {
                Key = "Category",
                Title = "Category",
                Accessor = t => t.Category,
                SortDirection = PivotSortDirection.Ascending,
                SortBy = PivotSortBy.Aggregate
            }
        };

        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ValueFields = [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }]
        };

        var result = PivotEngine.Transform(TestData, config, fields);

        // Transport total = 630, Food total = 1750
        result.Rows[0].DisplayValue.Should().Be("Transport"); // lower total first
        result.Rows[1].DisplayValue.Should().Be("Food");      // higher total second
    }

    [Fact]
    public void Transform_SortRows_DescendingByAggregate_OrdersByTotalDesc()
    {
        var fields = new List<PivotField<Transaction>>(Fields)
        {
            [0] = new PivotField<Transaction>
            {
                Key = "Category",
                Title = "Category",
                Accessor = t => t.Category,
                SortDirection = PivotSortDirection.Descending,
                SortBy = PivotSortBy.Aggregate
            }
        };

        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ValueFields = [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }]
        };

        var result = PivotEngine.Transform(TestData, config, fields);

        // Food total = 1750, Transport total = 630
        result.Rows[0].DisplayValue.Should().Be("Food");      // higher total first
        result.Rows[1].DisplayValue.Should().Be("Transport"); // lower total second
    }

    [Fact]
    public void Transform_SortColumns_AscendingByDimension_OrdersAlphabetically()
    {
        var fields = new List<PivotField<Transaction>>(Fields)
        {
            [2] = new PivotField<Transaction>
            {
                Key = "Month",
                Title = "Month",
                Accessor = t => t.Month,
                SortDirection = PivotSortDirection.Ascending,
                SortBy = PivotSortBy.Value
            }
        };

        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ColumnFieldKeys = ["Month"],
            ValueFields = [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }]
        };

        var result = PivotEngine.Transform(TestData, config, fields);

        var colOrder = result.Columns.Select(c => c.DisplayValue).ToList();
        colOrder.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Transform_SortClear_KeepsNaturalOrder()
    {
        var fields = new List<PivotField<Transaction>>(Fields)
        {
            [0] = new PivotField<Transaction>
            {
                Key = "Category",
                Title = "Category",
                Accessor = t => t.Category,
                SortDirection = PivotSortDirection.None,
                SortBy = PivotSortBy.Value
            }
        };

        var config = new PivotTableConfiguration
        {
            RowFieldKeys = ["Category"],
            ValueFields = [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }]
        };

        var result = PivotEngine.Transform(TestData, config, fields);

        var rowOrder = result.Rows.Select(r => r.DisplayValue).ToList();
        rowOrder.Should().Contain("Food").And.Contain("Transport");
    }
}
