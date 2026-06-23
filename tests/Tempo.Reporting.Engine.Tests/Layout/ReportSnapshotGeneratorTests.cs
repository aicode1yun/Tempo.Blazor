using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Processing;
using Tempo.Reporting.Engine.Snapshot;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;

namespace Tempo.Reporting.Engine.Tests.Layout;

public sealed class ReportSnapshotGeneratorTests
{
    [Fact]
    public void Generate_SubstitutesPageNumberAndTotalPagesAfterPagination()
    {
        var definition = new ReportDefinition
        {
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(220, 180),
                Margins = new ReportThickness(10),
            },
            Bands = new ReportBandCollection
            {
                PageFooter = new ReportBand
                {
                    Kind = ReportBandKind.PageFooter,
                    Height = 20,
                    Elements =
                    [
                        TextBox("page-number", "Page PageNumber / TotalPages", 0, 2, 180, 14, ReportHorizontalAlignment.Right),
                    ],
                },
            },
        };
        var detail = new ReportBand
        {
            Kind = ReportBandKind.Detail,
            Height = 70,
            Elements = [TextBox("detail", "Detail row", 0, 0, 160, 14)],
        };
        var instance = new ReportInstance(definition, [Instance(detail), Instance(detail), Instance(detail)]);

        var snapshot = ReportSnapshotGenerator.Generate(instance, new FixedTextMeasurer());

        snapshot.Pages.Should().HaveCount(2);
        PageNumberText(snapshot.Pages[0]).Should().Be("Page1/2");
        PageNumberText(snapshot.Pages[1]).Should().Be("Page2/2");
    }

    [Fact]
    public void Generate_ProducesRoundTripStableSnapshotWithAbsolutePrimitives()
    {
        var definition = new ReportDefinition
        {
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(260, 180),
                Margins = new ReportThickness(12),
            },
        };
        var band = new ReportBand
        {
            Kind = ReportBandKind.Detail,
            Height = 82,
            Elements =
            [
                new ReportShapeElement
                {
                    Id = "box",
                    X = 8,
                    Y = 4,
                    Width = 210,
                    Height = 40,
                    FillColor = "#f8fafc",
                    Border = ReportBorder.All("#cbd5e1", 1),
                },
                new ReportImageElement
                {
                    Id = "logo",
                    X = 12,
                    Y = 8,
                    Width = 32,
                    Height = 24,
                    Source = "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg'/%3E",
                },
                TextBox("caption", "Deterministic output", 52, 12, 150, 16),
                new ReportLineElement
                {
                    Id = "rule",
                    X = 8,
                    Y = 58,
                    Width = 210,
                    Height = 0,
                    Stroke = new ReportBorderLine("#94a3b8", 1),
                },
            ],
        };
        var instance = new ReportInstance(definition, [Instance(band)]);

        var first = ReportSnapshotGenerator.Generate(instance, new FixedTextMeasurer(), new ReportSnapshotGeneratorOptions { SnapshotId = "stable" });
        var second = ReportSnapshotGenerator.Generate(instance, new FixedTextMeasurer(), new ReportSnapshotGeneratorOptions { SnapshotId = "stable" });
        var json = ReportSnapshotJsonSerializer.Serialize(first);
        var roundTripJson = ReportSnapshotJsonSerializer.Serialize(ReportSnapshotJsonSerializer.Deserialize(json));

        ReportSnapshotJsonSerializer.Serialize(second).Should().Be(json);
        roundTripJson.Should().Be(json);
        first.Pages.Should().ContainSingle();
        first.Pages[0].Commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.Image && command.Source!.StartsWith("data:image/svg+xml", StringComparison.Ordinal));
        first.Pages[0].Commands.Should().Contain(command => command.Id.EndsWith("caption-text-0", StringComparison.Ordinal) && command.X == 64);
    }

    [Fact]
    public void Generate_PaginatesTableBandAndRepeatsTableHeaderInSnapshot()
    {
        var dataSet = TableRows(Enumerable.Range(1, 8).Select(index => ($"Item {index}", (decimal)index)));
        var table = new ReportTableElement
        {
            Id = "items",
            DataSetName = "Items",
            X = 0,
            Y = 0,
            Width = 180,
            Height = 80,
            RepeatHeaderOnNewPage = true,
            Columns = [new ReportTableColumn("Name", 120), new ReportTableColumn("Total", 60)],
            Header = TableRow("Name", "Total"),
            Detail = TableRow("=Fields.Name", "=Fields.Total"),
        };
        var definition = new ReportDefinition
        {
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(220, 150),
                Margins = new ReportThickness(10),
            },
        };
        var band = new ReportBand
        {
            Kind = ReportBandKind.Detail,
            Height = 80,
            Elements = [table],
        };
        var context = new ReportProcessingContext(
            new ReportExecutionContext("tenant", "user", "en-US"),
            dataSets: new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal) { ["Items"] = dataSet });
        var instance = new ReportInstance(
            definition,
            [new ReportBandInstance(ReportBandKind.Detail, null, null, [new ReportElementInstance(table, null, null)], sourceBand: band)],
            context.DataSets,
            context);

        var snapshot = ReportSnapshotGenerator.Generate(instance, new FixedTextMeasurer());

        snapshot.Pages.Should().HaveCountGreaterThan(1);
        snapshot.Pages[1].Commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.TextRun && command.Text == "Name");
        snapshot.Pages.SelectMany(page => page.Commands)
            .Should().Contain(command => command.Type == ReportSnapshotCommandType.TextRun && command.Text == "Item");
    }

    private static ReportTextBoxElement TextBox(
        string id,
        string text,
        double x,
        double y,
        double width,
        double height,
        ReportHorizontalAlignment alignment = ReportHorizontalAlignment.Left)
        => new()
        {
            Id = id,
            Text = text,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            HorizontalAlignment = alignment,
            Padding = new ReportThickness(0),
            TextStyle = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 },
        };

    private static ReportBandInstance Instance(ReportBand band)
    {
        var elements = band.Elements
            .Select(element => element is ReportTextBoxElement textBox
                ? new ReportTextBoxInstance(textBox, textBox.Text, textBox.Text ?? string.Empty)
                : new ReportElementInstance(element, null, null))
            .ToArray();
        return new ReportBandInstance(band.Kind, null, null, elements, sourceBand: band);
    }

    private static string PageNumberText(ReportSnapshotPage page)
        => string.Concat(page.Commands
            .Where(command => command.Type == ReportSnapshotCommandType.TextRun && command.Id.Contains("page-number", StringComparison.Ordinal))
            .Select(command => command.Text));

    private static ReportTableRow TableRow(params string[] values)
        => new()
        {
            Height = 20,
            Cells = values.Select(value => new ReportTableCell
            {
                Text = value.StartsWith('=') ? null : value,
                Expression = value.StartsWith('=') ? value : null,
                TextStyle = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 },
                Padding = new ReportThickness(2),
            }).ToList(),
        };

    private static ProcessedDataSet TableRows(IEnumerable<(string Name, decimal Total)> rows)
        => new(
            "Items",
            [
                new ReportDataColumn("Name", DataFieldType.String),
                new ReportDataColumn("Total", DataFieldType.Number),
            ],
            rows.Select(row => new ProcessedDataRow(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Name"] = row.Name,
                ["Total"] = row.Total,
            })).ToArray());
}
