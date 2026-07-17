#pragma warning disable MA0048

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tempo.ReportServer.Api.Security;

/// <summary>Constants for the report server authentication schemes and authorization policy.</summary>
public static class ReportServerAuthenticationDefaults
{
    /// <summary>Combined policy scheme that forwards to API key or bearer based on the request.</summary>
    public const string CombinedScheme = "ReportServerCombined";

    /// <summary>Custom API key authentication scheme name.</summary>
    public const string ApiKeyScheme = "ReportServerApiKey";

    /// <summary>Authorization policy that accepts any supported authentication scheme.</summary>
    public const string ApiPolicy = "ReportServerApi";
}

/// <summary>Options for the report server API key authentication handler.</summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

/// <summary>
/// Authenticates a request that carries a Tempo Report Server API key in the
/// <see cref="ReportSecurityHeaders.ApiKey"/> header, validating it against the
/// registered <see cref="IReportApiKeyStore"/>.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    /// <summary>Creates the API key authentication handler.</summary>
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ReportSecurityHeaders.ApiKey, out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var store = Context.RequestServices.GetRequiredService<IReportApiKeyStore>();
        var descriptor = await store.ValidateAsync(apiKey, Context.RequestAborted).ConfigureAwait(false);
        if (descriptor is null)
        {
            return AuthenticateResult.Fail("The provided report server API key is invalid or revoked.");
        }

        var identity = new ClaimsIdentity(ReportServerAuthenticationDefaults.ApiKeyScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, $"api:{descriptor.ApplicationId}"));
        identity.AddClaim(new Claim("sub", $"api:{descriptor.ApplicationId}"));
        identity.AddClaim(new Claim("tenant_id", descriptor.TenantId));
        identity.AddClaim(new Claim("report_api_key_id", descriptor.KeyId));
        identity.AddClaim(new Claim("report_application_id", descriptor.ApplicationId));

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ReportServerAuthenticationDefaults.ApiKeyScheme);
        return AuthenticateResult.Success(ticket);
    }
}

/// <summary>Service extensions that compose report server authentication and authorization.</summary>
public static class ReportServerAuthenticationExtensions
{
    /// <summary>
    /// Adds report server authentication: a JWT bearer scheme (Keycloak, configured from
    /// <c>Authentication:Jwt</c>) plus a custom API key scheme, fronted by a combined policy scheme,
    /// and an authorization policy (<see cref="ReportServerAuthenticationDefaults.ApiPolicy"/>) that
    /// accepts any authenticated scheme.
    /// </summary>
    public static IServiceCollection AddReportServerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var jwtSection = configuration.GetSection("Authentication:Jwt");
        var authority = jwtSection["Authority"];
        var audience = jwtSection["Audience"];
        var requireHttpsMetadata = jwtSection.GetValue("RequireHttpsMetadata", defaultValue: true);

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = ReportServerAuthenticationDefaults.CombinedScheme;
                options.DefaultChallengeScheme = ReportServerAuthenticationDefaults.CombinedScheme;
            })
            .AddPolicyScheme(
                ReportServerAuthenticationDefaults.CombinedScheme,
                ReportServerAuthenticationDefaults.CombinedScheme,
                options => options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(ReportSecurityHeaders.ApiKey)
                        ? ReportServerAuthenticationDefaults.ApiKeyScheme
                        : JwtBearerDefaults.AuthenticationScheme)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ReportServerAuthenticationDefaults.ApiKeyScheme,
                _ => { })
            .AddJwtBearer(options =>
            {
                if (!string.IsNullOrWhiteSpace(authority))
                {
                    options.Authority = authority;
                }

                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.TokenValidationParameters.ValidateAudience = !string.IsNullOrWhiteSpace(audience);
                if (!string.IsNullOrWhiteSpace(audience))
                {
                    options.Audience = audience;
                }
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                ReportServerAuthenticationDefaults.ApiPolicy,
                policy => policy
                    .AddAuthenticationSchemes(
                        ReportServerAuthenticationDefaults.ApiKeyScheme,
                        JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser());
        });

        return services;
    }
}

#pragma warning restore MA0048
