#pragma warning disable MA0016, MA0048

namespace Tempo.Reporting.Abstractions.Dtos;

/// <summary>Output format produced by a scheduled report run.</summary>
public enum ReportScheduleFormat
{
    /// <summary>Portable Document Format.</summary>
    Pdf,

    /// <summary>Comma-separated values.</summary>
    Csv,

    /// <summary>OpenXML workbook.</summary>
    Xlsx,
}

/// <summary>Delivery channel used to hand off a rendered scheduled report.</summary>
public enum ReportScheduleDeliveryKind
{
    /// <summary>Send the report as an email attachment.</summary>
    Email,

    /// <summary>Write the report to a configured storage location.</summary>
    Storage,

    /// <summary>POST the report to a webhook URL.</summary>
    Webhook,
}

/// <summary>Behaviour when a schedule has missed one or more occurrences.</summary>
public enum ReportScheduleMissedRunPolicy
{
    /// <summary>Fire a single run for the most recent missed occurrence.</summary>
    Skip,

    /// <summary>Fire one run for every missed occurrence.</summary>
    CatchUp,
}

/// <summary>Latest observed schedule state.</summary>
public enum ReportScheduleRunStatus
{
    /// <summary>The schedule has not run yet.</summary>
    NeverRun,

    /// <summary>A run is queued.</summary>
    Queued,

    /// <summary>The last run was delivered successfully.</summary>
    Delivered,

    /// <summary>The last run failed and is awaiting retry.</summary>
    Retrying,

    /// <summary>The last run failed without further retries.</summary>
    Failed,
}

/// <summary>Persisted report schedule projected for API consumers.</summary>
public sealed record ReportScheduleDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Schedule identifier.</summary>
    public string ScheduleId { get; init; } = string.Empty;

    /// <summary>Owner user identifier.</summary>
    public string OwnerUserId { get; init; } = string.Empty;

    /// <summary>Schedule name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Report identifier to render.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Five-field UTC cron expression.</summary>
    public string CronExpression { get; init; } = string.Empty;

    /// <summary>Output format.</summary>
    public ReportScheduleFormat Format { get; init; } = ReportScheduleFormat.Pdf;

    /// <summary>Render culture.</summary>
    public string CultureName { get; init; } = "en-US";

    /// <summary>Report parameter values keyed by parameter name (string values).</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Delivery channel.</summary>
    public ReportScheduleDeliveryKind DeliveryKind { get; init; } = ReportScheduleDeliveryKind.Email;

    /// <summary>Delivery target (recipients, storage folder, or webhook URL).</summary>
    public string DeliveryTarget { get; init; } = string.Empty;

    /// <summary>Missed-run policy.</summary>
    public ReportScheduleMissedRunPolicy MissedRunPolicy { get; init; } = ReportScheduleMissedRunPolicy.Skip;

    /// <summary>Maximum delivery attempts before a run is abandoned.</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Whether the schedule is active.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Next cron occurrence in UTC.</summary>
    public DateTimeOffset NextRunUtc { get; init; }

    /// <summary>Last run attempt timestamp.</summary>
    public DateTimeOffset? LastRunUtc { get; init; }

    /// <summary>Last successful delivery timestamp.</summary>
    public DateTimeOffset? LastDeliveredUtc { get; init; }

    /// <summary>Earliest retry timestamp after a failure.</summary>
    public DateTimeOffset? RetryAfterUtc { get; init; }

    /// <summary>Consecutive failure count.</summary>
    public int FailureCount { get; init; }

    /// <summary>Latest status.</summary>
    public ReportScheduleRunStatus LastStatus { get; init; } = ReportScheduleRunStatus.NeverRun;

    /// <summary>Latest status message.</summary>
    public string LastStatusMessage { get; init; } = "Never run";

    /// <summary>Occurrence timestamps a failed run must retry; empty when no retry is pending.</summary>
    public IReadOnlyList<DateTimeOffset> PendingOccurrencesUtc { get; init; } = [];
}

/// <summary>Create-or-update request for a report schedule.</summary>
public sealed record UpsertReportScheduleRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Schedule identifier. When empty a slug is derived from the name.</summary>
    public string ScheduleId { get; init; } = string.Empty;

    /// <summary>Owner user identifier.</summary>
    public string OwnerUserId { get; init; } = string.Empty;

    /// <summary>Schedule name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Report identifier to render.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Five-field UTC cron expression.</summary>
    public string CronExpression { get; init; } = "0 8 * * 1";

    /// <summary>Output format.</summary>
    public ReportScheduleFormat Format { get; init; } = ReportScheduleFormat.Pdf;

    /// <summary>Render culture.</summary>
    public string CultureName { get; init; } = "en-US";

    /// <summary>Report parameter values keyed by parameter name.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Delivery channel.</summary>
    public ReportScheduleDeliveryKind DeliveryKind { get; init; } = ReportScheduleDeliveryKind.Email;

    /// <summary>Delivery target (recipients, storage folder, or webhook URL).</summary>
    public string DeliveryTarget { get; init; } = string.Empty;

    /// <summary>Missed-run policy.</summary>
    public ReportScheduleMissedRunPolicy MissedRunPolicy { get; init; } = ReportScheduleMissedRunPolicy.Skip;

    /// <summary>Maximum delivery attempts before a run is abandoned.</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Whether the schedule is active.</summary>
    public bool IsEnabled { get; init; } = true;
}

/// <summary>Toggle-enabled request for a schedule.</summary>
public sealed record SetReportScheduleEnabledRequestDto
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Desired enabled state.</summary>
    public bool IsEnabled { get; init; }
}

/// <summary>Audit record of a single scheduled report run attempt.</summary>
public sealed record ReportScheduleRunDto
{
    /// <summary>Run identifier.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Schedule identifier.</summary>
    public string ScheduleId { get; init; } = string.Empty;

    /// <summary>The logical cron occurrence this run satisfied.</summary>
    public DateTimeOffset OccurrenceUtc { get; init; }

    /// <summary>When processing started.</summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>When processing completed.</summary>
    public DateTimeOffset? CompletedUtc { get; init; }

    /// <summary>Run status.</summary>
    public ReportScheduleRunStatus Status { get; init; } = ReportScheduleRunStatus.Queued;

    /// <summary>Delivery attempt number.</summary>
    public int Attempt { get; init; } = 1;

    /// <summary>Delivery channel token used for this run.</summary>
    public ReportScheduleDeliveryKind DeliveryKind { get; init; } = ReportScheduleDeliveryKind.Email;

    /// <summary>Delivery target used for this run.</summary>
    public string DeliveryTarget { get; init; } = string.Empty;

    /// <summary>Rendered artifact file name.</summary>
    public string? ArtifactFileName { get; init; }

    /// <summary>Rendered artifact content type.</summary>
    public string? ArtifactContentType { get; init; }

    /// <summary>Rendered artifact size in bytes.</summary>
    public int ArtifactByteCount { get; init; }

    /// <summary>Failure detail when the run failed.</summary>
    public string? ErrorMessage { get; init; }
}
