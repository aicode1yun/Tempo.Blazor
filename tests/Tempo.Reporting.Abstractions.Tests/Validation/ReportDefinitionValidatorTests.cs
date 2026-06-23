using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Validation;

namespace Tempo.Reporting.Abstractions.Tests.Validation;

public sealed class ReportDefinitionValidatorTests
{
    [Fact]
    public void Validate_InvalidDefinition_ReportsCoreRuleCodes()
    {
        var definition = new ReportDefinition
        {
            SchemaVersion = 0,
            Name = "",
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(0, 100),
                Margins = new ReportThickness(-1, 0, 0, 0),
            },
            Parameters =
            [
                new ReportParameterDefinition
                {
                    Name = "Region",
                    DataType = ReportParameterType.String,
                    AllowMultipleValues = true,
                    Hidden = true,
                },
                new ReportParameterDefinition
                {
                    Name = "Region",
                    DataType = ReportParameterType.List,
                    AvailableValues = ReportParameterAvailableValues.Static([]),
                },
                new ReportParameterDefinition
                {
                    Name = "Bad Name",
                    DataType = ReportParameterType.List,
                    AvailableValues = ReportParameterAvailableValues.FromDataSet("", "", ""),
                },
            ],
            DataSets =
            [
                new ReportDataSetDefinition { Name = "" },
                new ReportDataSetDefinition { Name = "Orders" },
                new ReportDataSetDefinition { Name = "Orders" },
            ],
            Bands = new ReportBandCollection
            {
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = -1,
                    Elements =
                    [
                        new ReportTextBoxElement
                        {
                            Id = "",
                            X = -1,
                            Y = 0,
                            Width = 0,
                            Height = 0,
                        },
                        new ReportTextBoxElement
                        {
                            Id = "dup",
                            X = 0,
                            Y = 0,
                            Width = 12,
                            Height = 12,
                        },
                        new ReportImageElement
                        {
                            Id = "dup",
                            X = 0,
                            Y = 0,
                            Width = 12,
                            Height = 12,
                        },
                        new ReportTableElement
                        {
                            Id = "empty-table",
                            X = 0,
                            Y = 0,
                            Width = 12,
                            Height = 12,
                        },
                        new ReportChartElement
                        {
                            Id = "empty-chart",
                            X = 0,
                            Y = 0,
                            Width = 12,
                            Height = 12,
                        },
                        new ReportChartElement
                        {
                            Id = "invalid-chart-series",
                            X = 0,
                            Y = 0,
                            Width = 12,
                            Height = 12,
                            Series = [new ReportChartSeries { Name = "Actual" }],
                        },
                        new ReportSubReportElement
                        {
                            Id = "empty-subreport",
                            X = 0,
                            Y = 0,
                            Width = 12,
                            Height = 12,
                        },
                    ],
                },
            },
        };

        var result = new ReportDefinitionValidator(ReportingValidationTestLocalizer.Create()).Validate(definition);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorCode).Should().Contain(
        [
            "ReportDefinition.SchemaVersion.Unsupported",
            "ReportDefinition.Name.Required",
            "ReportDefinition.PageSetup.Size",
            "ReportDefinition.PageSetup.Margins",
            "ReportDefinition.Bands.Detail.Required",
            "ReportDefinition.Bands.Height",
            "ReportDefinition.Parameters.Name.Duplicate",
            "ReportDefinition.DataSets.Name.Required",
            "ReportDefinition.DataSets.Name.Duplicate",
            "ReportDefinition.Elements.Id.Required",
            "ReportDefinition.Elements.Id.Duplicate",
            "ReportElement.Bounds.Invalid",
            "ReportTextBox.Content.Required",
            "ReportImage.Source.Required",
            "ReportTable.Columns.Required",
            "ReportChart.Series.Required",
            "ReportChart.Series.CategoryExpression.Required",
            "ReportChart.Series.ValueExpression.Required",
            "ReportSubReport.ReportId.Required",
            "ReportParameter.Name.Invalid",
            "ReportParameter.Multiple.RequiresList",
            "ReportParameter.Hidden.RequiresDefault",
            "ReportParameter.AvailableValues.Static.Required",
            "ReportParameter.AvailableValues.DataSet.Required",
        ]);
    }

    [Fact]
    public void Validate_EmptyName_UsesLocalizedMessages()
    {
        var definition = new ReportDefinition
        {
            Name = "",
            Bands = new ReportBandCollection
            {
                Detail = new ReportBand { Kind = ReportBandKind.Detail, Height = 12 },
            },
        };

        var validator = new ReportDefinitionValidator(ReportingValidationTestLocalizer.Create());

        var english = ReportingValidationTestLocalizer.InCulture("en", () =>
            validator.Validate(definition).Errors.First(e => e.ErrorCode == "ReportDefinition.Name.Required").ErrorMessage);
        var czech = ReportingValidationTestLocalizer.InCulture("cs", () =>
            validator.Validate(definition).Errors.First(e => e.ErrorCode == "ReportDefinition.Name.Required").ErrorMessage);

        english.Should().Be("The report name is required.");
        czech.Should().Be("Název reportu je povinný.");
    }
}
