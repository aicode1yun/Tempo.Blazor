namespace Tempo.ReportServer.Api.Security;

/// <summary>A just-in-time provisioned report server user projected from an OIDC token.</summary>
public sealed record ReportServerUserRecord
{
    /// <summary>OIDC subject identifier.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Email address.</summary>
    public string? Email { get; init; }

    /// <summary>Display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Timestamp of the first authentication.</summary>
    public DateTimeOffset FirstSeenAt { get; init; }

    /// <summary>Timestamp of the most recent authentication.</summary>
    public DateTimeOffset LastSeenAt { get; init; }
}

/// <summary>Just-in-time provisions (upserts) a report server user on authentication.</summary>
public interface IReportServerUserProvisioner
{
    /// <summary>Inserts the user on first sight, otherwise refreshes profile fields and last-seen.</summary>
    Task<ReportServerUserRecord> UpsertAsync(
        string subject,
        string tenantId,
        string? email,
        string? displayName,
        CancellationToken cancellationToken = default);
}

/// <summary>No-op provisioner used when persistence is not enabled (in-memory hosts and tests).</summary>
public sealed class NullReportServerUserProvisioner : IReportServerUserProvisioner
{
    /// <inheritdoc />
    public Task<ReportServerUserRecord> UpsertAsync(
        string subject,
        string tenantId,
        string? email,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new ReportServerUserRecord
        {
            Subject = subject,
            TenantId = tenantId,
            Email = email,
            DisplayName = displayName,
            FirstSeenAt = now,
            LastSeenAt = now,
        });
    }
}
