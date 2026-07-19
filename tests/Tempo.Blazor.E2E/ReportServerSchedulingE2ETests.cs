using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Full-stack (Fáze 13 PASS B): scheduling → real email delivery. Creates an every-minute email
/// schedule for a seeded report, lets the background <c>ReportSchedulingWorker</c> (fast 5s poll)
/// render + deliver it, then asserts a real message landed in smtp4dev (REST :5050) with an
/// <c>application/pdf</c> attachment AND a <c>ScheduleRuns</c> row with <c>Status=Delivered</c>.
/// </summary>
[TestClass]
[TestCategory("ReportServerFullStack")]
[DoNotParallelize]
public sealed class ReportServerSchedulingE2ETests : ReportServerFullStackE2ETestBase
{
    [TestMethod]
    public async Task Schedule_FiresSoon_DeliversEmailWithPdf_AndRecordsDeliveredRun()
    {
        var tag = UniqueTag();
        var recipient = $"e2e-{tag}@tempo.local";

        var (folderId, _) = await SeedFolderAsync($"Sched {tag}").ConfigureAwait(false);
        var reportName = $"E2E Scheduled Report {tag}";
        var reportId = await SeedReportAsync(folderId, reportName).ConfigureAwait(false);

        using var admin = await CreateBearerApiClientAsync("admin1").ConfigureAwait(false);

        // Create an every-minute email schedule (five-field UTC cron). The worker polls every 5s and
        // fires at the next minute boundary; a bounded 3-minute wait covers the worst case.
        var createResponse = await admin.PostAsJsonAsync("/api/schedules", new UpsertReportScheduleRequestDto
        {
            TenantId = TenantId,
            OwnerUserId = "admin1",
            Name = reportName,
            ReportId = reportId,
            CronExpression = "* * * * *",
            Format = ReportScheduleFormat.Pdf,
            DeliveryKind = ReportScheduleDeliveryKind.Email,
            DeliveryTarget = recipient,
            IsEnabled = true,
        }).ConfigureAwait(false);
        createResponse.EnsureSuccessStatusCode();
        var schedule = await createResponse.Content.ReadFromJsonAsync<ReportScheduleDto>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Schedule creation returned no body.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(schedule.ScheduleId), "Created schedule must have an id.");

        // 1) A real email arrives in smtp4dev with a PDF attachment. The subject carries the report name.
        var messageId = await WaitForEmailAsync(reportName).ConfigureAwait(false);
        Assert.IsTrue(await MessageHasPdfAttachmentAsync(messageId).ConfigureAwait(false),
            "The delivered scheduling email must carry an application/pdf attachment.");

        // 2) A ScheduleRuns row with Status=Delivered is persisted (reads the ScheduleRuns EF table).
        await PollAsync(async () =>
        {
            var runs = await admin.GetFromJsonAsync<List<ReportScheduleRunDto>>(
                $"/api/schedules/{schedule.ScheduleId}/runs?tenantId={TenantId}&max=20").ConfigureAwait(false) ?? [];
            return runs.Any(r => r.Status == ReportScheduleRunStatus.Delivered);
        }, "A ScheduleRuns row with Status=Delivered should be persisted.", timeoutMs: 60_000).ConfigureAwait(false);

        // Best-effort: disable the schedule so it stops firing during the rest of the run.
        await admin.PostAsJsonAsync($"/api/schedules/{schedule.ScheduleId}/enabled", new SetReportScheduleEnabledRequestDto
        {
            TenantId = TenantId,
            IsEnabled = false,
        }).ConfigureAwait(false);
    }
}
