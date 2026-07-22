using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Tempo.ReportServer.Api.Security;

namespace Tempo.ReportServer.Api.Tests.Security;

/// <summary>
/// Resilience specification for just-in-time user provisioning: a transient failure of the
/// provisioner (DB outage, primary-key race) must be fail-open — the validated token still
/// authorizes the request rather than turning into a 401/500.
/// </summary>
public sealed class ReportServerJitProvisioningResilienceTests
{
    private const string Issuer = "http://localhost:8080/realms/tempo-reports";
    private const string Audience = "tempo-report-api";

    [Fact]
    public async Task ProvisionerThrows_RequestStillAuthorized()
    {
        var provisioner = new ThrowingProvisioner();
        await using var app = await CreateAppAsync(provisioner);
        var client = app.GetTestClient();
        var token = Signer.Issue("user-jit");

        var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        provisioner.Attempts.Should().BeGreaterThan(0, "the fail-open path only matters once provisioning was attempted");
    }

    private static async Task<WebApplication> CreateAppAsync(IReportServerUserProvisioner provisioner)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Audience"] = Audience,
            ["Authentication:Jwt:RequireHttpsMetadata"] = "false",
        });
        builder.Services.AddReportServerSecurity();
        builder.Services.AddReportServerAuthentication(builder.Configuration);
        // Fail-open subject under test: a provisioner that always throws.
        builder.Services.AddScoped<IReportServerUserProvisioner>(_ => provisioner);
        // Inject the self-signed validation parameters without clobbering the JIT OnTokenValidated event.
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
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

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/secure", () => Results.Ok("ok"))
            .RequireAuthorization(ReportServerAuthenticationDefaults.ApiPolicy);
        await app.StartAsync();
        return app;
    }

    private sealed class ThrowingProvisioner : IReportServerUserProvisioner
    {
        private int _attempts;

        public int Attempts => _attempts;

        public Task<ReportServerUserRecord> UpsertAsync(
            string subject,
            string tenantId,
            string? email,
            string? displayName,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _attempts);
            throw new InvalidOperationException("Simulated transient provisioning failure.");
        }
    }

    private static class Signer
    {
        private static readonly RSA Rsa = RSA.Create(2048);

        public static RsaSecurityKey PublicKey { get; } = new(Rsa);

        public static string Issue(string sub)
        {
            var handler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;
            var claims = new List<Claim>
            {
                new("sub", sub),
                new(ReportPrincipalMapper.TenantClaimType, "tenant-a"),
            };
            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(5),
                signingCredentials: new SigningCredentials(new RsaSecurityKey(Rsa), SecurityAlgorithms.RsaSha256));
            return handler.WriteToken(token);
        }
    }
}
