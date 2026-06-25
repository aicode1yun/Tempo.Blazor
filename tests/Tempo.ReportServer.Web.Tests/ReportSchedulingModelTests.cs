using Tempo.Reporting.Abstractions.Data;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Tests;

public sealed class ReportSchedulingModelTests
{
    [Fact]
    public void CronSchedule_FindsNextDailyOccurrence()
    {
        var cron = ReportCronSchedule.Parse("0 8 * * *");
        var beforeRun = new DateTimeOffset(2026, 6, 22, 7, 59, 0, TimeSpan.Zero);
        var atRun = new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero);

        cron.GetNextOccurrence(beforeRun).Should().Be(atRun);
        cron.GetNextOccurrence(atRun).Should().Be(new DateTimeOffset(2026, 6, 23, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ScheduleStore_SeedsSubscriptionsAndPersistsScheduleParameters()
    {
        var clock = new ManualReportScheduleClock(new DateTimeOffset(2026, 6, 22, 6, 0, 0, TimeSpan.Zero));
        var store = new ReportScheduleStore(clock);

        var seeded = store.GetSchedule("northwind", "sales-dashboard-digest");
        seeded.Should().NotBeNull();
        seeded!.NextRunUtc.Should().BeAfter(clock.UtcNow);
        store.ListSubscriptions("northwind", "pavel.author")
            .Should()
            .Contain(subscription => subscription.ScheduleId == "sales-dashboard-digest" && subscription.IsEnabled);

        store.UpsertSchedule(new ReportScheduleDefinition
        {
            Id = "unit-weekly",
            TenantId = "northwind",
            OwnerUserId = "pavel.author",
            Name = "Weekly finance pack",
            ReportId = "sales-register",
            CronExpression = "15 7 * * 1",
            NextRunUtc = new DateTimeOffset(2026, 6, 29, 7, 15, 0, TimeSpan.Zero),
            Parameters = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
            {
                ["Region"] = ReportParameterValue.Scalar("EU"),
            },
            Recipients = [new ReportScheduleRecipient("finance@example.test", "Finance Ops")],
        });

        var schedule = store.GetSchedule("northwind", "unit-weekly");
        schedule.Should().NotBeNull();
        schedule!.Parameters["Region"].ScalarValue.Should().Be("EU");
        schedule.Recipients.Should().ContainSingle().Which.Email.Should().Be("finance@example.test");
    }
}
