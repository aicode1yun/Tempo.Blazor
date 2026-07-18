namespace Tempo.ReportServer.Web.Services;

/// <summary>
/// Portal identity seam used by the Report Server shell and pages. It abstracts <em>who is
/// signed in</em>, <em>which tenant their data belongs to</em> and <em>which roles gate the UI</em>,
/// so the portal can run in two modes without the consuming components knowing which:
/// <list type="bullet">
/// <item><description><b>Demo mode</b> (OIDC not configured): the seeded in-memory
/// <see cref="ReportServerSessionState"/> with a user-chosen tenant switcher and full role access —
/// the original self-contained behaviour, unchanged.</description></item>
/// <item><description><b>Auth mode</b> (OIDC configured): identity, tenant and roles are read from the
/// signed-in Keycloak principal's claims (<see cref="OidcPortalIdentity"/>); the tenant is fixed to the
/// user's <c>tenant_id</c> and the tenant switcher is hidden.</description></item>
/// </list>
/// </summary>
public interface IPortalIdentity
{
    /// <summary>Raised when authentication, tenant selection or roles change.</summary>
    event Action? Changed;

    /// <summary>Whether the current session is signed in.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Display name of the signed-in user.</summary>
    string UserName { get; }

    /// <summary>Tenants selectable in the shell (single, fixed tenant in auth mode).</summary>
    IReadOnlyList<ReportServerTenant> Tenants { get; }

    /// <summary>Current tenant id used for data queries against the API.</summary>
    string CurrentTenantId { get; }

    /// <summary>Current tenant display name.</summary>
    string CurrentTenantName { get; }

    /// <summary>Whether the UI may offer a tenant switcher (demo mode only).</summary>
    bool CanSwitchTenant { get; }

    /// <summary>Path an unauthenticated user is sent to (demo login page or OIDC challenge).</summary>
    string SignInPath { get; }

    /// <summary>Path the "sign out" action navigates to (demo login page or OIDC sign-out endpoint).</summary>
    string SignOutPath { get; }

    /// <summary>
    /// Whether sign-in/sign-out navigation targets server endpoints and must use a full browser load
    /// (<see langword="true"/> in auth mode). Demo mode navigates within the Blazor router.
    /// </summary>
    bool UsesExternalAuthNavigation { get; }

    /// <summary>Portal roles resolved for the current user (UI gating only; the API stays authoritative).</summary>
    IReadOnlyList<PortalRole> Roles { get; }

    /// <summary>
    /// Whether the current user reaches at least <paramref name="minimumRole"/> in the portal role
    /// hierarchy (<see cref="PortalRole.Viewer"/> &lt; <see cref="PortalRole.Author"/> &lt;
    /// <see cref="PortalRole.Admin"/>). Demo mode always returns <see langword="true"/>.
    /// </summary>
    bool CanAccess(PortalRole minimumRole);

    /// <summary>Signs the demo user in. No-op in auth mode (login goes through the OIDC challenge).</summary>
    void SignIn(string userName);

    /// <summary>Signs the demo user out. No-op in auth mode (logout goes through the OIDC endpoint).</summary>
    void SignOut();

    /// <summary>Switches the current tenant. No-op when <see cref="CanSwitchTenant"/> is <see langword="false"/>.</summary>
    void SwitchTenant(string tenantId);
}
