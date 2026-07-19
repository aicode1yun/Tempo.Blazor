using Microsoft.Extensions.DependencyInjection;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.ReportServer.Web.Pages;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class RunHistoryPageTests : ReportServerWebTestBase
{
    [Fact]
    public void RunHistoryPage_ShowsEmptyState_WhenNoRuns()
    {
        SignIn();
        var cut = RenderComponent<RunHistoryPage>();

        cut.Find("[data-testid='run-history-empty']").Should().NotBeNull();
        cut.FindAll("[data-testid='run-history-row']").Should().BeEmpty();
    }

    [Fact]
    public void RunHistoryPage_RendersRowsFromClient()
    {
        SignIn();
        var fake = (FakeTempoReportServerClient)Services.GetRequiredService<ITempoReportServerClient>();
        fake.SeedRenderRun("sales-register", format: "Pdf", outcome: "Succeeded");
        fake.SeedRenderRun("fulfillment-sla", format: "Csv", outcome: "Succeeded");

        var cut = RenderComponent<RunHistoryPage>();

        cut.FindAll("[data-testid='run-history-row']").Should().HaveCount(2);
        var table = cut.Find("[data-testid='run-history-table']").TextContent;
        table.Should().Contain("Sales Register");
        table.Should().Contain("Pdf");
    }
}
