using Microsoft.AspNetCore.Components.Authorization;

namespace Tempo.ReportServer.Web.Services;

/// <summary>
/// <see cref="IPortalIdentity"/> backed by the signed-in Keycloak OIDC principal. Identity, tenant and
/// roles are read from the ambient <see cref="AuthenticationStateProvider"/> — the same claim shapes the
/// API uses — so the portal reflects the real user instead of the demo session. The tenant is fixed to
/// the principal's <c>tenant_id</c> claim (no switcher), and roles gate page/action visibility. Used only
/// when OIDC is configured; otherwise the demo <see cref="ReportServerSessionState"/> is registered.
/// </summary>
public sealed class OidcPortalIdentity : IPortalIdentity, IDisposable
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    private IReadOnlyList<PortalRole> _roles = [];
    private string _userName = string.Empty;
    private string _tenantId = PortalClaims.DefaultTenantId;
    private string _tenantName = PortalClaims.DefaultTenantId;
    private bool _isAuthenticated;

    /// <summary>Creates the OIDC-backed identity and subscribes to authentication-state changes.</summary>
    public OidcPortalIdentity(AuthenticationStateProvider authenticationStateProvider)
    {
        ArgumentNullException.ThrowIfNull(authenticationStateProvider);
        _authenticationStateProvider = authenticationStateProvider;
        _authenticationStateProvider.AuthenticationStateChanged += HandleAuthenticationStateChanged;

        // Populate synchronously when the provider already has state (server prerender / deserialized
        // WASM state), otherwise refresh as soon as the state task completes and notify consumers.
        var stateTask = _authenticationStateProvider.GetAuthenticationStateAsync();
        if (stateTask.IsCompletedSuccessfully)
        {
            Apply(stateTask.Result);
        }
        else
        {
            _ = RefreshAsync(stateTask);
        }
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public bool IsAuthenticated => _isAuthenticated;

    /// <inheritdoc />
    public string UserName => _userName;

    /// <inheritdoc />
    public IReadOnlyList<ReportServerTenant> Tenants => [new(_tenantId, _tenantName)];

    /// <inheritdoc />
    public string CurrentTenantId => _tenantId;

    /// <inheritdoc />
    public string CurrentTenantName => _tenantName;

    /// <inheritdoc />
    public bool CanSwitchTenant => false;

    /// <inheritdoc />
    public string SignInPath => "/account/login";

    /// <inheritdoc />
    public string SignOutPath => "/account/logout";

    /// <inheritdoc />
    public bool UsesExternalAuthNavigation => true;

    /// <inheritdoc />
    public IReadOnlyList<PortalRole> Roles => _roles;

    /// <inheritdoc />
    public bool CanAccess(PortalRole minimumRole) => PortalClaims.Reaches(_roles, minimumRole);

    /// <inheritdoc />
    public void SignIn(string userName)
    {
        // Sign-in is delegated to the OIDC challenge endpoint; the shell navigates to SignInPath.
    }

    /// <inheritdoc />
    public void SignOut()
    {
        // Sign-out is delegated to the OIDC sign-out endpoint; the shell navigates to SignOutPath.
    }

    /// <inheritdoc />
    public void SwitchTenant(string tenantId)
    {
        // Tenant is fixed to the principal's claim in auth mode; switching is not offered.
    }

    /// <inheritdoc />
    public void Dispose()
        => _authenticationStateProvider.AuthenticationStateChanged -= HandleAuthenticationStateChanged;

    private async Task RefreshAsync(Task<AuthenticationState> stateTask)
    {
        var state = await stateTask.ConfigureAwait(false);
        Apply(state);
        Changed?.Invoke();
    }

    private void HandleAuthenticationStateChanged(Task<AuthenticationState> stateTask)
        => _ = RefreshAsync(stateTask);

    private void Apply(AuthenticationState state)
    {
        var principal = state.User;
        _isAuthenticated = principal.Identity?.IsAuthenticated == true;
        _tenantId = PortalClaims.ReadTenant(principal);
        _tenantName = principal.FindFirst("tenant_name")?.Value ?? _tenantId;
        _roles = PortalClaims.ReadRoles(principal);
        _userName = PortalClaims.ReadDisplayName(principal);
    }
}
