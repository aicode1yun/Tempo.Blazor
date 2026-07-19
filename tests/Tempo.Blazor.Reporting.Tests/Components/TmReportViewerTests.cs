using Tempo.Blazor.Reporting.Components;
using Tempo.Blazor.Reporting.Models;
using Tempo.Blazor.Reporting.Tests.Fixtures;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Blazor.Reporting.Tests.Components;

public sealed class TmReportViewerTests : ReportingComponentTestBase
{
    [Fact]
    public void Viewer_LoadsMetadata_RendersToolbarAndFirstPage()
    {
        var source = new RecordingReportSource(metadata: Metadata());

        var cut = Render<TmReportViewer>(parameters => parameters
            .Add(component => component.ReportSource, source));

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tm-report-toolbar']").Should().NotBeNull();
            cut.Find("[data-testid='tm-report-page-count']").TextContent.Should().Contain("1 of 2");
            source.MetadataRequests.Should().HaveCount(1);
            source.RenderRequests.Should().HaveCount(1);
        });
    }

    [Fact]
    public void Viewer_ShowsEmptyStateWithoutSource()
    {
        var cut = Render<TmReportViewer>(parameters => parameters
            .Add(component => component.ReportSource, null));

        cut.Find("[data-testid='tm-report-empty-state']").TextContent.Should().Contain("No report source is configured.");
    }

    [Fact]
    public void Viewer_ShowsErrorStateWhenRenderFails()
    {
        var source = new RecordingReportSource(renderException: new InvalidOperationException("Broken report"));

        var cut = Render<TmReportViewer>(parameters => parameters
            .Add(component => component.ReportSource, source));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tm-report-error-state']").TextContent.Should().Contain("Broken report"));
    }

    [Fact]
    public void Viewer_PagesAndZoomsThroughToolbar()
    {
        var source = new RecordingReportSource();
        var cut = Render<TmReportViewer>(parameters => parameters
            .Add(component => component.ReportSource, source));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tm-report-page-count']").TextContent.Should().Contain("1 of 2"));

        cut.Find("button[aria-label='Next page']").Click();
        cut.Find("[data-testid='tm-report-page-input']").GetAttribute("value").Should().Be("2");

        cut.Find("[data-testid='tm-report-zoom-select']").Change("FitWidth");
        cut.Find("[data-testid='tm-report-zoom-select']").GetAttribute("value").Should().Be("FitWidth");
    }

    [Fact]
    public void Viewer_ExportsPdfFromMenu()
    {
        ReportViewerExportResult? exported = null;
        var source = new RecordingReportSource();
        var cut = Render<TmReportViewer>(parameters => parameters
            .Add(component => component.ReportSource, source)
            .Add(component => component.PdfExported, value => exported = value));
        cut.WaitForAssertion(() => source.RenderRequests.Should().HaveCount(1));

        cut.Find("button[aria-label='Export']").Click();
        cut.Find("[data-testid='tm-report-export-menu']").Should().NotBeNull();
        cut.Find("[data-testid='tm-report-export-pdf']").Click();

        source.ExportRequests.Should().HaveCount(1);
        exported.Should().NotBeNull();
        exported!.FileName.Should().Be("test.pdf");
    }

    [Fact]
    public void Viewer_ExportsCsvAndXlsxFromMenu()
    {
        ReportViewerExportResult? csv = null;
        ReportViewerExportResult? xlsx = null;
        var source = new RecordingReportSource();
        var cut = Render<TmReportViewer>(parameters => parameters
            .Add(component => component.ReportSource, source)
            .Add(component => component.CsvExported, value => csv = value)
            .Add(component => component.XlsxExported, value => xlsx = value));
        cut.WaitForAssertion(() => source.RenderRequests.Should().HaveCount(1));

        cut.Find("button[aria-label='Export']").Click();
        cut.Find("[data-testid='tm-report-export-csv']").Click();
        cut.Find("button[aria-label='Export']").Click();
        cut.Find("[data-testid='tm-report-export-xlsx']").Click();

        source.CsvExportRequests.Should().HaveCount(1);
        source.XlsxExportRequests.Should().HaveCount(1);
        csv.Should().NotBeNull();
        csv!.FileName.Should().Be("test.csv");
        xlsx.Should().NotBeNull();
        xlsx!.FileName.Should().Be("test.xlsx");
    }

    [Fact]
    public void Viewer_UsesResolvedRenderParametersInParameterPanel()
    {
        var source = new RecordingReportSource(
            metadata: Metadata(),
            renderedParameters: new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
            {
                ["Region"] = ReportParameterValue.Scalar("EU"),
            });

        var cut = Render<TmReportViewer>(parameters => parameters
            .Add(component => component.ReportSource, source));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tm-report-param-Region']").GetAttribute("value").Should().Be("EU"));
    }

    [Fact]
    public async Task Viewer_ToggleInteractionRefreshesWithStatelessToken()
    {
        var source = new RecordingReportSource();
        var cut = Render<TmReportViewer>(parameters => parameters
            .Add(component => component.ReportSource, source));
        cut.WaitForAssertion(() => source.RenderRequests.Should().HaveCount(1));

        await cut.InvokeAsync(() => cut.Instance.ToggleInteractionAsync("details"));

        source.RenderRequests.Should().HaveCount(2);
        source.RenderRequests[1].InteractionToken.Should().Be("details");
    }

    private static ReportViewerMetadata Metadata()
        => new()
        {
            ReportId = "sales",
            Title = "Sales",
            Parameters =
            [
                new ReportViewerParameterMetadata(new ReportParameterDefinition
                {
                    Name = "Region",
                    Label = "Region",
                    DataType = ReportParameterType.List,
                    AvailableValues = ReportParameterAvailableValues.Static(
                    [
                        new ReportParameterAvailableValue("EU", "Europe"),
                    ]),
                },
                [
                    new ReportViewerParameterOption("EU", "Europe"),
                ]),
            ],
        };
}
