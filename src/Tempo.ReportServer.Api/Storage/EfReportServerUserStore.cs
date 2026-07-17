using Microsoft.EntityFrameworkCore;
using Tempo.ReportServer.Api.Security;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>
/// EF Core <see cref="IReportServerUserProvisioner"/>: upserts a <see cref="ReportServerUserEntity"/>
/// the first time a subject authenticates and refreshes profile/last-seen on subsequent logins.
/// </summary>
public sealed class EfReportServerUserProvisioner : IReportServerUserProvisioner
{
    private readonly ReportServerDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the provisioner.</summary>
    public EfReportServerUserProvisioner(ReportServerDbContext dbContext)
        : this(dbContext, TimeProvider.System)
    {
    }

    /// <summary>Creates the provisioner with an explicit time source (testability).</summary>
    public EfReportServerUserProvisioner(ReportServerDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<ReportServerUserRecord> UpsertAsync(
        string subject,
        string tenantId,
        string? email,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        var now = _timeProvider.GetUtcNow();

        var entity = await _dbContext.Users
            .FirstOrDefaultAsync(user => user.Subject == subject, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            entity = new ReportServerUserEntity
            {
                Subject = subject,
                TenantId = tenantId,
                Email = email,
                DisplayName = displayName,
                FirstSeenAt = now,
                LastSeenAt = now,
            };
            _dbContext.Users.Add(entity);
        }
        else
        {
            entity.LastSeenAt = now;
            if (!string.IsNullOrWhiteSpace(email))
            {
                entity.Email = email;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                entity.DisplayName = displayName;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToRecord(entity);
    }

    private static ReportServerUserRecord ToRecord(ReportServerUserEntity entity)
        => new()
        {
            Subject = entity.Subject,
            TenantId = entity.TenantId,
            Email = entity.Email,
            DisplayName = entity.DisplayName,
            FirstSeenAt = entity.FirstSeenAt,
            LastSeenAt = entity.LastSeenAt,
        };
}
