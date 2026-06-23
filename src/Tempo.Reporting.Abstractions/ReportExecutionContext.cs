namespace Tempo.Reporting.Abstractions;

/// <summary>Tenant-scoped execution context passed through reporting providers and render jobs.</summary>
public sealed record ReportExecutionContext
{
    /// <summary>Creates an execution context with normalized non-null values.</summary>
    public ReportExecutionContext(
        string TenantId,
        string UserId,
        string CultureName,
        IReadOnlyDictionary<string, string>? Claims = null,
        CancellationToken CancellationToken = default)
    {
        this.TenantId = TenantId ?? string.Empty;
        this.UserId = UserId ?? string.Empty;
        this.CultureName = string.IsNullOrWhiteSpace(CultureName) ? "en-US" : CultureName;
        this.Claims = Claims is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(Claims, StringComparer.Ordinal);
        this.CancellationToken = CancellationToken;
    }

    /// <summary>Tenant identifier for data isolation and quotas.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>User identifier for auditing and data-provider filtering.</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Culture name used during expression formatting and localization.</summary>
    public string CultureName { get; init; } = "en-US";

    /// <summary>Immutable claim snapshot used by reporting providers.</summary>
    public IReadOnlyDictionary<string, string> Claims { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Cancellation token for provider, store and render operations.</summary>
    public CancellationToken CancellationToken { get; init; }
}
