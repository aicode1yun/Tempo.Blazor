#pragma warning disable MA0048

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tempo.ReportServer.Api.Security;

/// <summary>
/// Options for the report server development authentication scheme. Bound from the
/// <c>Authentication:Dev</c> configuration section.
/// </summary>
/// <remarks>
/// This scheme exists ONLY to run the Keycloak-free CI/E2E lane (see
/// <c>tests/Tempo.Blazor.E2E/ReportServerE2ETestBase.cs</c>): it lets the OIDC-off Web portal call the
/// real Api catalog/favorites/render endpoints with an authenticated principal without a running
/// Keycloak. It is strictly opt-in via <c>Authentication:Dev:Enabled=true</c>; when unset the Api
/// authenticates exactly as before (JWT bearer + API key). It MUST NOT be enabled in production.
/// </remarks>
public sealed class ReportServerDevAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Authentication:Dev";

    /// <summary>
    /// Informational only. The real gate is startup registration: the scheme is registered (and this
    /// section bound) solely when <see cref="ReportServerDevAuthentication.IsDevAuthenticationEnabled"/>
    /// reads <c>Authentication:Dev:Enabled=true</c> AND the environment is not Production. The handler
    /// never consults this property — once the scheme is registered it always authenticates.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Subject (<c>sub</c>) of the dev principal.</summary>
    public string Subject { get; set; } = "dev-user";

    /// <summary>Tenant of the dev principal (must match the portal's data tenant, e.g. <c>northwind</c>).</summary>
    public string TenantId { get; set; } = ReportPrincipalMapper.DefaultTenantId;

    /// <summary>
    /// Space/comma/semicolon-delimited role names granted to the dev principal. Accepts Keycloak realm
    /// role names (<c>report-admin</c>/<c>report-author</c>/<c>report-viewer</c>) or built-in enum names
    /// (<c>Admin</c>/<c>Author</c>/<c>Viewer</c>). Defaults to full tenant admin.
    /// </summary>
    public string Roles { get; set; } = "report-admin";
}

/// <summary>
/// Authenticates every request as a fixed development principal. Enabled only when
/// <see cref="ReportServerDevAuthenticationOptions.Enabled"/> is set. The per-request
/// <see cref="ReportSecurityHeaders.TenantId"/>/<see cref="ReportSecurityHeaders.Roles"/> headers, when
/// present, override the configured tenant/roles so a caller can act in a specific tenant while still
/// passing the ASP.NET authorization gate.
/// </summary>
public sealed class ReportServerDevAuthenticationHandler : AuthenticationHandler<ReportServerDevAuthenticationOptions>
{
    /// <summary>Creates the dev authentication handler.</summary>
    public ReportServerDevAuthenticationHandler(
        IOptionsMonitor<ReportServerDevAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tenantId = Request.Headers.TryGetValue(ReportSecurityHeaders.TenantId, out var tenantHeader) &&
            !string.IsNullOrWhiteSpace(tenantHeader.ToString())
            ? tenantHeader.ToString()
            : Options.TenantId;
        var subject = Request.Headers.TryGetValue(ReportSecurityHeaders.UserId, out var userHeader) &&
            !string.IsNullOrWhiteSpace(userHeader.ToString())
            ? userHeader.ToString()
            : Options.Subject;
        var roles = Request.Headers.TryGetValue(ReportSecurityHeaders.Roles, out var rolesHeader) &&
            !string.IsNullOrWhiteSpace(rolesHeader.ToString())
            ? rolesHeader.ToString()
            : Options.Roles;

        var identity = new ClaimsIdentity("ReportServerDev");
        identity.AddClaim(new Claim("sub", subject));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject));
        identity.AddClaim(new Claim(ReportPrincipalMapper.TenantClaimType, tenantId));
        foreach (var role in roles.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            identity.AddClaim(new Claim("roles", role));
        }

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ReportServerAuthenticationDefaults.DevScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Extension that reports whether the development authentication scheme is enabled.</summary>
public static class ReportServerDevAuthentication
{
    /// <summary>Reads <c>Authentication:Dev:Enabled</c> from configuration.</summary>
    public static bool IsDevAuthenticationEnabled(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetValue($"{ReportServerDevAuthenticationOptions.SectionName}:Enabled", defaultValue: false);
    }
}

#pragma warning restore MA0048
