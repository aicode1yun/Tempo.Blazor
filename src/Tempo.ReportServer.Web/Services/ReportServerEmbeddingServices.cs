using Tempo.ReportServer.Api.Security;

namespace Tempo.ReportServer.Web.Services;

/// <summary>Shared constants for the report server embedding demo.</summary>
public static class ReportServerEmbeddingDemo
{
    /// <summary>Deterministic API key used by the local embedding demo.</summary>
    public const string ApiKey = "tmr_demo_embed_key";

    /// <summary>Embedding application id represented by the demo API key.</summary>
    public const string ApplicationId = "tempo-demo-shared-ui";
}

/// <summary>API key store with a deterministic development key plus generated in-memory keys.</summary>
public sealed class DemoReportApiKeyStore : IReportApiKeyStore
{
    private static readonly ReportApiKeyDescriptor DemoDescriptor = new()
    {
        KeyId = "rk_demo_embed",
        TenantId = "northwind",
        ApplicationId = ReportServerEmbeddingDemo.ApplicationId,
        Permissions = ReportPermission.View | ReportPermission.Render | ReportPermission.Export,
        CreatedAt = DateTimeOffset.Parse(
            "2026-06-12T00:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture),
    };

    private readonly InMemoryReportApiKeyStore _generatedKeys = new();
    private readonly object _gate = new();
    private ReportApiKeyDescriptor _demoDescriptor = DemoDescriptor;

    /// <inheritdoc />
    public Task<ReportApiKeyCreationResult> CreateAsync(
        string tenantId,
        string applicationId,
        ReportPermission permissions,
        CancellationToken cancellationToken = default)
        => _generatedKeys.CreateAsync(tenantId, applicationId, permissions, cancellationToken);

    /// <inheritdoc />
    public Task<ReportApiKeyCreationResult> CreateAsync(
        string tenantId,
        string applicationId,
        ReportPermission permissions,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
        => _generatedKeys.CreateAsync(tenantId, applicationId, permissions, expiresAt, cancellationToken);

    /// <inheritdoc />
    public Task<ReportApiKeyDescriptor?> ValidateAsync(
        string plainTextKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(plainTextKey, ReportServerEmbeddingDemo.ApiKey, StringComparison.Ordinal))
        {
            lock (_gate)
            {
                return Task.FromResult(_demoDescriptor.RevokedAt is null ? _demoDescriptor : null);
            }
        }

        return _generatedKeys.ValidateAsync(plainTextKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReportApiKeyDescriptor?> GetAsync(
        string keyId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(keyId, DemoDescriptor.KeyId, StringComparison.Ordinal) &&
            string.Equals(tenantId, DemoDescriptor.TenantId, StringComparison.Ordinal))
        {
            lock (_gate)
            {
                return _demoDescriptor;
            }
        }

        return await _generatedKeys.GetAsync(keyId, tenantId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportApiKeyDescriptor>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var generated = await _generatedKeys.ListAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(tenantId, DemoDescriptor.TenantId, StringComparison.Ordinal))
        {
            return generated;
        }

        ReportApiKeyDescriptor demo;
        lock (_gate)
        {
            demo = _demoDescriptor;
        }

        return [demo, .. generated];
    }

    /// <inheritdoc />
    public Task RevokeAsync(
        string keyId,
        string tenantId,
        string revokedByUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(keyId, DemoDescriptor.KeyId, StringComparison.Ordinal) &&
            string.Equals(tenantId, DemoDescriptor.TenantId, StringComparison.Ordinal))
        {
            lock (_gate)
            {
                _demoDescriptor = _demoDescriptor with
                {
                    RevokedAt = DateTimeOffset.UtcNow,
                    RevokedByUserId = revokedByUserId,
                };
            }

            return Task.CompletedTask;
        }

        return _generatedKeys.RevokeAsync(keyId, tenantId, revokedByUserId, cancellationToken);
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
        if (string.Equals(keyId, DemoDescriptor.KeyId, StringComparison.Ordinal) &&
            string.Equals(tenantId, DemoDescriptor.TenantId, StringComparison.Ordinal))
        {
            ReportApiKeyDescriptor source;
            lock (_gate)
            {
                source = _demoDescriptor;
                _demoDescriptor = _demoDescriptor with
                {
                    RevokedAt = DateTimeOffset.UtcNow,
                    RevokedByUserId = rotatedByUserId,
                };
            }

            return await _generatedKeys
                .CreateAsync(source.TenantId, source.ApplicationId, source.Permissions, expiresAt, cancellationToken)
                .ConfigureAwait(false);
        }

        return await _generatedKeys.RotateAsync(keyId, tenantId, rotatedByUserId, expiresAt, cancellationToken)
            .ConfigureAwait(false);
    }
}
