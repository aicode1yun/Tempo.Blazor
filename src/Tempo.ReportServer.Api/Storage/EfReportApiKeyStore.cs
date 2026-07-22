using Microsoft.EntityFrameworkCore;
using Tempo.ReportServer.Api.Security;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>
/// EF Core implementation of <see cref="IReportApiKeyStore"/>. Secret material is stored only as a
/// SHA-256 hash; validation resolves a key through an indexed hash lookup and enforces revocation
/// and expiration. Rotation is atomic (revoke + reissue in a single transaction).
/// </summary>
public sealed class EfReportApiKeyStore : IReportApiKeyStore
{
    private readonly ReportServerDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates an EF API key store.</summary>
    public EfReportApiKeyStore(ReportServerDbContext dbContext)
        : this(dbContext, TimeProvider.System)
    {
    }

    /// <summary>Creates an EF API key store with an explicit time source (testability).</summary>
    public EfReportApiKeyStore(ReportServerDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public Task<ReportApiKeyCreationResult> CreateAsync(
        string tenantId,
        string applicationId,
        ReportPermission permissions,
        CancellationToken cancellationToken = default)
        => CreateAsync(tenantId, applicationId, permissions, expiresAt: null, cancellationToken);

    /// <inheritdoc />
    public async Task<ReportApiKeyCreationResult> CreateAsync(
        string tenantId,
        string applicationId,
        ReportPermission permissions,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
    {
        var (entity, plainTextKey) = NewKey(tenantId, applicationId, permissions, expiresAt);
        _dbContext.ApiKeys.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ReportApiKeyCreationResult(entity.KeyId, plainTextKey, ToDescriptor(entity));
    }

    /// <inheritdoc />
    public async Task<ReportApiKeyDescriptor?> ValidateAsync(
        string plainTextKey,
        CancellationToken cancellationToken = default)
    {
        if (!ReportApiKeyMaterial.HasKeyPrefix(plainTextKey))
        {
            return null;
        }

        var hash = ReportApiKeyMaterial.ComputeHash(plainTextKey);
        var entity = await _dbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(key => key.KeyHash == hash, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        var descriptor = ToDescriptor(entity);
        return descriptor.IsActive(_timeProvider.GetUtcNow()) ? descriptor : null;
    }

    /// <inheritdoc />
    public async Task<ReportApiKeyDescriptor?> GetAsync(
        string keyId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(key => key.KeyId == keyId && key.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDescriptor(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportApiKeyDescriptor>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.ApiKeys
            .AsNoTracking()
            .Where(key => key.TenantId == tenantId)
            .OrderByDescending(key => key.CreatedAt)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Array.ConvertAll(entities, ToDescriptor);
    }

    /// <inheritdoc />
    public async Task RevokeAsync(
        string keyId,
        string tenantId,
        string revokedByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ApiKeys
            .FirstOrDefaultAsync(key => key.KeyId == keyId && key.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || entity.RevokedAt is not null)
        {
            return;
        }

        entity.RevokedAt = _timeProvider.GetUtcNow();
        entity.RevokedByUserId = revokedByUserId;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ReportApiKeyCreationResult?> RotateAsync(
        string keyId,
        string tenantId,
        string rotatedByUserId,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ApiKeys
            .FirstOrDefaultAsync(key => key.KeyId == keyId && key.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            if (existing.RevokedAt is null)
            {
                existing.RevokedAt = _timeProvider.GetUtcNow();
                existing.RevokedByUserId = rotatedByUserId;
            }

            var (replacement, plainTextKey) = NewKey(
                existing.TenantId,
                existing.ApplicationId,
                (ReportPermission)existing.Permissions,
                expiresAt);
            _dbContext.ApiKeys.Add(replacement);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ReportApiKeyCreationResult(replacement.KeyId, plainTextKey, ToDescriptor(replacement));
        }).ConfigureAwait(false);
    }

    private (ReportApiKeyEntity Entity, string PlainTextKey) NewKey(
        string tenantId,
        string applicationId,
        ReportPermission permissions,
        DateTimeOffset? expiresAt)
    {
        var plainTextKey = ReportApiKeyMaterial.NewPlainTextKey();
        var entity = new ReportApiKeyEntity
        {
            KeyId = ReportApiKeyMaterial.NewKeyId(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            KeyHash = ReportApiKeyMaterial.ComputeHash(plainTextKey),
            Permissions = (int)permissions,
            CreatedAt = _timeProvider.GetUtcNow(),
            ExpiresAt = expiresAt,
        };
        return (entity, plainTextKey);
    }

    private static ReportApiKeyDescriptor ToDescriptor(ReportApiKeyEntity entity)
        => new()
        {
            KeyId = entity.KeyId,
            TenantId = entity.TenantId,
            ApplicationId = entity.ApplicationId,
            Permissions = (ReportPermission)entity.Permissions,
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt,
            RevokedAt = entity.RevokedAt,
            RevokedByUserId = entity.RevokedByUserId,
        };
}
