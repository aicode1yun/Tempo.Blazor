using Tempo.Reporting.Abstractions.Data;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Tests;

public sealed class ReportScheduleWorkerTests
{
    [Fact]
    public async Task TriggerDueSchedules_EnqueuesRenderJobAndProcessesDelivery()
    {
        var clock = new ManualReportScheduleClock(new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero));
        var store = CreateStore(clock);
        var queue = new ReportRenderJobQueue(clock);
        var delivery = new CapturingScheduledDeliveryService();
        var worker = new ReportScheduleWorker(store, queue, delivery, clock);

        var triggered = await worker.TriggerDueSchedulesAsync();
        var processed = await worker.ProcessQueuedJobsAsync();

        triggered.Should().Be(1);
        processed.Should().Be(1);
        delivery.DeliveredJobs.Should().ContainSingle(job => job.ScheduleId == "due-digest");
        queue.PendingCount.Should().Be(0);
        var schedule = store.GetSchedule("northwind", "due-digest");
        schedule!.LastStatus.Should().Be(ReportScheduleRunStatus.Delivered);
        schedule.LastDeliveredUtc.Should().Be(clock.UtcNow);
        schedule.NextRunUtc.Should().Be(new DateTimeOffset(2026, 6, 23, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ProcessQueuedJobs_RetriesFailedDeliveryWithBackoff()
    {
        var clock = new ManualReportScheduleClock(new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero));
        var store = CreateStore(clock);
        var queue = new ReportRenderJobQueue(clock);
        var delivery = new CapturingScheduledDeliveryService { FailNextDelivery = true };
        var worker = new ReportScheduleWorker(store, queue, delivery, clock);

        await worker.TriggerDueSchedulesAsync();
        await worker.ProcessQueuedJobsAsync();

        var failed = store.GetSchedule("northwind", "due-digest");
        failed!.LastStatus.Should().Be(ReportScheduleRunStatus.Retrying);
        failed.FailureCount.Should().Be(1);
        failed.RetryAfterUtc.Should().Be(clock.UtcNow.AddMinutes(2));

        clock.Advance(TimeSpan.FromMinutes(2));
        await worker.TriggerDueSchedulesAsync();
        await worker.ProcessQueuedJobsAsync();

        var delivered = store.GetSchedule("northwind", "due-digest");
        delivered!.LastStatus.Should().Be(ReportScheduleRunStatus.Delivered);
        delivered.FailureCount.Should().Be(0);
        queue.History.Count(job => job.ScheduleId == "due-digest").Should().Be(2);
    }

    private static ReportScheduleStore CreateStore(IReportScheduleClock clock)
    {
        var store = new ReportScheduleStore(clock, seedDemoData: false);
        store.UpsertSchedule(new ReportScheduleDefinition
        {
            Id = "due-digest",
            TenantId = "northwind",
            OwnerUserId = "pavel.author",
            Name = "Due dashboard digest",
            ReportId = "sales-dashboard",
            CronExpression = "0 8 * * *",
            NextRunUtc = new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero),
            Parameters = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
            {
                ["Region"] = ReportParameterValue.Scalar("EU"),
            },
            Recipients = [new ReportScheduleRecipient("ops@example.test", "Ops")],
        });
        return store;
    }

    private sealed class CapturingScheduledDeliveryService : IReportScheduledDeliveryService
    {
        public bool FailNextDelivery { get; set; }

        public List<ScheduledReportJob> DeliveredJobs { get; } = [];

        public Task<DeliveredReportEmail> DeliverAsync(ScheduledReportJob job, CancellationToken cancellationToken = default)
        {
            if (FailNextDelivery)
            {
                FailNextDelivery = false;
                throw new InvalidOperationException("smtp4dev unavailable");
            }

            DeliveredJobs.Add(job);
            return Task.FromResult(new DeliveredReportEmail(
                job.JobId,
                job.ScheduleId,
                job.TenantId,
                new EmailMessageSnapshot(["ops@example.test"], "Report", "smtp4dev"),
                [new ReportEmailAttachment("sales-dashboard.pdf", "application/pdf", [0x25, 0x50, 0x44, 0x46])],
                DateTimeOffset.UtcNow));
        }
    }
}
