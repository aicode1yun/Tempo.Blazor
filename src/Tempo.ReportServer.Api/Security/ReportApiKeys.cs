#pragma warning disable MA0048, MA0158

using System.Security.Cryptography;

namespace Tempo.ReportServer.Api.Security;

/// <summary>Descriptor for a stored embedding API key.</summary>
public sealed record ReportApiKeyDescriptor
{
    /// <summary>Key identifier.</summary>
    public string KeyId { get; init; } = string.Empty;

    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Embedding application identifier (machine principal / service account).</summary>
    public string ApplicationId { get; init; } = string.Empty;

    /// <summary>Allowed operation scopes.</summary>
    public ReportPermission Permissions { get; init; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Optional expiration timestamp; a key is invalid once this instant has passed.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Revocation timestamp.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>User that revoked the key.</summary>
    public string? RevokedByUserId { get; init; }

    /// <summary>Returns true when the key is expired relative to <paramref name="now"/>.</summary>
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiry && expiry <= now;

    /// <summary>Returns true when the key is active (neither revoked nor expired) at <paramref name="now"/>.</summary>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && !IsExpired(now);
}

/// <summary>API key creation result containing the one-time plain text key.</summary>
public sealed record ReportApiKeyCreationResult(
    string KeyId,
    string PlainTextKey,
    ReportApiKeyDescriptor Descriptor);

/// <summary>Embedding API key store.</summary>
public interface IReportApiKeyStore
{
    /// <summary>Creates a tenant/application-scoped API key.</summary>
    Task<ReportApiKeyCreationResult> CreateAsync(
        string tenantId,
        string applicationId,
        ReportPermission permissions,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a tenant/application-scoped API key with an optional expiration.</summary>
    Task<ReportApiKeyCreationResult> CreateAsync(
        string tenantId,
        string applicationId,
        ReportPermission permissions,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>Validates a plain text key and returns a non-revoked, non-expired descriptor.</summary>
    Task<ReportApiKeyDescriptor?> ValidateAsync(string plainTextKey, CancellationToken cancellationToken = default);

    /// <summary>Gets a stored descriptor by id and tenant.</summary>
    Task<ReportApiKeyDescriptor?> GetAsync(
        string keyId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists all key descriptors for a tenant (secret material is never returned).</summary>
    Task<IReadOnlyList<ReportApiKeyDescriptor>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes a key.</summary>
    Task RevokeAsync(
        string keyId,
        string tenantId,
        string revokedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically rotates a key: the existing (non-revoked) key is revoked and a replacement key is
    /// issued for the same application and permission scope. Returns the new one-time key, or
    /// <see langword="null"/> when the source key does not exist for the tenant.
    /// </summary>
    Task<ReportApiKeyCreationResult?> RotateAsync(
        string keyId,
        string tenantId,
        string rotatedByUserId,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Shared helpers for report server API key generation and hashing.</summary>
public static class ReportApiKeyMaterial
{
    /// <summary>Prefix that every issued plain text key carries.</summary>
    public const string KeyPrefix = "tmr_";

    /// <summary>Creates a new random key identifier.</summary>
    public static string NewKeyId() => $"rk_{Guid.NewGuid():N}";

    /// <summary>Generates a new high-entropy plain text key (256 bits of randomness).</summary>
    public static string NewPlainTextKey()
    {
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        return KeyPrefix + Base64UrlEncode(secretBytes);
    }

    /// <summary>Computes the SHA-256 hash of a plain text key as a base64 string suitable for indexed lookup.</summary>
    public static string ComputeHash(string plainTextKey)
        => Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plainTextKey)));

    /// <summary>Returns true when the value carries the report server key prefix.</summary>
    public static bool HasKeyPrefix(string? plainTextKey)
        => plainTextKey is not null && plainTextKey.StartsWith(KeyPrefix, StringComparison.Ordinal);

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>In-memory API key store with hashed secret material.</summary>
public sealed class InMemoryReportApiKeyStore : IReportApiKeyStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredApiKey> _keys = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates an in-memory API key store.</summary>
    public InMemoryReportApiKeyStore()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Creates an in-memory API key store with an explicit time source (testability).</summary>
    public InMemoryReportApiKeyStore(TimeProvider timeProvider)
    {
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
    public Task<ReportApiKeyCreationResult> CreateAsync(
        string tenantId,
        string applicationId,
        ReportPermission permissions,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keyId = ReportApiKeyMaterial.NewKeyId();
        var plainTextKey = ReportApiKeyMaterial.NewPlainTextKey();
        var descriptor = new ReportApiKeyDescriptor
        {
            KeyId = keyId,
            TenantId = tenantId,
            ApplicationId = applicationId,
            Permissions = permissions,
            CreatedAt = _timeProvider.GetUtcNow(),
            ExpiresAt = expiresAt,
        };
        var stored = new StoredApiKey(descriptor, ReportApiKeyMaterial.ComputeHash(plainTextKey));
        lock (_gate)
        {
            _keys[keyId] = stored;
        }

        return Task.FromResult(new ReportApiKeyCreationResult(keyId, plainTextKey, descriptor));
    }

    /// <inheritdoc />
    public Task<ReportApiKeyDescriptor?> ValidateAsync(
        string plainTextKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReportApiKeyMaterial.HasKeyPrefix(plainTextKey))
        {
            return Task.FromResult<ReportApiKeyDescriptor?>(null);
        }

        var candidateHash = ReportApiKeyMaterial.ComputeHash(plainTextKey);
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            var descriptor = _keys.Values
                .Where(stored => stored.Descriptor.IsActive(now))
                .FirstOrDefault(stored => string.Equals(stored.KeyHash, candidateHash, StringComparison.Ordinal))
                ?.Descriptor;
            return Task.FromResult(descriptor);
        }
    }

    /// <inheritdoc />
    public Task<ReportApiKeyDescriptor?> GetAsync(
        string keyId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var descriptor = _keys.TryGetValue(keyId, out var stored) &&
                string.Equals(stored.Descriptor.TenantId, tenantId, StringComparison.Ordinal)
                    ? stored.Descriptor
                    : null;
            return Task.FromResult(descriptor);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReportApiKeyDescriptor>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var descriptors = _keys.Values
                .Where(stored => string.Equals(stored.Descriptor.TenantId, tenantId, StringComparison.Ordinal))
                .Select(stored => stored.Descriptor)
                .OrderByDescending(descriptor => descriptor.CreatedAt)
                .ToArray();
            return Task.FromResult((IReadOnlyList<ReportApiKeyDescriptor>)descriptors);
        }
    }

    /// <inheritdoc />
    public Task RevokeAsync(
        string keyId,
        string tenantId,
        string revokedByUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RevokeLocked(keyId, tenantId, revokedByUserId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ReportApiKeyCreationResult?> RotateAsync(
        string keyId,
        string tenantId,
        string rotatedByUserId,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReportApiKeyDescriptor source;
        lock (_gate)
        {
            if (!_keys.TryGetValue(keyId, out var stored) ||
                !string.Equals(stored.Descriptor.TenantId, tenantId, StringComparison.Ordinal))
            {
                return null;
            }

            source = stored.Descriptor;
            RevokeLocked(keyId, tenantId, rotatedByUserId);
        }

        return await CreateAsync(source.TenantId, source.ApplicationId, source.Permissions, expiresAt, cancellationToken)
            .ConfigureAwait(false);
    }

    private void RevokeLocked(string keyId, string tenantId, string revokedByUserId)
    {
        if (_keys.TryGetValue(keyId, out var stored) &&
            string.Equals(stored.Descriptor.TenantId, tenantId, StringComparison.Ordinal) &&
            stored.Descriptor.RevokedAt is null)
        {
            _keys[keyId] = stored with
            {
                Descriptor = stored.Descriptor with
                {
                    RevokedAt = _timeProvider.GetUtcNow(),
                    RevokedByUserId = revokedByUserId,
                },
            };
        }
    }

    private sealed record StoredApiKey(ReportApiKeyDescriptor Descriptor, string KeyHash);
}

#pragma warning restore MA0048, MA0158
