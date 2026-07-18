#pragma warning disable MA0046

namespace Tempo.ReportServer.Web.Services;

/// <summary>
/// In-memory demo session backing <see cref="IPortalIdentity"/> when OIDC is not configured.
/// Keeps the original self-contained portal behaviour: a user-chosen display name, a tenant
/// switcher across the seeded demo tenants and full role access (nothing is hidden).
/// </summary>
public sealed class ReportServerSessionState : IPortalIdentity
{
    // Demo mode is the self-contained showcase: the seeded identity sees every page/action.
    private static readonly IReadOnlyList<PortalRole> DemoRoles =
    [
        PortalRole.Viewer,
        PortalRole.Author,
        PortalRole.Admin,
    ];

    private readonly List<ReportServerTenant> _tenants =
    [
        new("northwind", "Northwind Finance"),
        new("contoso", "Contoso Operations"),
    ];

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public bool IsAuthenticated { get; private set; }

    /// <inheritdoc />
    public string UserName { get; private set; } = "Report Author";

    /// <inheritdoc />
    public IReadOnlyList<ReportServerTenant> Tenants => _tenants;

    /// <inheritdoc />
    public string CurrentTenantId { get; private set; } = "northwind";

    /// <inheritdoc />
    public string CurrentTenantName
        => _tenants.FirstOrDefault(tenant => string.Equals(tenant.Id, CurrentTenantId, StringComparison.Ordinal))?.Name
            ?? CurrentTenantId;

    /// <inheritdoc />
    public bool CanSwitchTenant => true;

    /// <inheritdoc />
    public string SignInPath => "/login";

    /// <inheritdoc />
    public string SignOutPath => "/login";

    /// <inheritdoc />
    public bool UsesExternalAuthNavigation => false;

    /// <inheritdoc />
    public IReadOnlyList<PortalRole> Roles => DemoRoles;

    /// <inheritdoc />
    public bool CanAccess(PortalRole minimumRole) => true;

    /// <inheritdoc />
    public void SignIn(string userName)
    {
        if (!string.IsNullOrWhiteSpace(userName))
        {
            UserName = userName.Trim();
        }

        IsAuthenticated = true;
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void SignOut()
    {
        IsAuthenticated = false;
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void SwitchTenant(string tenantId)
    {
        if (_tenants.Any(tenant => string.Equals(tenant.Id, tenantId, StringComparison.Ordinal)))
        {
            CurrentTenantId = tenantId;
            Changed?.Invoke();
        }
    }
}

#pragma warning restore MA0046
