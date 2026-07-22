using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Web.Components;
using Tempo.ReportServer.Web.Services;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

/// <summary>
/// Shell behaviour in auth mode (OIDC configured): the portal reflects the signed-in Keycloak
/// principal — real identity/tenant, no tenant switcher, and nav gated by the role matrix
/// (viewer: Reports; author: + Designer/Schedules/Revisions; admin: + Data sources/Permissions/API keys).
/// </summary>
public sealed class ReportServerShellAuthTests : ReportServerWebTestBase
{
    [Fact]
    public void AuthMode_ShowsRealIdentityAndFixedTenant_NoSwitcher()
    {
        AuthorizeAs("author1", "tenant-a", "report-author");

        var cut = RenderShell();

        cut.Find("[data-testid='signed-in-user']").TextContent.Should().Contain("author1");
        cut.Find("[data-testid='tenant-display']").TextContent.Should().Contain("tenant-a");
        cut.FindAll("[data-testid='tenant-switcher']").Should().BeEmpty();
    }

    [Fact]
    public void AuthMode_Viewer_SeesOnlyReports()
    {
        AuthorizeAs("viewer1", "tenant-a", "report-viewer");

        var cut = RenderShell();

        cut.FindAll("[data-testid='nav-reports']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-designer']").Should().BeEmpty();
        cut.FindAll("[data-testid='nav-schedules']").Should().BeEmpty();
        cut.FindAll("[data-testid='nav-revisions']").Should().BeEmpty();
        cut.FindAll("[data-testid='nav-datasources']").Should().BeEmpty();
        cut.FindAll("[data-testid='nav-permissions']").Should().BeEmpty();
        cut.FindAll("[data-testid='nav-apikeys']").Should().BeEmpty();
    }

    [Fact]
    public void AuthMode_Author_SeesAuthoringButNotAdmin()
    {
        AuthorizeAs("author1", "tenant-a", "report-author");

        var cut = RenderShell();

        cut.FindAll("[data-testid='nav-reports']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-designer']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-schedules']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-revisions']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-datasources']").Should().BeEmpty();
        cut.FindAll("[data-testid='nav-permissions']").Should().BeEmpty();
        cut.FindAll("[data-testid='nav-apikeys']").Should().BeEmpty();
    }

    [Fact]
    public void AuthMode_Admin_SeesEverything()
    {
        AuthorizeAs("admin1", "tenant-a", "report-admin");

        var cut = RenderShell();

        cut.FindAll("[data-testid='nav-reports']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-designer']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-schedules']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-revisions']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-datasources']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-permissions']").Should().ContainSingle();
        cut.FindAll("[data-testid='nav-apikeys']").Should().ContainSingle();
    }

    private void AuthorizeAs(string userName, string tenant, string realmRole)
    {
        // Auth mode: provide a Keycloak-shaped signed-in principal and the OIDC-backed identity that
        // reads it. The shell consumes IPortalIdentity, so no cascading AuthorizeView setup is needed.
        var principal = BuildPrincipal(userName, tenant, realmRole);
        Services.AddScoped<AuthenticationStateProvider>(_ => new StubAuthenticationStateProvider(principal));
        Services.AddScoped<IPortalIdentity, OidcPortalIdentity>();
    }

    private static ClaimsPrincipal BuildPrincipal(string userName, string tenant, string realmRole)
    {
        var claims = new List<Claim>
        {
            new("sub", $"kc-{userName}"),
            new("preferred_username", userName),
            new(PortalClaims.TenantClaimType, tenant),
            new("realm_access", $"{{\"roles\":[\"{realmRole}\"]}}"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private IRenderedComponent<ReportServerShell> RenderShell()
        => Render<ReportServerShell>(parameters => parameters
            .Add(component => component.Title, "Reports")
            .Add(component => component.ActiveSection, "reports")
            .AddChildContent("<div data-testid='shell-body'>Body</div>"));

    private sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state;

        public StubAuthenticationStateProvider(ClaimsPrincipal principal)
            => _state = new AuthenticationState(principal);

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(_state);
    }
}
