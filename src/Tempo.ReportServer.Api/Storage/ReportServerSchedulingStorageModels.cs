namespace Tempo.ReportServer.Api.Storage;

/// <summary>
/// Persistent report schedule definition. Owned by a tenant but deliberately not covered by the
/// ambient tenant query filter: the scheduling worker sweeps every tenant's due schedules on a
/// single background pass, so it queries across tenants and the tenant-scoped stores/endpoints
/// constrain by <see cref="TenantId"/> explicitly.
/// </summary>
public sealed class ReportScheduleEntity
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Schedule identifier (unique within a tenant).</summary>
    public string ScheduleId { get; set; } = string.Empty;

    /// <summary>User that owns the schedule.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Human-readable schedule name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Report identifier to render.</summary>
    public string ReportId { get; set; } = string.Empty;

    /// <summary>Five-field UTC cron expression.</summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>Output format token (Pdf, Csv, Xlsx).</summary>
    public string Format { get; set; } = "Pdf";

    /// <summary>Culture used for rendering and export formatting.</summary>
    public string CultureName { get; set; } = "en-US";

    /// <summary>Serialized report parameter values.</summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>Delivery channel token (Email, Storage, Webhook).</summary>
    public string DeliveryKind { get; set; } = "Email";

    /// <summary>
    /// Delivery target: for <c>Email</c> a comma-separated recipient list, for <c>Storage</c> a
    /// directory-relative folder, for <c>Webhook</c> an absolute URL.
    /// </summary>
    public string DeliveryTarget { get; set; } = string.Empty;

    /// <summary>Missed-run policy token (Skip, CatchUp).</summary>
    public string MissedRunPolicy { get; set; } = "Skip";

    /// <summary>Whether the schedule is active.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Next cron occurrence to fire, in UTC.</summary>
    public DateTimeOffset NextRunUtc { get; set; }

    /// <summary>Last run attempt timestamp.</summary>
    public DateTimeOffset? LastRunUtc { get; set; }

    /// <summary>Last successful delivery timestamp.</summary>
    public DateTimeOffset? LastDeliveredUtc { get; set; }

    /// <summary>Earliest retry timestamp after a failed delivery.</summary>
    public DateTimeOffset? RetryAfterUtc { get; set; }

    /// <summary>Consecutive delivery failure count.</summary>
    public int FailureCount { get; set; }

    /// <summary>Maximum delivery attempts before a run is abandoned.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Latest run status token.</summary>
    public string LastStatus { get; set; } = "NeverRun";

    /// <summary>Latest status message.</summary>
    public string LastStatusMessage { get; set; } = "Never run";

    /// <summary>
    /// Serialized list of occurrence timestamps that a failed run must retry. Empty/null when no
    /// retry is pending. Persisted so a retry re-runs the exact missed occurrence(s), not a fresh
    /// cron slot.
    /// </summary>
    public string? PendingOccurrencesJson { get; set; }

    /// <summary>Optimistic-concurrency token.</summary>
    public byte[]? RowVersion { get; set; }
}

/// <summary>Immutable audit record of a single scheduled report run attempt.</summary>
public sealed class ReportScheduleRunEntity
{
    /// <summary>Run identifier.</summary>
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Schedule identifier.</summary>
    public string ScheduleId { get; set; } = string.Empty;

    /// <summary>The logical cron occurrence this run satisfied.</summary>
    public DateTimeOffset OccurrenceUtc { get; set; }

    /// <summary>When processing started.</summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>When processing completed (success or failure).</summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>Run status token (Delivered, Retrying, Failed).</summary>
    public string Status { get; set; } = "Queued";

    /// <summary>Delivery attempt number (1-based).</summary>
    public int Attempt { get; set; } = 1;

    /// <summary>Delivery channel token used for this run.</summary>
    public string DeliveryKind { get; set; } = "Email";

    /// <summary>Delivery target used for this run.</summary>
    public string DeliveryTarget { get; set; } = string.Empty;

    /// <summary>Rendered artifact file name.</summary>
    public string? ArtifactFileName { get; set; }

    /// <summary>Rendered artifact content type.</summary>
    public string? ArtifactContentType { get; set; }

    /// <summary>Rendered artifact size in bytes.</summary>
    public int ArtifactByteCount { get; set; }

    /// <summary>Failure detail, when the run failed.</summary>
    public string? ErrorMessage { get; set; }
}
