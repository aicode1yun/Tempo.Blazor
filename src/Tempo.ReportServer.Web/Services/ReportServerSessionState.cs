#pragma warning disable MA0046

namespace Tempo.ReportServer.Web.Services;

/// <summary>Small in-memory session used by the F12 demo shell.</summary>
public sealed class ReportServerSessionState
{
    private readonly List<ReportServerTenant> _tenants =
    [
        new("northwind", "Northwind Finance"),
        new("contoso", "Contoso Operations"),
    ];

    /// <summary>Raised when authentication or tenant selection changes.</summary>
    public event Action? Changed;

    /// <summary>Whether the current browser session is signed in.</summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>Display name of the signed-in user.</summary>
    public string UserName { get; private set; } = "Report Author";

    /// <summary>Tenants available to the user.</summary>
    public IReadOnlyList<ReportServerTenant> Tenants => _tenants;

    /// <summary>Current tenant id.</summary>
    public string CurrentTenantId { get; private set; } = "northwind";

    /// <summary>Current tenant display name.</summary>
    public string CurrentTenantName
        => _tenants.FirstOrDefault(tenant => string.Equals(tenant.Id, CurrentTenantId, StringComparison.Ordinal))?.Name
            ?? CurrentTenantId;

    /// <summary>Signs the demo user in.</summary>
    public void SignIn(string userName)
    {
        if (!string.IsNullOrWhiteSpace(userName))
        {
            UserName = userName.Trim();
        }

        IsAuthenticated = true;
        Changed?.Invoke();
    }

    /// <summary>Signs the demo user out.</summary>
    public void SignOut()
    {
        IsAuthenticated = false;
        Changed?.Invoke();
    }

    /// <summary>Switches the current tenant.</summary>
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
