using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Processing;

namespace Tempo.Reporting.Engine.Tests.Processing;

public sealed class BandInstantiationTests
{
    [Fact]
    public async Task Instantiate_CreatesDetailBandInstancesWithEvaluatedTextAndVisibility()
    {
        var dataSet = await OrdersAsync(
            ("Ada", 125m),
            ("Borek", 40m));
        var definition = new ReportDefinition
        {
            Name = "Orders",
            Bands = new ReportBandCollection
            {
                Detail = new ReportBand
                {
                    Kind = ReportBandKind.Detail,
                    Height = 20,
                    Elements =
                    [
                        new ReportTextBoxElement { Id = "name", Text = "Customer" },
                        new ReportTextBoxElement { Id = "value", Expression = "=Fields.Customer + \":\" + Fields.Total" },
                        new ReportTextBoxElement
                        {
                            Id = "highValue",
                            Text = "High value",
                            VisibleExpression = "=Fields.Total >= 100",
                        },
                    ],
                },
            },
        };

        var instance = ReportBandInstantiator.Instantiate(definition, dataSet, Context(dataSet));

        instance.Bands.Should().HaveCount(2);
        instance.Bands[0].Kind.Should().Be(ReportBandKind.Detail);
        instance.Bands[0].Elements.OfType<ReportTextBoxInstance>()
            .Single(element => element.ElementId == "value")
            .Runs.Should().ContainSingle(run => run.Text == "Ada:125");
        instance.Bands[0].Elements.Should().Contain(element => element.ElementId == "highValue");
        instance.Bands[1].Elements.Should().NotContain(element => element.ElementId == "highValue");
    }

    [Fact]
    public async Task Instantiate_ExpandsSubReportParameterMappingsAndEnforcesDepthLimit()
    {
        var dataSet = await OrdersAsync(("Ada", 125m));
        var definition = new ReportDefinition
        {
            Name = "Orders",
            Bands = new ReportBandCollection
            {
                Detail = new ReportBand
                {
                    Kind = ReportBandKind.Detail,
                    Height = 20,
                    Elements =
                    [
                        new ReportSubReportElement
                        {
                            Id = "invoiceLines",
                            ReportId = "InvoiceLines",
                            ParameterMappings =
                            [
                                new ReportSubReportParameterMapping("Customer", "=Fields.Customer"),
                                new ReportSubReportParameterMapping("MinimumTotal", "=Fields.Total"),
                            ],
                        },
                    ],
                },
            },
        };

        var instance = ReportBandInstantiator.Instantiate(
            definition,
            dataSet,
            Context(dataSet),
            new ReportProcessingOptions { MaxSubReportDepth = 1 });

        var subReport = instance.Bands.Single().Elements.OfType<ReportSubReportInstance>().Single();
        subReport.ReportId.Should().Be("InvoiceLines");
        subReport.ParameterValues["Customer"].ScalarValue.Should().Be("Ada");
        subReport.ParameterValues["MinimumTotal"].ScalarValue.Should().Be(125m);

        var act = () => ReportBandInstantiator.Instantiate(
            definition,
            dataSet,
            Context(dataSet),
            new ReportProcessingOptions { MaxSubReportDepth = 0 });

        act.Should().Throw<ReportProcessingException>()
            .Which.Code.Should().Be("Processing.SubReportDepthExceeded");
    }

    private static ReportProcessingContext Context(ProcessedDataSet dataSet)
        => new(
            new ReportExecutionContext("tenant", "user", "en-US"),
            new Dictionary<string, ReportParameterValue>(),
            new Dictionary<string, ProcessedDataSet> { [dataSet.Name] = dataSet });

    private static Task<ProcessedDataSet> OrdersAsync(params (string Customer, decimal Total)[] rows)
        => ReportDataSetRuntime.LoadAsync(
            "Orders",
            new ReportDataSetResult(
                [
                    new ReportDataColumn("Customer", DataFieldType.String),
                    new ReportDataColumn("Total", DataFieldType.Number),
                ],
                Rows(rows)),
            CancellationToken.None);

    private static async IAsyncEnumerable<ReportDataRow> Rows(IEnumerable<(string Customer, decimal Total)> rows)
    {
        foreach (var row in rows)
        {
            await Task.Yield();
            yield return new ReportDataRow(new Dictionary<string, object?>
            {
                ["Customer"] = row.Customer,
                ["Total"] = row.Total,
            });
        }
    }
}
