using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Validation;

namespace Tempo.Reporting.Abstractions.Tests.Validation;

public sealed class ReportDrillThroughValidationTests
{
    [Fact]
    public void Validate_DrillThroughWithoutTargetAndBadMappings_ReportsCodes()
    {
        var definition = BaseDefinition(new ReportChartElement
        {
            Id = "chart",
            X = 0,
            Y = 0,
            Width = 100,
            Height = 100,
            Series =
            [
                new ReportChartSeries
                {
                    Name = "Actual",
                    CategoryExpression = "=Fields.Status",
                    ValueExpression = "=Fields.Total",
                    // No target and mappings that are each individually invalid.
                    DrillThrough = new ReportDrillThroughAction
                    {
                        ParameterMappings =
                        [
                            new ReportDrillThroughParameterMapping("", ReportDrillThroughSourceKind.Static, "x"),
                            new ReportDrillThroughParameterMapping("Region", ReportDrillThroughSourceKind.Field, ""),
                        ],
                    },
                },
            ],
        });

        var result = new ReportDefinitionValidator(ReportingValidationTestLocalizer.Create()).Validate(definition);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorCode).Should().Contain(
        [
            "ReportDrillThrough.Target.Required",
            "ReportDrillThrough.Mapping.ParameterName.Required",
            "ReportDrillThrough.Mapping.Source.Required",
        ]);
    }

    [Fact]
    public void Validate_TableCellDrillThroughWithoutTarget_ReportsTargetRequired()
    {
        var definition = BaseDefinition(new ReportTableElement
        {
            Id = "table",
            X = 0,
            Y = 0,
            Width = 100,
            Height = 100,
            Columns = [new ReportTableColumn("Customer", 100)],
            Detail = new ReportTableRow
            {
                Cells =
                [
                    new ReportTableCell
                    {
                        Expression = "=Fields.Customer",
                        DrillThrough = new ReportDrillThroughAction(),
                    },
                ],
            },
        });

        var result = new ReportDefinitionValidator(ReportingValidationTestLocalizer.Create()).Validate(definition);

        result.Errors.Select(e => e.ErrorCode).Should().Contain("ReportDrillThrough.Target.Required");
    }

    [Fact]
    public void Validate_ValidDrillThrough_ReportsNoDrillThroughErrors()
    {
        var definition = BaseDefinition(new ReportChartElement
        {
            Id = "chart",
            X = 0,
            Y = 0,
            Width = 100,
            Height = 100,
            Series =
            [
                new ReportChartSeries
                {
                    Name = "Actual",
                    CategoryExpression = "=Fields.Status",
                    ValueExpression = "=Fields.Total",
                    DrillThrough = new ReportDrillThroughAction
                    {
                        TargetReportPath = "Finance/Detail",
                        ParameterMappings =
                        [
                            new ReportDrillThroughParameterMapping("Region", ReportDrillThroughSourceKind.Field, "Region"),
                            new ReportDrillThroughParameterMapping("Mode", ReportDrillThroughSourceKind.Static, ""),
                        ],
                    },
                },
            ],
        });

        var result = new ReportDefinitionValidator(ReportingValidationTestLocalizer.Create()).Validate(definition);

        result.Errors.Select(e => e.ErrorCode)
            .Should().NotContain(code => code.StartsWith("ReportDrillThrough", StringComparison.Ordinal));
    }

    private static ReportDefinition BaseDefinition(ReportElement element)
        => new()
        {
            Name = "Drill-through report",
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(400, 400),
                Margins = new ReportThickness(10),
            },
            Bands = new ReportBandCollection
            {
                Detail = new ReportBand
                {
                    Kind = ReportBandKind.Detail,
                    Height = 200,
                    Elements = [element],
                },
            },
        };
}
