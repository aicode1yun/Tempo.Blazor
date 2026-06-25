using Tempo.ReportServer.Api.Security;

namespace Tempo.ReportServer.Api.Tests.Security;

public sealed class ReportAuditLogTests
{
    [Fact]
    public async Task AuditLog_RecordsWhoWhenWhatForCoreReportServerActions()
    {
        var audit = new InMemoryReportAuditLog();
        var now = DateTimeOffset.Parse("2026-06-22T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        await audit.WriteAsync(ReportAuditEvent.Allowed("tenant-a", "author-1", ReportAuditAction.RenderReport, ReportResourceKind.Render, "orders", now));
        await audit.WriteAsync(ReportAuditEvent.Allowed("tenant-a", "author-1", ReportAuditAction.ExportReport, ReportResourceKind.Export, "orders", now.AddMinutes(1)));
        await audit.WriteAsync(ReportAuditEvent.Allowed("tenant-a", "author-1", ReportAuditAction.ChangeDefinition, ReportResourceKind.ReportDefinition, "orders", now.AddMinutes(2)));
        await audit.WriteAsync(ReportAuditEvent.Allowed("tenant-a", "admin", ReportAuditAction.ChangeAcl, ReportResourceKind.Acl, "finance", now.AddMinutes(3)));

        var events = await audit.ListAsync("tenant-a");

        events.Select(e => e.Action).Should().Equal(
            ReportAuditAction.RenderReport,
            ReportAuditAction.ExportReport,
            ReportAuditAction.ChangeDefinition,
            ReportAuditAction.ChangeAcl);
        events.Should().OnlyContain(e => e.TenantId == "tenant-a");
        events.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.ActorId));
        events.Should().OnlyContain(e => e.Timestamp >= now);
    }
}
