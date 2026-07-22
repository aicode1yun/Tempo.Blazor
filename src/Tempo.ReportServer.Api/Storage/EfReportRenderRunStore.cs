using Microsoft.EntityFrameworkCore;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>Ad-hoc render run history store. Tenant- and actor-scoped; SaveChanges is the unit of work.</summary>
public interface IReportRenderRunStore
{
    /// <summary>Persists a single render run record.</summary>
    Task RecordAsync(RenderRunEntity run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a single actor's render runs within a tenant, newest first. When <paramref name="reportId"/>
    /// is supplied the results are constrained to that report.
    /// </summary>
    Task<IReadOnlyList<RenderRunEntity>> ListAsync(string tenantId, string actorId, string? reportId, int max, CancellationToken cancellationToken = default);
}

/// <summary>EF Core backed <see cref="IReportRenderRunStore"/>.</summary>
public sealed class EfReportRenderRunStore : IReportRenderRunStore
{
    private readonly ReportServerDbContext _dbContext;

    /// <summary>Creates the store over a report server context.</summary>
    public EfReportRenderRunStore(ReportServerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task RecordAsync(RenderRunEntity run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        _dbContext.RenderRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RenderRunEntity>> ListAsync(string tenantId, string actorId, string? reportId, int max, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(max, 1, 200);
        var query = _dbContext.RenderRuns
            .AsNoTracking()
            .Where(run => run.TenantId == tenantId && run.ActorId == actorId);
        if (!string.IsNullOrWhiteSpace(reportId))
        {
            query = query.Where(run => run.ReportId == reportId);
        }

        return await query
            // Order by the identity key: it is monotonic with insertion, so this yields newest-first
            // while remaining provider-agnostic (SQLite cannot ORDER BY a DateTimeOffset column).
            .OrderByDescending(run => run.Id)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
