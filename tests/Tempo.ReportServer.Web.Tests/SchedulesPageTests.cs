using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Web.Pages;
using Tempo.ReportServer.Web.Services;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class SchedulesPageTests : ReportServerWebTestBase
{
    [Fact]
    public void SchedulesPage_CreatesScheduleTogglesItAndRunsEmailDelivery()
    {
        SignIn();
        var cut = Render<SchedulesPage>();

        cut.Find("[data-testid='f16-schedules-page']").TextContent.Should().Contain("Schedules");
        cut.Find("[data-testid='schedule-name']").Input("Monday ops pack");
        cut.Find("[data-testid='schedule-cron']").Input("30 6 * * 1");
        cut.Find("[data-testid='schedule-email']").Input("ops@example.test");
        cut.Find("[data-testid='schedule-save']").Click();

        cut.Find("[data-testid='schedules-table']").TextContent.Should().Contain("Monday ops pack");
        cut.Find("[data-testid='schedules-table']").TextContent.Should().Contain("ops@example.test");

        cut.Find("[data-testid='toggle-schedule-sales-dashboard-digest']").Click();
        Services.GetRequiredService<ReportScheduleStore>()
            .GetSchedule("northwind", "sales-dashboard-digest")!
            .IsEnabled
            .Should()
            .BeFalse();

        cut.Find("[data-testid='toggle-schedule-sales-dashboard-digest']").Click();
        Services.GetRequiredService<ReportScheduleStore>()
            .GetSchedule("northwind", "sales-dashboard-digest")!
            .IsEnabled
            .Should()
            .BeTrue();
        cut.Find("[data-testid='run-schedule-sales-dashboard-digest']").Click();

        cut.WaitForAssertion(
            () =>
            {
                Services.GetRequiredService<ReportScheduleStore>()
                    .GetSchedule("northwind", "sales-dashboard-digest")!
                    .LastStatus
                    .Should()
                    .Be(ReportScheduleRunStatus.Delivered);
                cut.Find("[data-testid='schedule-outbox']").TextContent.Should().Contain("sales-dashboard.pdf");
            },
            TimeSpan.FromSeconds(10));
        cut.Find("[data-testid='schedule-outbox']").TextContent.Should().Contain("smtp4dev");
    }
}
