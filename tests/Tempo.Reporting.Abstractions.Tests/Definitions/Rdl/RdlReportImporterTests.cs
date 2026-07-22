using System.Reflection;
using FluentValidation;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Definitions.Rdl;
using Tempo.Reporting.Abstractions.Serialization;
using Tempo.Reporting.Abstractions.Tests.Validation;
using Tempo.Reporting.Abstractions.Validation;

namespace Tempo.Reporting.Abstractions.Tests.Definitions.Rdl;

public sealed class RdlReportImporterTests
{
    private static readonly RdlReportImporter Importer = new();

    private static string LoadFixture(string fileName)
    {
        var assembly = typeof(RdlReportImporterTests).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith($".{fileName}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void AssertPassesValidator(ReportDefinition definition)
    {
        var validator = new ReportDefinitionValidator(ReportingValidationTestLocalizer.Create());
        var result = validator.Validate(definition);
        result.IsValid.Should().BeTrue(
            because: "imported definitions must satisfy the canonical validator; failures: {0}",
            string.Join("; ", result.Errors.Select(error => $"{error.ErrorCode}:{error.PropertyName}")));
    }

    [Fact]
    public void Import_TextboxReport_MapsPageParametersAndTextboxes()
    {
        var result = Importer.Import(LoadFixture("textbox-report.rdl"));

        result.HasErrors.Should().BeFalse();
        var definition = result.Definition;

        // 8.5in x 11in = 612 x 792 pt; margins 1in = 72 pt each.
        definition.PageSetup.PageSize.Width.Should().BeApproximately(612, 0.5);
        definition.PageSetup.PageSize.Height.Should().BeApproximately(792, 0.5);
        definition.PageSetup.Margins.Left.Should().BeApproximately(72, 0.5);
        definition.Description.Should().Be("A simple textbox-only report.");

        var elements = definition.Bands.Detail!.Elements;
        elements.Should().HaveCount(2);
        var title = elements.OfType<ReportTextBoxElement>().Single(box => box.Id == "Title");
        title.Text.Should().Be("Quarterly Sales Summary");
        title.Expression.Should().BeNull();
        title.TextStyle.Bold.Should().BeTrue();
        title.TextStyle.FontSize.Should().BeApproximately(18, 0.01);
        title.HorizontalAlignment.Should().Be(ReportHorizontalAlignment.Center);
        title.CanGrow.Should().BeTrue();
        title.TextDirection.Should().Be(ReportTextDirection.Auto);

        var generated = elements.OfType<ReportTextBoxElement>().Single(box => box.Id == "Generated");
        generated.Expression.Should().Be("=Globals!ExecutionTime");
        generated.Text.Should().BeNull();

        AssertPassesValidator(definition);
    }

    [Fact]
    public void Import_TablixReport_MapsColumnsHeaderAndDetail()
    {
        var result = Importer.Import(LoadFixture("tablix-report.rdl"));

        result.HasErrors.Should().BeFalse();
        var definition = result.Definition;

        var table = definition.Bands.Detail!.Elements.OfType<ReportTableElement>().Single();
        table.Id.Should().Be("SalesTable");
        table.DataSetName.Should().Be("Sales");
        table.Columns.Should().HaveCount(3);
        table.Columns[0].Header.Should().Be("Product");
        table.Header!.Cells.Should().HaveCount(3);
        table.Header.Cells[2].Text.Should().Be("Total");
        table.Detail.Cells[0].Expression.Should().Be("=Fields!Product.Value");
        table.Detail.Cells[0].TextDirection.Should().Be(ReportTextDirection.Auto);

        AssertPassesValidator(definition);
    }

    [Fact]
    public void Import_StackedChartReport_MapsStackedColumnTypeAndSeries()
    {
        var result = Importer.Import(LoadFixture("chart-stacked.rdl"));

        result.HasErrors.Should().BeFalse();
        var definition = result.Definition;

        var chart = definition.Bands.Detail!.Elements.OfType<ReportChartElement>().Single();
        chart.ChartType.Should().Be(ReportChartType.StackedColumn);
        chart.Title.Should().Be("Revenue by Quarter");
        chart.DataSetName.Should().Be("Revenue");
        chart.Series.Should().HaveCount(2);
        chart.Series[0].Name.Should().Be("Online");
        chart.Series[0].ValueExpression.Should().Be("=Sum(Fields!Online.Value)");
        chart.Series[0].CategoryExpression.Should().Be("=Fields!Quarter.Value");

        AssertPassesValidator(definition);
    }

    [Fact]
    public void Import_ParametersAndDataSetReport_MapsParametersDataSetAndSource()
    {
        var result = Importer.Import(LoadFixture("parameters-dataset.rdl"));

        result.HasErrors.Should().BeFalse();
        var definition = result.Definition;

        definition.Parameters.Should().HaveCount(3);
        var region = definition.Parameters.Single(parameter => parameter.Name == "Region");
        region.Label.Should().Be("Region");
        region.DataType.Should().Be(ReportParameterType.String);
        region.DefaultExpression.Should().Be("All");
        region.AvailableValues.Should().NotBeNull();
        region.AvailableValues!.StaticValues.Should().HaveCount(3);
        region.AvailableValues.StaticValues[1].Value.Should().Be("EU");
        region.AvailableValues.StaticValues[1].Label.Should().Be("Europe");

        var includeClosed = definition.Parameters.Single(parameter => parameter.Name == "IncludeClosed");
        includeClosed.DataType.Should().Be(ReportParameterType.Boolean);
        includeClosed.Required.Should().BeFalse();

        var asOf = definition.Parameters.Single(parameter => parameter.Name == "AsOf");
        asOf.DataType.Should().Be(ReportParameterType.Date);
        asOf.Hidden.Should().BeTrue();

        var dataSet = definition.DataSets.Single();
        dataSet.Name.Should().Be("Orders");
        dataSet.Source!.Name.Should().Be("ErpDb");
        dataSet.Query.Should().Contain("SELECT Region");
        dataSet.Fields.Should().HaveCount(3);
        dataSet.Fields.Single(field => field.Name == "Total").DataType.Should().Be(ReportDataFieldType.Number);
        dataSet.Fields.Single(field => field.Name == "OrderDate").DataType.Should().Be(ReportDataFieldType.Date);
        dataSet.Parameters.Single().Name.Should().Be("@Region");

        // The data-source connection string must be diagnosed as not-imported (security), never silently kept.
        result.Warnings.Should().Contain(warning => warning.ElementPath.Contains("DataSource[ErpDb]"));

        AssertPassesValidator(definition);
    }

    [Fact]
    public void Import_TelerikNamespacedReport_ParsesNamespaceAgnostically()
    {
        var result = Importer.Import(LoadFixture("telerik-report.rdl"));

        result.HasErrors.Should().BeFalse();
        var definition = result.Definition;

        definition.Bands.Detail!.Elements.OfType<ReportTextBoxElement>().Single().Text.Should().Be("Telerik Export");
        var table = definition.Bands.Detail.Elements.OfType<ReportTableElement>().Single();
        table.Columns.Should().HaveCount(2);
        table.Header!.Cells[0].Text.Should().Be("Name");
        table.Detail.Cells[1].Expression.Should().Be("=Fields!Quantity.Value");

        // pt sizes convert 1:1 to the model's point unit.
        table.Columns[0].Width.Should().BeApproximately(150, 0.01);

        AssertPassesValidator(definition);
    }

    [Fact]
    public void Import_UnsupportedConstructs_ProducesWarningsButStillValidDefinition()
    {
        var result = Importer.Import(LoadFixture("unsupported.rdl"));

        result.HasErrors.Should().BeFalse();
        result.Warnings.Should().Contain(warning => warning.Message.Contains("Subreport", StringComparison.OrdinalIgnoreCase));
        result.Warnings.Should().Contain(warning => warning.Message.Contains("Gauge", StringComparison.OrdinalIgnoreCase));
        result.Warnings.Should().Contain(warning => warning.Message.Contains("Custom code", StringComparison.OrdinalIgnoreCase));

        // The supported textbox is still imported and the definition remains valid despite the skipped items.
        result.Definition.Bands.Detail!.Elements.OfType<ReportTextBoxElement>().Single().Text.Should().Be("Dashboard");
        AssertPassesValidator(result.Definition);
    }

    [Fact]
    public void Import_ReportSectionsShape_MapsNestedBodyItems()
    {
        // REGRESSION GUARD: SSRS 2010/2016 nests the body as Report/ReportSections/ReportSection/Body.
        // Resolving only a direct <Body> child silently imported an EMPTY report for the dominant format.
        var result = Importer.Import(LoadFixture("reportsections-2016.rdl"));

        result.HasErrors.Should().BeFalse();
        var definition = result.Definition;

        var elements = definition.Bands.Detail!.Elements;
        elements.Should().HaveCount(2);
        elements.OfType<ReportTextBoxElement>().Single().Text.Should().Be("Section Body Title");
        var table = elements.OfType<ReportTableElement>().Single();
        table.Columns.Should().HaveCount(2);
        table.Detail.Cells[1].Expression.Should().Be("=Fields!Total.Value");

        // Page setup lives inside the section too, and must still be picked up.
        definition.PageSetup.PageSize.Width.Should().BeApproximately(612, 0.5);
        definition.DataSets.Single().Name.Should().Be("Sales");

        // A SINGLE-section report is imported in full, so it must NOT claim sections were dropped.
        result.Warnings.Should().NotContain(warning => warning.ElementPath.Contains("ReportSections", StringComparison.Ordinal));

        AssertPassesValidator(definition);
    }

    [Fact]
    public void Import_MultipleSections_WarnsThatOnlyTheFirstIsImported()
    {
        const string rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <ReportSections>
                <ReportSection><Body><ReportItems>
                  <Textbox Name="A"><Value>First</Value><Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>1in</Width></Textbox>
                </ReportItems></Body></ReportSection>
                <ReportSection><Body><ReportItems>
                  <Textbox Name="B"><Value>Second</Value><Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>1in</Width></Textbox>
                </ReportItems></Body></ReportSection>
              </ReportSections>
            </Report>
            """;

        var result = Importer.Import(rdl);

        result.Definition.Bands.Detail!.Elements.OfType<ReportTextBoxElement>().Single().Text.Should().Be("First");
        result.Warnings.Should().Contain(warning =>
            warning.ElementPath.Contains("ReportSections", StringComparison.Ordinal)
            && warning.Message.Contains("2 sections", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_EmptyTextbox_DoesNotInventItsNameAsVisibleText()
    {
        // A designer-generated name like "Textbox27" must never become printed report content.
        const string rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Textbox Name="Textbox27"><Top>0in</Top><Left>0in</Left><Height>0.2in</Height><Width>2in</Width></Textbox>
              </ReportItems></Body>
            </Report>
            """;

        var result = Importer.Import(rdl);

        // The model requires non-empty text content, so an empty RDL textbox is dropped with a diagnostic
        // rather than being backfilled with its designer name.
        result.Definition.Bands.Detail!.Elements.Should().BeEmpty();
        result.Warnings.Should().Contain(warning =>
            warning.Message.Contains("empty text box", StringComparison.OrdinalIgnoreCase)
            && warning.ElementPath.Contains("Textbox27", StringComparison.Ordinal));
        result.Diagnostics.Should().NotContain(diagnostic => diagnostic.Message.Contains("used its name", StringComparison.OrdinalIgnoreCase));

        AssertPassesValidator(result.Definition);
    }

    [Fact]
    public void Import_UnknownChartType_WarnsAboutTheColumnFallback()
    {
        const string rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Chart Name="Scatter1">
                  <Top>0in</Top><Left>0in</Left><Height>3in</Height><Width>4in</Width>
                  <ChartData><ChartSeriesCollection><ChartSeries Name="S1">
                    <ChartDataPoints><ChartDataPoint><ChartDataPointValues><Y>=Fields!V.Value</Y></ChartDataPointValues></ChartDataPoint></ChartDataPoints>
                    <Type>Scatter</Type>
                  </ChartSeries></ChartSeriesCollection></ChartData>
                  <ChartCategoryHierarchy><ChartMembers><ChartMember><Group Name="G">
                    <GroupExpressions><GroupExpression>=Fields!C.Value</GroupExpression></GroupExpressions>
                  </Group></ChartMember></ChartMembers></ChartCategoryHierarchy>
                </Chart>
              </ReportItems></Body>
            </Report>
            """;

        var result = Importer.Import(rdl);

        result.Definition.Bands.Detail!.Elements.OfType<ReportChartElement>().Single()
            .ChartType.Should().Be(ReportChartType.Column);
        result.Warnings.Should().Contain(warning => warning.Message.Contains("Scatter", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_ColumnGroupedTablix_WarnsThatItWasFlattened()
    {
        const string rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Tablix Name="Pivot">
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>4in</Width>
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>2in</Width></TablixColumn></TablixColumns>
                    <TablixRows><TablixRow><Height>0.3in</Height><TablixCells>
                      <TablixCell><CellContents><Textbox Name="c1"><Value>Head</Value></Textbox></CellContents></TablixCell>
                    </TablixCells></TablixRow></TablixRows>
                  </TablixBody>
                  <TablixColumnHierarchy><TablixMembers><TablixMember>
                    <Group Name="ByMonth"><GroupExpressions><GroupExpression>=Fields!Month.Value</GroupExpression></GroupExpressions></Group>
                  </TablixMember></TablixMembers></TablixColumnHierarchy>
                </Tablix>
              </ReportItems></Body>
            </Report>
            """;

        var result = Importer.Import(rdl);

        result.Warnings.Should().Contain(warning => warning.Message.Contains("Column grouping", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_HiddenReportItem_WarnsThatVisibilityIsNotImported()
    {
        // Dropping Hidden=true silently turns an invisible RDL item into a visible one.
        const string rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Textbox Name="Secret">
                  <Value>Hidden content</Value>
                  <Visibility><Hidden>true</Hidden></Visibility>
                  <Top>0in</Top><Left>0in</Left><Height>0.2in</Height><Width>2in</Width>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;

        var result = Importer.Import(rdl);

        result.Warnings.Should().Contain(warning =>
            warning.Message.Contains("Visibility", StringComparison.Ordinal)
            && warning.ElementPath.Contains("Secret", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_MalformedXml_ReturnsErrorResult_WithoutThrowing()
    {
        var act = () => Importer.Import(LoadFixture("malformed.rdl"));

        var result = act.Should().NotThrow().Subject;
        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Severity.Should().Be(RdlDiagnosticSeverity.Error);
    }

    [Fact]
    public void Import_EmptyInput_ReturnsErrorResult()
    {
        var result = Importer.Import("   ");

        result.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void Import_NonReportRoot_ReturnsErrorResult()
    {
        var result = Importer.Import("<NotAReport></NotAReport>");

        result.HasErrors.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("Report");
    }

    [Fact]
    public void Import_MappedDefinition_RoundTripsThroughCanonicalSerializer()
    {
        var result = Importer.Import(LoadFixture("chart-stacked.rdl"));

        var json = ReportDefinitionJsonSerializer.Serialize(result.Definition);
        var roundTripped = ReportDefinitionJsonSerializer.Deserialize(json);

        roundTripped.Bands.Detail!.Elements.OfType<ReportChartElement>().Single().ChartType
            .Should().Be(ReportChartType.StackedColumn);
    }
}
