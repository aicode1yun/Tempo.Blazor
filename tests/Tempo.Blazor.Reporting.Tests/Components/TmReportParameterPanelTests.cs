using Tempo.Blazor.Reporting.Components;
using Tempo.Blazor.Reporting.Models;
using Tempo.Blazor.Reporting.Tests.Fixtures;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Blazor.Reporting.Tests.Components;

public sealed class TmReportParameterPanelTests : ReportingComponentTestBase
{
    [Fact]
    public void ParameterPanel_GeneratesControlsForSupportedParameterTypes()
    {
        var cut = Render<TmReportParameterPanel>(parameters => parameters
            .Add(component => component.Parameters, Parameters()));

        cut.Find("[data-testid='tm-report-param-Text']").GetAttribute("type").Should().Be("text");
        cut.Find("[data-testid='tm-report-param-Amount']").GetAttribute("type").Should().Be("number");
        cut.Find("[data-testid='tm-report-param-Date']").GetAttribute("type").Should().Be("date");
        cut.Find("[data-testid='tm-report-param-Enabled']").GetAttribute("type").Should().Be("checkbox");
        cut.Find("[data-testid='tm-report-param-Region']").TagName.Should().Be("SELECT");
        cut.Find("[data-testid='tm-report-param-Tags']").QuerySelectorAll("input[type='checkbox']").Should().HaveCount(2);
    }

    [Fact]
    public void ParameterPanel_ValidatesRequiredValues()
    {
        var cut = Render<TmReportParameterPanel>(parameters => parameters
            .Add(component => component.Parameters, [TextParameter(required: true)]));

        cut.Find("button").Click();

        cut.Find("[data-testid='tm-report-param-error-Text']").TextContent.Should().Contain("Text is required.");
    }

    [Fact]
    public void ParameterPanel_SubmitsChangedValues()
    {
        IReadOnlyDictionary<string, ReportParameterValue>? submitted = null;
        var cut = Render<TmReportParameterPanel>(parameters => parameters
            .Add(component => component.Parameters, Parameters())
            .Add(component => component.OnSubmit, args => submitted = args.Values));

        cut.Find("[data-testid='tm-report-param-Text']").Input("Quarterly");
        cut.Find("[data-testid='tm-report-param-Amount']").Input("42");
        cut.Find("[data-testid='tm-report-param-Date']").Input("2026-06-22");
        cut.Find("[data-testid='tm-report-param-Enabled']").Change(true);
        cut.Find("[data-testid='tm-report-param-Region']").Change("EU");
        cut.Find("[data-testid='tm-report-param-Tags'] input[value='A']").Change(true);
        cut.Find("[data-testid='tm-report-param-Tags'] input[value='B']").Change(true);
        cut.Find("button").Click();

        submitted.Should().NotBeNull();
        submitted!["Text"].ScalarValue.Should().Be("Quarterly");
        submitted["Amount"].ScalarValue.Should().Be("42");
        submitted["Date"].ScalarValue.Should().Be("2026-06-22");
        submitted["Enabled"].ScalarValue.Should().Be(true);
        submitted["Region"].ScalarValue.Should().Be("EU");
        submitted["Tags"].Values.Should().BeEquivalentTo(["A", "B"]);
    }

    private static IReadOnlyList<ReportViewerParameterMetadata> Parameters()
        =>
        [
            TextParameter(required: false),
            new(new ReportParameterDefinition { Name = "Amount", Label = "Amount", DataType = ReportParameterType.Number, Required = false }),
            new(new ReportParameterDefinition { Name = "Date", Label = "Date", DataType = ReportParameterType.Date, Required = false }),
            new(new ReportParameterDefinition { Name = "Enabled", Label = "Enabled", DataType = ReportParameterType.Boolean, Required = false }),
            new(
                new ReportParameterDefinition { Name = "Region", Label = "Region", DataType = ReportParameterType.List, Required = false },
                [new ReportViewerParameterOption("EU", "Europe")]),
            new(
                new ReportParameterDefinition { Name = "Tags", Label = "Tags", DataType = ReportParameterType.List, AllowMultipleValues = true, Required = false },
                [new ReportViewerParameterOption("A", "A"), new ReportViewerParameterOption("B", "B")]),
        ];

    private static ReportViewerParameterMetadata TextParameter(bool required)
        => new(new ReportParameterDefinition { Name = "Text", Label = "Text", DataType = ReportParameterType.String, Required = required });
}
