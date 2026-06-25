#pragma warning disable MA0048, MA0158

namespace Tempo.ReportServer.Api.Security;

/// <summary>Audited report server action.</summary>
public enum ReportAuditAction
{
    /// <summary>Report rendering.</summary>
    RenderReport,

    /// <summary>Report export.</summary>
    ExportReport,

    /// <summary>Report definition or revision change.</summary>
    ChangeDefinition,

    /// <summary>Data source change.</summary>
    ChangeDataSource,

    /// <summary>ACL change.</summary>
    ChangeAcl,
}

/// <summary>Audit outcome.</summary>
public enum ReportAuditOutcome
{
    /// <summary>Operation was allowed.</summary>
    Allowed,

    /// <summary>Operation was denied.</summary>
    Denied,
}

/// <summary>Report server audit event.</summary>
public sealed record ReportAuditEvent
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>User id or API actor id.</summary>
    public string ActorId { get; init; } = string.Empty;

    /// <summary>Audited action.</summary>
    public ReportAuditAction Action { get; init; }

    /// <summary>Resource kind.</summary>
    public ReportResourceKind ResourceKind { get; init; }

    /// <summary>Resource identifier.</summary>
    public string ResourceId { get; init; } = string.Empty;

    /// <summary>Operation outcome.</summary>
    public ReportAuditOutcome Outcome { get; init; }

    /// <summary>Event timestamp.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Optional details.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Creates an allowed audit event.</summary>
    public static ReportAuditEvent Allowed(
        string tenantId,
        string actorId,
        ReportAuditAction action,
        ReportResourceKind resourceKind,
        string resourceId,
        DateTimeOffset? timestamp = null)
        => Create(tenantId, actorId, action, resourceKind, resourceId, ReportAuditOutcome.Allowed, timestamp);

    /// <summary>Creates a denied audit event.</summary>
    public static ReportAuditEvent Denied(
        string tenantId,
        string actorId,
        ReportAuditAction action,
        ReportResourceKind resourceKind,
        string resourceId,
        DateTimeOffset? timestamp = null)
        => Create(tenantId, actorId, action, resourceKind, resourceId, ReportAuditOutcome.Denied, timestamp);

    private static ReportAuditEvent Create(
        string tenantId,
        string actorId,
        ReportAuditAction action,
        ReportResourceKind resourceKind,
        string resourceId,
        ReportAuditOutcome outcome,
        DateTimeOffset? timestamp)
        => new()
        {
            TenantId = tenantId,
            ActorId = actorId,
            Action = action,
            ResourceKind = resourceKind,
            ResourceId = resourceId,
            Outcome = outcome,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        };
}

/// <summary>Report server audit log.</summary>
public interface IReportAuditLog
{
    /// <summary>Writes an event.</summary>
    Task WriteAsync(ReportAuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>Lists events for a tenant.</summary>
    Task<IReadOnlyList<ReportAuditEvent>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
}

/// <summary>In-memory audit log.</summary>
public sealed class InMemoryReportAuditLog : IReportAuditLog
{
    private readonly object _gate = new();
    private readonly List<ReportAuditEvent> _events = [];

    /// <inheritdoc />
    public Task WriteAsync(ReportAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _events.Add(auditEvent);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportAuditEvent>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult((IReadOnlyList<ReportAuditEvent>)_events
                .Where(auditEvent => string.Equals(auditEvent.TenantId, tenantId, StringComparison.Ordinal))
                .OrderBy(auditEvent => auditEvent.Timestamp)
                .ToArray());
        }
    }
}

#pragma warning restore MA0048, MA0158
