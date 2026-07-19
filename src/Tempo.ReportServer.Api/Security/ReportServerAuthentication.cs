#pragma warning disable MA0048

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    /// <summary>
    /// Development-only authentication scheme name. Active only when <c>Authentication:Dev:Enabled=true</c>;
    /// used by the Keycloak-free CI/E2E lane. Never enabled in production.
    /// </summary>
    public const string DevScheme = "ReportServerDev";

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
    /// <param name="environment">
    /// The host environment. When it is Production, the development authentication bypass is forced OFF
    /// even if <c>Authentication:Dev:Enabled=true</c> — one stray env var must never disable real auth in
    /// production. Pass <see langword="null"/> (the default, used by in-process tests) to skip the
    /// production guard.
    /// </param>
    public static IServiceCollection AddReportServerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var jwtSection = configuration.GetSection("Authentication:Jwt");
        var authority = jwtSection["Authority"];
        var audience = jwtSection["Audience"];
        var requireHttpsMetadata = jwtSection.GetValue("RequireHttpsMetadata", defaultValue: true);

        // Development-only: a Keycloak-free scheme that authenticates every request as a fixed dev
        // principal so the OIDC-off portal can exercise the real Api in CI/E2E. Strictly opt-in; when
        // disabled the schemes and policy below are exactly the production (JWT + API key) configuration.
        var devEnabled = configuration.IsDevAuthenticationEnabled();

        // Defense in depth: the dev bypass must never activate in Production, regardless of config. If the
        // flag was set but suppressed here, log a WARNING (do not throw — the host stays up on real auth).
        if (devEnabled && environment is not null && environment.IsProduction())
        {
            devEnabled = false;
            using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
            bootstrapLoggerFactory
                .CreateLogger("Tempo.ReportServer.Api.Security.ReportServerDevAuthentication")
                .LogWarning(
                    "Authentication:Dev:Enabled is set but was SUPPRESSED because the host environment is " +
                    "Production. The report server API is using real authentication (JWT bearer + API key). " +
                    "The development authentication bypass must never run in Production.");
        }

        var authenticationBuilder = services
            .AddAuthentication(options =>
            {
                var defaultScheme = devEnabled
                    ? ReportServerAuthenticationDefaults.DevScheme
                    : ReportServerAuthenticationDefaults.CombinedScheme;
                options.DefaultScheme = defaultScheme;
                options.DefaultChallengeScheme = defaultScheme;
            });

        if (devEnabled)
        {
            // Bind the options for THIS scheme name — the handler resolves its options via
            // IOptionsMonitor.Get(DevScheme), so an unnamed services.Configure would not reach it and the
            // tenant/roles would silently fall back to the class defaults.
            authenticationBuilder.AddScheme<ReportServerDevAuthenticationOptions, ReportServerDevAuthenticationHandler>(
                ReportServerAuthenticationDefaults.DevScheme,
                options => configuration.GetSection(ReportServerDevAuthenticationOptions.SectionName).Bind(options));
        }

        authenticationBuilder
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

                // JIT provisioning: upsert the local user projection on every validated token.
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        if (principal?.Identity?.IsAuthenticated != true)
                        {
                            return;
                        }

                        var subject = principal.FindFirst("sub")?.Value
                            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (string.IsNullOrWhiteSpace(subject))
                        {
                            return;
                        }

                        var securityContext = ReportPrincipalMapper.FromClaimsPrincipal(principal);
                        var provisioner = context.HttpContext.RequestServices
                            .GetRequiredService<IReportServerUserProvisioner>();
                        try
                        {
                            await provisioner.UpsertAsync(
                                subject,
                                securityContext.TenantId,
                                principal.FindFirst("email")?.Value,
                                principal.FindFirst("preferred_username")?.Value ?? principal.FindFirst("name")?.Value,
                                context.HttpContext.RequestAborted).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // JIT provisioning is fail-open: a transient database outage or a primary-key
                            // race between concurrent first logins of the same subject must not turn a valid
                            // token into a failed request. Authorization still relies on the validated token,
                            // not on the local user projection, so skipping the upsert is safe. The next
                            // authenticated request re-attempts the upsert.
                            context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("Tempo.ReportServer.Api.Security.JitProvisioning")
                                .LogWarning(
                                    ex,
                                    "Just-in-time user provisioning failed for subject {Subject}; continuing with the validated token.",
                                    subject);
                        }
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            string[] policySchemes = devEnabled
                ? [ReportServerAuthenticationDefaults.DevScheme]
                : [ReportServerAuthenticationDefaults.ApiKeyScheme, JwtBearerDefaults.AuthenticationScheme];
            options.AddPolicy(
                ReportServerAuthenticationDefaults.ApiPolicy,
                policy => policy
                    .AddAuthenticationSchemes(policySchemes)
                    .RequireAuthenticatedUser());
        });

        return services;
    }
}

#pragma warning restore MA0048
