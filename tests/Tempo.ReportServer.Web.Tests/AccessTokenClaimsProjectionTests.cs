using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Tests;

/// <summary>
/// Regression guard for the real-Keycloak gap the live E2E exposed: the OIDC cookie principal is built
/// from the id token + userinfo and lacks the role/tenant claims Keycloak only puts in the access token,
/// so without projection the portal UI hid every nav item for a role-bearing user. bUnit could not catch
/// this because it injects claims directly.
/// </summary>
public sealed class AccessTokenClaimsProjectionTests
{
    [Fact]
    public void Project_AddsRealmRolesAndTenant_FromAccessToken()
    {
        var identity = new ClaimsIdentity([new Claim("preferred_username", "author1")], authenticationType: "cookie");
        var accessToken = FakeJwt(new
        {
            realm_access = new { roles = new[] { "report-author", "offline_access" } },
            tenant_id = "tenant-a",
        });

        ReportServerAccessTokenClaims.Project(identity, accessToken);

        identity.HasClaim(c => c.Type == "realm_access").Should().BeTrue();
        identity.FindFirst("tenant_id")!.Value.Should().Be("tenant-a");

        // The projected principal must now read as an author for UI gating.
        var principal = new ClaimsPrincipal(identity);
        PortalClaims.ReadRoles(principal).Should().Contain(PortalRole.Author);
        PortalClaims.ReadTenant(principal).Should().Be("tenant-a");
    }

    [Fact]
    public void Project_DoesNotOverwriteExistingClaim()
    {
        var identity = new ClaimsIdentity([new Claim("tenant_id", "already-set")], authenticationType: "cookie");
        var accessToken = FakeJwt(new { tenant_id = "from-token" });

        ReportServerAccessTokenClaims.Project(identity, accessToken);

        identity.FindAll("tenant_id").Should().ContainSingle();
        identity.FindFirst("tenant_id")!.Value.Should().Be("already-set");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("only.two")]
    public void Project_IgnoresMalformedToken(string token)
    {
        var identity = new ClaimsIdentity([new Claim("preferred_username", "author1")], authenticationType: "cookie");

        ReportServerAccessTokenClaims.Project(identity, token);

        identity.HasClaim(c => c.Type == "realm_access").Should().BeFalse();
    }

    private static string FakeJwt(object payload)
    {
        static string Enc(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = Enc("{\"alg\":\"none\"}");
        var body = Enc(JsonSerializer.Serialize(payload));
        return $"{header}.{body}.sig";
    }
}
