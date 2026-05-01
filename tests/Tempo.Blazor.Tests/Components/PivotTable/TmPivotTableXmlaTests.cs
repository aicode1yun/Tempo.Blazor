using FluentAssertions;
using NSubstitute;
using Tempo.Blazor.Abstractions.PivotTable;
using Tempo.Blazor.Components.PivotTable;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.PivotTable;

public class TmPivotTableXmlaTests : LocalizationTestBase
{
    // ═══════════════════════════════════════════════════════════════
    //  XMLA Provider Type
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void DataProviderType_DefaultsToClient()
    {
        var cut = RenderComponent<TmPivotTable<object>>();
        cut.Instance.DataProviderType.Should().Be(PivotDataProviderType.Client);
    }

    [Fact]
    public void DataProviderType_CanBeSetToXmla()
    {
        var cut = RenderComponent<TmPivotTable<object>>(parameters => parameters
            .Add(p => p.DataProviderType, PivotDataProviderType.Xmla));
        cut.Instance.DataProviderType.Should().Be(PivotDataProviderType.Xmla);
    }

    // ═══════════════════════════════════════════════════════════════
    //  XMLA Data Loading
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task RefreshXmlaDataAsync_CallsExecuteQueryAsync()
    {
        var xmla = Substitute.For<IXmlaPivotDataProvider>();
        xmla.ExecuteQueryAsync(Arg.Any<PivotTableConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PivotTableResult
            {
                Rows = [],
                Columns = [],
                Cells = new PivotCell[0, 0],
                GrandTotals = [],
                ValueFieldCount = 0,
                Configuration = new PivotTableConfiguration(),
                LeafRowCount = 0,
                LeafColumnCount = 0
            }));

        var cut = RenderComponent<TmPivotTable<object>>(parameters => parameters
            .Add(p => p.DataProviderType, PivotDataProviderType.Xmla)
            .Add(p => p.XmlaDataProvider, xmla));

        cut.Instance.XmlaDataProvider.Should().NotBeNull();
        cut.Instance.XmlaDataProvider.Should().BeSameAs(xmla);

        var method = typeof(TmPivotTable<object>).GetMethod("RefreshXmlaDataAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => (Task)method!.Invoke(cut.Instance, null)!);

        await xmla.Received(2).ExecuteQueryAsync(Arg.Any<PivotTableConfiguration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RebindAsync_WithXmlaProvider_CallsExecuteQueryAsync()
    {
        var xmla = Substitute.For<IXmlaPivotDataProvider>();
        xmla.ExecuteQueryAsync(Arg.Any<PivotTableConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PivotTableResult
            {
                Rows = [],
                Columns = [],
                Cells = new PivotCell[0, 0],
                GrandTotals = [],
                ValueFieldCount = 0,
                Configuration = new PivotTableConfiguration(),
                LeafRowCount = 0,
                LeafColumnCount = 0
            }));

        var cut = RenderComponent<TmPivotTable<object>>(parameters => parameters
            .Add(p => p.DataProviderType, PivotDataProviderType.Xmla)
            .Add(p => p.XmlaDataProvider, xmla)
            .Add(p => p.RowFieldKeys, ["Dim1"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Measure1" }]));

        await cut.InvokeAsync(() => cut.Instance.RebindAsync());

        await xmla.Received(2).ExecuteQueryAsync(Arg.Any<PivotTableConfiguration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfigurationChanged_WithXmlaProvider_CallsExecuteQueryAsync()
    {
        var xmla = Substitute.For<IXmlaPivotDataProvider>();
        xmla.ExecuteQueryAsync(Arg.Any<PivotTableConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PivotTableResult
            {
                Rows = [],
                Columns = [],
                Cells = new PivotCell[0, 0],
                GrandTotals = [],
                ValueFieldCount = 0,
                Configuration = new PivotTableConfiguration(),
                LeafRowCount = 0,
                LeafColumnCount = 0
            }));

        var cut = RenderComponent<TmPivotTable<object>>(parameters => parameters
            .Add(p => p.DataProviderType, PivotDataProviderType.Xmla)
            .Add(p => p.XmlaDataProvider, xmla)
            .Add(p => p.RowFieldKeys, ["Dim1"])
            .Add(p => p.ValueFields, [new PivotValueFieldConfiguration { FieldKey = "Measure1" }]));

        // Simulate configuration change via InvokeAsync to stay on dispatcher
        var method = cut.Instance.GetType().GetMethod("OnConfigurationPanelChangedAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => (Task)method!.Invoke(cut.Instance, [new PivotTableConfiguration { RowFieldKeys = ["Dim2"] }])!);

        await xmla.Received(2).ExecuteQueryAsync(Arg.Any<PivotTableConfiguration>(), Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════
    //  XMLA Metadata Abstractions
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task XmlaProvider_GetDimensionsAsync_ReturnsDimensions()
    {
        var xmla = Substitute.For<IXmlaPivotDataProvider>();
        xmla.GetDimensionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PivotXmlaDimension>>(
            [
                new PivotXmlaDimension { UniqueName = "[Date]", Caption = "Date", Hierarchies = [] },
                new PivotXmlaDimension { UniqueName = "[Product]", Caption = "Product", Hierarchies = [] }
            ]));

        var dims = await xmla.GetDimensionsAsync();

        dims.Should().HaveCount(2);
        dims[0].Caption.Should().Be("Date");
        dims[1].Caption.Should().Be("Product");
    }

    [Fact]
    public async Task XmlaProvider_GetMeasuresAsync_ReturnsMeasures()
    {
        var xmla = Substitute.For<IXmlaPivotDataProvider>();
        xmla.GetMeasuresAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PivotXmlaMeasure>>(
            [
                new PivotXmlaMeasure { UniqueName = "[Measures].[Sales]", Caption = "Sales", DataType = "Currency", DefaultFormat = "#,##0.00" }
            ]));

        var measures = await xmla.GetMeasuresAsync();

        measures.Should().HaveCount(1);
        measures[0].Caption.Should().Be("Sales");
        measures[0].DefaultFormat.Should().Be("#,##0.00");
    }
}
