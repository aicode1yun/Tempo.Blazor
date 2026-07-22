using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tempo.ReportServer.Api.Security;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>EF Core implementation of <see cref="IReportAuditLog"/>.</summary>
public sealed class EfReportAuditLog : IReportAuditLog
{
    private static readonly JsonSerializerOptions DetailsJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ReportServerDbContext _dbContext;

    /// <summary>Creates an EF audit log.</summary>
    public EfReportAuditLog(ReportServerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task WriteAsync(ReportAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        _dbContext.AuditEvents.Add(ToEntity(auditEvent));
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportAuditEvent>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.AuditEvents
            .AsNoTracking()
            .Where(auditEvent => auditEvent.TenantId == tenantId)
            .OrderBy(auditEvent => auditEvent.Timestamp)
            .ThenBy(auditEvent => auditEvent.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Array.ConvertAll(entities, ToEvent);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportAuditEvent>> QueryAsync(
        ReportAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var events = _dbContext.AuditEvents
            .AsNoTracking()
            .Where(auditEvent => auditEvent.TenantId == query.TenantId);

        if (query.Action is { } action)
        {
            var actionValue = (int)action;
            events = events.Where(auditEvent => auditEvent.Action == actionValue);
        }

        if (query.Outcome is { } outcome)
        {
            var outcomeValue = (int)outcome;
            events = events.Where(auditEvent => auditEvent.Outcome == outcomeValue);
        }

        if (!string.IsNullOrWhiteSpace(query.ActorId))
        {
            events = events.Where(auditEvent => auditEvent.ActorId == query.ActorId);
        }

        if (!string.IsNullOrWhiteSpace(query.ResourceId))
        {
            events = events.Where(auditEvent => auditEvent.ResourceId == query.ResourceId);
        }

        if (query.From is { } from)
        {
            events = events.Where(auditEvent => auditEvent.Timestamp >= from);
        }

        if (query.To is { } to)
        {
            events = events.Where(auditEvent => auditEvent.Timestamp <= to);
        }

        events = events
            .OrderByDescending(auditEvent => auditEvent.Timestamp)
            .ThenByDescending(auditEvent => auditEvent.Id);

        if (query.Take is { } take && take >= 0)
        {
            events = events.Take(take);
        }

        var entities = await events.ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return Array.ConvertAll(entities, ToEvent);
    }

    private static ReportAuditEventEntity ToEntity(ReportAuditEvent auditEvent)
        => new()
        {
            TenantId = auditEvent.TenantId,
            ActorId = auditEvent.ActorId,
            Action = (int)auditEvent.Action,
            ResourceKind = (int)auditEvent.ResourceKind,
            ResourceId = auditEvent.ResourceId,
            Outcome = (int)auditEvent.Outcome,
            Timestamp = auditEvent.Timestamp,
            DetailsJson = JsonSerializer.Serialize(
                auditEvent.Details ?? new Dictionary<string, string>(StringComparer.Ordinal),
                DetailsJsonOptions),
        };

    private static ReportAuditEvent ToEvent(ReportAuditEventEntity entity)
        => new()
        {
            TenantId = entity.TenantId,
            ActorId = entity.ActorId,
            Action = (ReportAuditAction)entity.Action,
            ResourceKind = (ReportResourceKind)entity.ResourceKind,
            ResourceId = entity.ResourceId,
            Outcome = (ReportAuditOutcome)entity.Outcome,
            Timestamp = entity.Timestamp,
            Details = DeserializeDetails(entity.DetailsJson),
        };

    private static IReadOnlyDictionary<string, string> DeserializeDetails(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(detailsJson, DetailsJsonOptions)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
