using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Abstractions.PivotTable;
using Tempo.Blazor.Components.PivotTable;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.PivotTable;

public class TmPivotTableTemplateTests : LocalizationTestBase
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

    private static RenderFragment<PivotCellContext> CellTemplate(string cssClass) =>
        ctx => builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", cssClass);
            builder.AddContent(2, ctx.FormattedValue);
            builder.CloseElement();
        };

    private static RenderFragment<PivotRowHeaderContext> RowTemplate(string cssClass) =>
        ctx => builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", cssClass);
            builder.AddContent(2, ctx.Text);
            builder.CloseElement();
        };

    private static RenderFragment<PivotColumnHeaderContext> ColTemplate(string cssClass) =>
        ctx => builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", cssClass);
            builder.AddContent(2, ctx.Text);
            builder.CloseElement();
        };

    // ═══════════════════════════════════════════════════════════════
    //  A.1  DataCellTemplate
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotTable_DataCellTemplate_RendersCustomContent()
    {
        var cut = Render<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.DataCellTemplate, CellTemplate("custom-cell"))
        );

        var customCells = cut.FindAll(".custom-cell");
        customCells.Should().NotBeEmpty();
        customCells.Select(c => c.TextContent).Should().Contain("500");
        customCells.Select(c => c.TextContent).Should().Contain("600");
    }

    [Fact]
    public void TmPivotTable_DataCellTemplate_NullValue_HandlesGracefully()
    {
        var cut = Render<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.DataCellTemplate, ctx => builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "class", ctx.IsNull ? "custom-null" : "custom-cell");
                builder.AddContent(2, ctx.IsNull ? "NO DATA" : ctx.FormattedValue);
                builder.CloseElement();
            })
        );

        var customCells = cut.FindAll(".custom-cell");
        customCells.Should().NotBeEmpty();
    }

    [Fact]
    public void TmPivotTable_DataCellTemplate_Context_HasCorrectProperties()
    {
        PivotCellContext? capturedContext = null;

        var cut = Render<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.DataCellTemplate, ctx => builder =>
            {
                capturedContext ??= ctx;
                builder.OpenElement(0, "span");
                builder.AddContent(1, ctx.FormattedValue);
                builder.CloseElement();
            })
        );

        capturedContext.Should().NotBeNull();
        capturedContext!.RowFieldValues.Should().Contain("Food");
        capturedContext.ColumnFieldValues.Should().Contain("Feb");
        capturedContext.ValueFieldIndex.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════
    //  A.2  RowHeaderTemplate
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotTable_RowHeaderTemplate_RendersCustomContent()
    {
        var cut = Render<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.RowHeaderTemplate, RowTemplate("custom-row-header"))
        );

        var customHeaders = cut.FindAll(".custom-row-header");
        customHeaders.Should().NotBeEmpty();
        customHeaders.Select(h => h.TextContent).Should().Contain("Food");
        customHeaders.Select(h => h.TextContent).Should().Contain("Transport");
    }

    [Fact]
    public void TmPivotTable_RowHeaderTemplate_Context_HasCorrectProperties()
    {
        PivotRowHeaderContext? capturedContext = null;

        var cut = Render<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.RowHeaderTemplate, ctx => builder =>
            {
                capturedContext ??= ctx;
                builder.OpenElement(0, "span");
                builder.AddContent(1, ctx.Text);
                builder.CloseElement();
            })
        );

        capturedContext.Should().NotBeNull();
        capturedContext!.Text.Should().BeOneOf("Food", "Transport");
        capturedContext.RowFieldValues.Should().Contain(capturedContext.Text);
        capturedContext.Level.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════
    //  A.3  ColumnHeaderTemplate
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotTable_ColumnHeaderTemplate_RendersCustomContent()
    {
        var cut = Render<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.ColumnHeaderTemplate, ColTemplate("custom-col-header"))
        );

        var customHeaders = cut.FindAll(".custom-col-header");
        customHeaders.Should().NotBeEmpty();
        customHeaders.Select(h => h.TextContent).Should().Contain("Feb");
        customHeaders.Select(h => h.TextContent).Should().Contain("Jan");
    }

    [Fact]
    public void TmPivotTable_ColumnHeaderTemplate_Context_HasCorrectProperties()
    {
        PivotColumnHeaderContext? capturedContext = null;

        var cut = Render<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
            .Add(p => p.ColumnHeaderTemplate, ctx => builder =>
            {
                capturedContext ??= ctx;
                builder.OpenElement(0, "span");
                builder.AddContent(1, ctx.Text);
                builder.CloseElement();
            })
        );

        capturedContext.Should().NotBeNull();
        capturedContext!.Text.Should().BeOneOf("Feb", "Jan");
        capturedContext.ColumnFieldValues.Should().Contain(capturedContext.Text);
        capturedContext.Level.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════
    //  A.4  Without templates (default rendering still works)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmPivotTable_NoTemplates_RendersDefaultContent()
    {
        var cut = Render<TmPivotTable<Transaction>>(parameters => parameters
            .Add(p => p.Items, TestData)
            .Add(p => p.Fields, Fields)
            .Add(p => p.RowFieldKeys, ["Category"])
            .Add(p => p.ColumnFieldKeys, ["Month"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Amount", Aggregation = "Sum" }])
        );

        var cells = cut.FindAll(".tm-pivot-cell");
        cells.Should().NotBeEmpty();
        cells.Select(c => c.TextContent).Should().Contain("500");

        var rowDims = cut.FindAll(".tm-pivot-row-dim");
        rowDims.Should().Contain(r => r.TextContent == "Food");

        var colHeaders = cut.FindAll(".tm-pivot-col-header");
        colHeaders.Should().Contain(c => c.TextContent == "Jan");
    }
}
