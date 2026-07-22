using Microsoft.EntityFrameworkCore;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>Per-user report favorite store. Tenant- and user-scoped; SaveChanges is the unit of work.</summary>
public interface IReportFavoriteStore
{
    /// <summary>Lists the favorites owned by a single user within a tenant, newest first.</summary>
    Task<IReadOnlyList<ReportFavoriteEntity>> ListAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a favorite for a user. Idempotent: if the (tenant, user, report) favorite already exists it
    /// is returned unchanged and no duplicate row is created.
    /// </summary>
    Task<ReportFavoriteEntity> AddAsync(string tenantId, string userId, string reportId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user's favorite. Returns <c>true</c> when a row was removed.</summary>
    Task<bool> RemoveAsync(string tenantId, string userId, string reportId, CancellationToken cancellationToken = default);
}

/// <summary>EF Core backed <see cref="IReportFavoriteStore"/>.</summary>
public sealed class EfReportFavoriteStore : IReportFavoriteStore
{
    private readonly ReportServerDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the store over a report server context.</summary>
    public EfReportFavoriteStore(ReportServerDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportFavoriteEntity>> ListAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.TenantId == tenantId && favorite.UserId == userId)
            // Order by the identity key: it is monotonic with insertion, so this yields newest-first
            // while remaining provider-agnostic (SQLite cannot ORDER BY a DateTimeOffset column).
            .OrderByDescending(favorite => favorite.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ReportFavoriteEntity> AddAsync(string tenantId, string userId, string reportId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Favorites
            .FirstOrDefaultAsync(
                favorite => favorite.TenantId == tenantId && favorite.UserId == userId && favorite.ReportId == reportId,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var favorite = new ReportFavoriteEntity
        {
            TenantId = tenantId,
            UserId = userId,
            ReportId = reportId,
            CreatedAt = _timeProvider.GetUtcNow(),
        };
        _dbContext.Favorites.Add(favorite);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return favorite;
        }
        catch (DbUpdateException)
        {
            // A concurrent add of the same (tenant, user, report) lost the check-then-act race and hit
            // the unique index. Treat it as idempotent success: detach the failed insert and return the
            // row the winner persisted.
            _dbContext.Entry(favorite).State = EntityState.Detached;
            var winner = await _dbContext.Favorites
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    candidate => candidate.TenantId == tenantId && candidate.UserId == userId && candidate.ReportId == reportId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (winner is null)
            {
                // The failure was not the expected unique-index collision; surface it.
                throw;
            }

            return winner;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string tenantId, string userId, string reportId, CancellationToken cancellationToken = default)
    {
        var favorite = await _dbContext.Favorites
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.UserId == userId && candidate.ReportId == reportId,
                cancellationToken)
            .ConfigureAwait(false);
        if (favorite is null)
        {
            return false;
        }

        _dbContext.Favorites.Remove(favorite);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
