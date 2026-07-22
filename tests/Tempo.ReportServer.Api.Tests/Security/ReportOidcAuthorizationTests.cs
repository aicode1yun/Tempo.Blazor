using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Tempo.ReportServer.Api.Security;
using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.Tests.Security;

/// <summary>
/// End-to-end authorization specification for the real OIDC (Keycloak) path, exercised with
/// self-signed access tokens that carry the same issuer/audience/claim shape Keycloak emits
/// (<c>sub</c>, <c>tenant_id</c>, <c>realm_access.roles</c>, <c>resource_access</c>). This keeps
/// the suite fast and independent of a running Keycloak while validating the JWT → capability
/// bridge (<see cref="ReportPrincipalMapper"/>), the folder ACL resolver, and the endpoint filter.
/// </summary>
public sealed class ReportOidcAuthorizationTests
{
    private const string Issuer = "http://localhost:8080/realms/tempo-reports";
    private const string Audience = "tempo-report-api";
    private const string Tenant = "tenant-a";

    [Fact]
    public async Task Viewer_CanRender_ButCannotEditOrChangeAcl()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        var token = Signer.Issue(sub: "viewer-user", tenant: Tenant, realmRoles: ["report-viewer"]);

        (await Post(client, "/folders/finance/reports/orders/render", token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(client, "/folders/finance/reports", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(client, "/folders/finance/acl", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Author_CanEditInGrantedFolder_ButIsDeniedInLockedFolder()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        var token = Signer.Issue(sub: "author-user", tenant: Tenant, realmRoles: ["report-author"]);

        // Author capability ceiling includes EditDefinition, refined per folder by the ACL.
        (await Post(client, "/folders/finance/reports", token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(client, "/folders/locked/reports", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(client, "/folders/finance/acl", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_CanDoEverythingIncludingAcl()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        var token = Signer.Issue(sub: "admin-user", tenant: Tenant, realmRoles: ["report-admin"]);

        (await Post(client, "/folders/finance/reports/orders/render", token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(client, "/folders/finance/reports", token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(client, "/folders/finance/acl", token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ServiceAccount_WithRenderClientRole_IsLimitedToRenderScope()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        var token = Signer.Issue(
            sub: "service-account-tempo-report-m2m",
            tenant: Tenant,
            realmRoles: [],
            clientRoles: ("tempo-report-api", ["report.render"]));

        (await Post(client, "/folders/finance/reports/orders/render", token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(client, "/folders/finance/reports", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Token_WithWrongAudience_IsRejectedWith401()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        var token = Signer.Issue(sub: "viewer-user", tenant: Tenant, realmRoles: ["report-viewer"], audience: "some-other-api");

        (await Post(client, "/folders/finance/reports/orders/render", token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithoutToken_IsRejectedWith401()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        (await Post(client, "/folders/finance/reports/orders/render", token: null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string uri, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client.SendAsync(request);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddReportServerSecurity();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.Configuration = new OpenIdConnectConfiguration();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = Issuer,
                    ValidAudience = Audience,
                    IssuerSigningKey = Signer.PublicKey,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                };
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();

        var store = app.Services.GetRequiredService<IReportPermissionStore>();
        var context = new ReportExecutionContext(Tenant, "seed", "en-US");
        await store.SaveFolderAsync(new ReportFolderPermissionNode("finance"), context);
        await store.SaveFolderAsync(new ReportFolderPermissionNode("locked"), context);
        // Folder-scoped refinement below the role ceiling: authors are denied edits in "locked".
        await store.SetAclEntriesAsync(
            "locked",
            [ReportFolderAclEntry.DenyRole("locked", ReportServerRole.Author, ReportPermission.EditDefinition)],
            context);

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapPost("/folders/{folderId}/reports/{reportId}/render", () => Results.Ok("rendered"))
            .RequireAuthorization()
            .RequireReportPermission(ReportPermission.Render, ReportResourceKind.Render);
        app.MapPost("/folders/{folderId}/reports", () => Results.Ok("edited"))
            .RequireAuthorization()
            .RequireReportPermission(ReportPermission.EditDefinition, ReportResourceKind.ReportDefinition, requiresAuthorRole: true);
        app.MapPost("/folders/{folderId}/acl", () => Results.Ok("acl"))
            .RequireAuthorization()
            .RequireReportPermission(ReportPermission.ManagePermissions, ReportResourceKind.Acl);

        await app.StartAsync();
        return app;
    }

    private static class Signer
    {
        private static readonly RSA Rsa = RSA.Create(2048);

        public static RsaSecurityKey PublicKey { get; } = new(Rsa);

        public static string Issue(
            string sub,
            string tenant,
            string[] realmRoles,
            (string ClientId, string[] Roles)? clientRoles = null,
            string audience = Audience)
        {
            var handler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;
            var claims = new List<System.Security.Claims.Claim>
            {
                new("sub", sub),
                new(ReportPrincipalMapper.TenantClaimType, tenant),
                new("realm_access", $"{{\"roles\":[{string.Join(",", realmRoles.Select(role => $"\"{role}\""))}]}}", System.IdentityModel.Tokens.Jwt.JsonClaimValueTypes.Json),
            };
            if (clientRoles is { } cr)
            {
                var rolesJson = string.Join(",", cr.Roles.Select(role => $"\"{role}\""));
                claims.Add(new System.Security.Claims.Claim(
                    "resource_access",
                    $"{{\"{cr.ClientId}\":{{\"roles\":[{rolesJson}]}}}}",
                    System.IdentityModel.Tokens.Jwt.JsonClaimValueTypes.Json));
            }

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(5),
                signingCredentials: new SigningCredentials(new RsaSecurityKey(Rsa), SecurityAlgorithms.RsaSha256));
            return handler.WriteToken(token);
        }
    }
}
