using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Tests;

public sealed class PortalIdentityTests
{
    [Fact]
    public void RoleHierarchy_Reaches_RespectsRank()
    {
        PortalClaims.Reaches([PortalRole.Viewer], PortalRole.Viewer).Should().BeTrue();
        PortalClaims.Reaches([PortalRole.Viewer], PortalRole.Author).Should().BeFalse();
        PortalClaims.Reaches([PortalRole.Author], PortalRole.Viewer).Should().BeTrue();
        PortalClaims.Reaches([PortalRole.Author], PortalRole.Admin).Should().BeFalse();
        PortalClaims.Reaches([PortalRole.Admin], PortalRole.Author).Should().BeTrue();
        PortalClaims.Reaches([], PortalRole.Viewer).Should().BeFalse();
    }

    [Fact]
    public void Oidc_MapsIdentityTenantAndRoles_FromClaims()
    {
        var identity = CreateIdentity(userName: "author1", tenant: "tenant-a", realmRoles: ["report-author"]);

        identity.IsAuthenticated.Should().BeTrue();
        identity.UserName.Should().Be("author1");
        identity.CurrentTenantId.Should().Be("tenant-a");
        identity.CurrentTenantName.Should().Be("tenant-a");
        identity.CanSwitchTenant.Should().BeFalse();
        identity.UsesExternalAuthNavigation.Should().BeTrue();
        identity.SignInPath.Should().Be("/account/login");
        identity.SignOutPath.Should().Be("/account/logout");
        identity.Tenants.Should().ContainSingle(tenant => tenant.Id == "tenant-a");
        identity.Roles.Should().Contain(PortalRole.Author);
    }

    [Fact]
    public void Oidc_Author_CanAccessAuthorNotAdmin()
    {
        var identity = CreateIdentity(userName: "author1", tenant: "tenant-a", realmRoles: ["report-author"]);

        identity.CanAccess(PortalRole.Viewer).Should().BeTrue();
        identity.CanAccess(PortalRole.Author).Should().BeTrue();
        identity.CanAccess(PortalRole.Admin).Should().BeFalse();
    }

    [Fact]
    public void Oidc_Viewer_CannotAccessAuthor()
    {
        var identity = CreateIdentity(userName: "viewer1", tenant: "tenant-a", realmRoles: ["report-viewer"]);

        identity.CanAccess(PortalRole.Viewer).Should().BeTrue();
        identity.CanAccess(PortalRole.Author).Should().BeFalse();
    }

    [Fact]
    public void Oidc_Admin_CanAccessAdmin()
    {
        var identity = CreateIdentity(userName: "admin1", tenant: "tenant-a", realmRoles: ["report-admin"]);

        identity.CanAccess(PortalRole.Admin).Should().BeTrue();
    }

    [Fact]
    public void Oidc_NoTenantClaim_FallsBackToDefault()
    {
        var identity = CreateIdentity(userName: "author1", tenant: null, realmRoles: ["report-author"]);

        identity.CurrentTenantId.Should().Be(PortalClaims.DefaultTenantId);
    }

    [Fact]
    public void Oidc_Anonymous_IsNotAuthenticated()
    {
        var provider = new StubAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity()));
        using var identity = new OidcPortalIdentity(provider);

        identity.IsAuthenticated.Should().BeFalse();
        identity.CanAccess(PortalRole.Viewer).Should().BeFalse();
    }

    [Fact]
    public void Oidc_RaisesChanged_WhenAuthenticationStateChanges()
    {
        var provider = new StubAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity()));
        using var identity = new OidcPortalIdentity(provider);
        var raised = false;
        identity.Changed += () => raised = true;

        provider.SetPrincipal(BuildPrincipal("author1", "tenant-a", ["report-author"]));

        raised.Should().BeTrue();
        identity.UserName.Should().Be("author1");
        identity.Roles.Should().Contain(PortalRole.Author);
    }

    private static OidcPortalIdentity CreateIdentity(string userName, string? tenant, string[] realmRoles)
        => new(new StubAuthenticationStateProvider(BuildPrincipal(userName, tenant, realmRoles)));

    private static ClaimsPrincipal BuildPrincipal(string userName, string? tenant, string[] realmRoles)
    {
        var claims = new List<Claim>
        {
            new("sub", $"kc-{userName}"),
            new("preferred_username", userName),
        };
        if (!string.IsNullOrWhiteSpace(tenant))
        {
            claims.Add(new Claim(PortalClaims.TenantClaimType, tenant));
        }

        // Keycloak-shaped realm_access JSON, matching PortalClaims' parsing.
        var rolesJson = "{\"roles\":[" + string.Join(",", realmRoles.Select(role => $"\"{role}\"")) + "]}";
        claims.Add(new Claim("realm_access", rolesJson));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState _state;

        public StubAuthenticationStateProvider(ClaimsPrincipal principal)
            => _state = new AuthenticationState(principal);

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(_state);

        public void SetPrincipal(ClaimsPrincipal principal)
        {
            _state = new AuthenticationState(principal);
            NotifyAuthenticationStateChanged(Task.FromResult(_state));
        }
    }
}
