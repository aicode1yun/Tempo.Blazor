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

    /// <summary>Embedding application identifier.</summary>
    public string ApplicationId { get; init; } = string.Empty;

    /// <summary>Allowed operation scopes.</summary>
    public ReportPermission Permissions { get; init; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Revocation timestamp.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>User that revoked the key.</summary>
    public string? RevokedByUserId { get; init; }
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

    /// <summary>Validates a plain text key and returns a non-revoked descriptor.</summary>
    Task<ReportApiKeyDescriptor?> ValidateAsync(string plainTextKey, CancellationToken cancellationToken = default);

    /// <summary>Gets a stored descriptor by id and tenant.</summary>
    Task<ReportApiKeyDescriptor?> GetAsync(
        string keyId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes a key.</summary>
    Task RevokeAsync(
        string keyId,
        string tenantId,
        string revokedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>In-memory API key store with hashed secret material.</summary>
public sealed class InMemoryReportApiKeyStore : IReportApiKeyStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredApiKey> _keys = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<ReportApiKeyCreationResult> CreateAsync(
        string tenantId,
        string applicationId,
        ReportPermission permissions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keyId = $"rk_{Guid.NewGuid():N}";
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Base64UrlEncode(secretBytes);
        var plainTextKey = $"tmr_{secret}";
        var descriptor = new ReportApiKeyDescriptor
        {
            KeyId = keyId,
            TenantId = tenantId,
            ApplicationId = applicationId,
            Permissions = permissions,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var stored = new StoredApiKey(descriptor, Hash(plainTextKey));
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
        if (!plainTextKey.StartsWith("tmr_", StringComparison.Ordinal))
        {
            return Task.FromResult<ReportApiKeyDescriptor?>(null);
        }

        var candidateHash = Hash(plainTextKey);
        lock (_gate)
        {
            var descriptor = _keys.Values
                .Where(stored => stored.Descriptor.RevokedAt is null)
                .FirstOrDefault(stored => CryptographicOperations.FixedTimeEquals(candidateHash, stored.KeyHash))
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
    public Task RevokeAsync(
        string keyId,
        string tenantId,
        string revokedByUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_keys.TryGetValue(keyId, out var stored) &&
                string.Equals(stored.Descriptor.TenantId, tenantId, StringComparison.Ordinal))
            {
                _keys[keyId] = stored with
                {
                    Descriptor = stored.Descriptor with
                    {
                        RevokedAt = DateTimeOffset.UtcNow,
                        RevokedByUserId = revokedByUserId,
                    },
                };
            }
        }

        return Task.CompletedTask;
    }

    private static byte[] Hash(string value)
        => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record StoredApiKey(ReportApiKeyDescriptor Descriptor, byte[] KeyHash);
}

#pragma warning restore MA0048, MA0158
