using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Api.Scheduling;

/// <summary>A run record to persist as part of applying a schedule outcome.</summary>
public sealed record ScheduleRunRecord(
    DateTimeOffset OccurrenceUtc,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    ReportScheduleRunStatus Status,
    int Attempt,
    ReportScheduleDeliveryKind DeliveryKind,
    string DeliveryTarget,
    string? ArtifactFileName,
    string? ArtifactContentType,
    int ArtifactByteCount,
    string? ErrorMessage);

/// <summary>The final schedule-row state after a worker pass, applied atomically with the run records.</summary>
public sealed record ScheduleStateUpdate(
    DateTimeOffset? LastRunUtc,
    DateTimeOffset? LastDeliveredUtc,
    DateTimeOffset NextRunUtc,
    DateTimeOffset? RetryAfterUtc,
    int FailureCount,
    ReportScheduleRunStatus LastStatus,
    string LastStatusMessage,
    IReadOnlyList<DateTimeOffset> PendingOccurrences);

/// <summary>
/// Persistent report schedule store. Tenant-scoped read/write methods constrain by tenant explicitly;
/// <see cref="GetDueSchedulesAsync"/> and <see cref="ApplyRunOutcomeAsync"/> support the cross-tenant
/// background worker.
/// </summary>
public interface IReportScheduleStore
{
    /// <summary>Lists schedules for a tenant, ordered by next run.</summary>
    Task<IReadOnlyList<ReportScheduleDto>> ListAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>Gets a single schedule.</summary>
    Task<ReportScheduleDto?> GetAsync(string tenantId, string scheduleId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a schedule and seeds its next run when needed.</summary>
    Task<ReportScheduleDto> UpsertAsync(UpsertReportScheduleRequestDto request, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Enables or disables a schedule.</summary>
    Task<bool> SetEnabledAsync(string tenantId, string scheduleId, bool isEnabled, CancellationToken cancellationToken = default);

    /// <summary>Deletes a schedule.</summary>
    Task<bool> DeleteAsync(string tenantId, string scheduleId, CancellationToken cancellationToken = default);

    /// <summary>Gets the most recent run records for a schedule, newest first.</summary>
    Task<IReadOnlyList<ReportScheduleRunDto>> GetRunsAsync(string tenantId, string scheduleId, int max, CancellationToken cancellationToken = default);

    /// <summary>Gets all enabled schedules that are due (next run or retry at or before now), across tenants.</summary>
    Task<IReadOnlyList<ReportScheduleDto>> GetDueSchedulesAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Atomically updates the schedule row and appends the run records in one transaction.</summary>
    Task ApplyRunOutcomeAsync(
        string tenantId,
        string scheduleId,
        ScheduleStateUpdate update,
        IReadOnlyList<ScheduleRunRecord> runs,
        CancellationToken cancellationToken = default);
}
