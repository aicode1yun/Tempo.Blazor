using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Tempo.Reporting.Abstractions.Auth;

namespace Tempo.ReportServer.Web.Services;

/// <summary>
/// Server-side, per-user access/refresh token store. A confidential (BFF) host keeps the tokens
/// out of the browser: the cookie only carries identity, while the tokens live in server memory
/// keyed by the user's <c>sub</c>. Hydrated in <c>OnTokenValidated</c> and refreshed on demand.
/// </summary>
public interface IReportServerTokenStore
{
    /// <summary>Stores or replaces the token set for a subject.</summary>
    void Set(string subject, ReportServerTokenSet tokens);

    /// <summary>Returns the current token set for a subject, or null when unknown.</summary>
    ReportServerTokenSet? Get(string subject);

    /// <summary>Removes the token set for a subject (sign-out).</summary>
    void Remove(string subject);
}

/// <summary>A user's OIDC token set held server-side.</summary>
public sealed record ReportServerTokenSet(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresUtc);

/// <summary><see cref="IMemoryCache"/>-backed <see cref="IReportServerTokenStore"/>.</summary>
public sealed class MemoryCacheReportServerTokenStore : IReportServerTokenStore
{
    private const string KeyPrefix = "reportserver:tokens:";
    private readonly IMemoryCache _cache;

    /// <summary>Creates the store.</summary>
    public MemoryCacheReportServerTokenStore(IMemoryCache cache)
        => _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <inheritdoc />
    public void Set(string subject, ReportServerTokenSet tokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(tokens);
        // Keep the entry a little past token expiry so the refresh token stays available.
        _cache.Set(KeyPrefix + subject, tokens, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(8),
        });
    }

    /// <inheritdoc />
    public ReportServerTokenSet? Get(string subject)
        => string.IsNullOrWhiteSpace(subject) ? null : _cache.Get<ReportServerTokenSet>(KeyPrefix + subject);

    /// <inheritdoc />
    public void Remove(string subject)
    {
        if (!string.IsNullOrWhiteSpace(subject))
        {
            _cache.Remove(KeyPrefix + subject);
        }
    }
}

/// <summary>
/// <see cref="IDistributedCache"/>-backed <see cref="IReportServerTokenStore"/> for scale-out
/// (multi-instance) BFF deployments. Unlike <see cref="MemoryCacheReportServerTokenStore"/>, which is
/// process-local, this keeps the per-user token set in a shared cache (SQL Server via
/// <c>AddDistributedSqlServerCache</c>, Redis, etc.) so any host instance behind the load balancer can
/// resolve and refresh a user's tokens. The token set is stored as UTF-8 JSON.
/// </summary>
public sealed class DistributedCacheReportServerTokenStore : IReportServerTokenStore
{
    private const string KeyPrefix = "reportserver:tokens:";
    private static readonly DistributedCacheEntryOptions EntryOptions = new()
    {
        // Keep the entry a little past token expiry so the refresh token stays available.
        SlidingExpiration = TimeSpan.FromHours(8),
    };

    private readonly IDistributedCache _cache;

    /// <summary>Creates the store.</summary>
    public DistributedCacheReportServerTokenStore(IDistributedCache cache)
        => _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <inheritdoc />
    public void Set(string subject, ReportServerTokenSet tokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(tokens);
        _cache.Set(KeyPrefix + subject, JsonSerializer.SerializeToUtf8Bytes(tokens), EntryOptions);
    }

    /// <inheritdoc />
    public ReportServerTokenSet? Get(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var payload = _cache.Get(KeyPrefix + subject);
        return payload is null ? null : JsonSerializer.Deserialize<ReportServerTokenSet>(payload);
    }

    /// <inheritdoc />
    public void Remove(string subject)
    {
        if (!string.IsNullOrWhiteSpace(subject))
        {
            _cache.Remove(KeyPrefix + subject);
        }
    }
}

/// <summary>
/// Resolves a valid access token for a known subject, refreshing it at the Keycloak token endpoint
/// (plain HTTP form post — no Keycloak-specific SDK) when it is within the refresh window or a
/// refresh is forced. Singleton and keyed only by subject, so it is safe to share between the
/// circuit-scoped <see cref="ServerAccessTokenProvider"/> and the <c>/auth/token</c> hand-out
/// endpoint without any per-request/per-circuit state.
/// </summary>
public sealed class ReportServerTokenIssuer
{
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromSeconds(60);

    private readonly IReportServerTokenStore _tokenStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ReportServerOidcOptions _options;

    /// <summary>Creates the issuer.</summary>
    public ReportServerTokenIssuer(
        IReportServerTokenStore tokenStore,
        IHttpClientFactory httpClientFactory,
        ReportServerOidcOptions options)
    {
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Returns the current (refreshed if needed) token set for <paramref name="subject"/>, or
    /// <see langword="null"/> when the subject has no stored tokens.
    /// </summary>
    public async Task<ReportServerTokenSet?> GetValidTokensAsync(
        string subject,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var tokens = _tokenStore.Get(subject);
        if (tokens is null)
        {
            return null;
        }

        var needsRefresh = forceRefresh || tokens.ExpiresUtc - DateTimeOffset.UtcNow <= RefreshWindow;
        if (!needsRefresh || string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return tokens;
        }

        var refreshed = await RefreshAsync(tokens.RefreshToken, cancellationToken).ConfigureAwait(false);
        if (refreshed is null)
        {
            return tokens;
        }

        _tokenStore.Set(subject, refreshed);
        return refreshed;
    }

    private async Task<ReportServerTokenSet?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint())
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["refresh_token"] = refreshToken,
            }),
        };

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (!root.TryGetProperty("access_token", out var accessTokenElement))
        {
            return null;
        }

        var expiresIn = root.TryGetProperty("expires_in", out var expiresElement) && expiresElement.TryGetInt32(out var seconds)
            ? seconds
            : 300;
        var newRefresh = root.TryGetProperty("refresh_token", out var refreshElement)
            ? refreshElement.GetString()
            : refreshToken;

        return new ReportServerTokenSet(
            accessTokenElement.GetString() ?? string.Empty,
            newRefresh,
            DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }
}

/// <summary>
/// The InteractiveServer (circuit) leg of <see cref="IAccessTokenProvider"/>. Resolves the subject
/// from the circuit's <see cref="AuthenticationStateProvider"/> — never <see cref="IHttpContextAccessor"/>,
/// which is not reliably available on a live circuit — and pulls a valid token from the server-side
/// store via <see cref="ReportServerTokenIssuer"/>. Scoped, so it always reads the current circuit's
/// user and can never leak another user's token.
/// </summary>
public sealed class ServerAccessTokenProvider : IAccessTokenProvider
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ReportServerTokenIssuer _tokenIssuer;

    /// <summary>Creates the provider.</summary>
    public ServerAccessTokenProvider(
        AuthenticationStateProvider authenticationStateProvider,
        ReportServerTokenIssuer tokenIssuer)
    {
        _authenticationStateProvider = authenticationStateProvider ?? throw new ArgumentNullException(nameof(authenticationStateProvider));
        _tokenIssuer = tokenIssuer ?? throw new ArgumentNullException(nameof(tokenIssuer));
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
        var subject = state.User.FindFirstValue("sub") ?? state.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var tokens = await _tokenIssuer.GetValidTokensAsync(subject, forceRefresh, cancellationToken).ConfigureAwait(false);
        return tokens?.AccessToken;
    }
}

/// <summary>Bound OIDC options for the report server web (BFF) host, read from <c>Authentication:Oidc</c>.</summary>
public sealed class ReportServerOidcOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Authentication:Oidc";

    /// <summary>Keycloak realm authority (issuer), e.g. <c>http://localhost:8080/realms/tempo-reports</c>.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>Confidential client id (<c>tempo-report-web</c>).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Confidential client secret (from user-secrets / environment; never committed).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Whether HTTPS metadata is required (false for local dev over http).</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Server-side token store backing: <c>Memory</c> (default, single-host process-local
    /// <see cref="IMemoryCache"/>) or <c>Distributed</c> (scale-out, shared <see cref="IDistributedCache"/>).
    /// A <c>Distributed</c> host must register a real distributed cache (e.g. <c>AddDistributedSqlServerCache</c>);
    /// when none is registered a dev-only in-memory distributed cache is used as a fallback.
    /// </summary>
    public string TokenStore { get; set; } = "Memory";

    /// <summary>Additional scopes to request beyond openid/profile/email.</summary>
    public IList<string> Scopes { get; set; } = new List<string>();

    /// <summary>True when a usable authority + client id are configured.</summary>
    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>Resolves the realm token endpoint.</summary>
    public string TokenEndpoint()
        => $"{Authority.TrimEnd('/')}/protocol/openid-connect/token";
}

/// <summary>DI wiring for report server web (BFF) authentication.</summary>
public static class ReportServerWebAuthenticationExtensions
{
    /// <summary>
    /// Adds cookie + OpenID Connect (Keycloak) authentication and the server-side token plumbing
    /// (<see cref="IReportServerTokenStore"/>, <see cref="ReportServerTokenIssuer"/>, and the
    /// InteractiveServer leg of <see cref="IAccessTokenProvider"/>) when <c>Authentication:Oidc</c>
    /// is configured. Returns the bound options so the caller can decide how to wire the API client.
    /// No-op (returns a not-configured options instance) when the section is empty, so the
    /// self-contained demo keeps running.
    /// </summary>
    public static ReportServerOidcOptions AddReportServerWebAuthentication(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new ReportServerOidcOptions();
        builder.Configuration.GetSection(ReportServerOidcOptions.SectionName).Bind(options);
        builder.Services.AddSingleton(options);
        builder.Services.AddMemoryCache();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient();
        if (string.Equals(options.TokenStore, "Distributed", StringComparison.OrdinalIgnoreCase))
        {
            // Consume whatever IDistributedCache the host registered (SQL Server, Redis, ...).
            // AddDistributedMemoryCache uses TryAdd, so a real distributed cache registered earlier wins;
            // it only supplies a dev-only fallback so the store always resolves.
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSingleton<IReportServerTokenStore, DistributedCacheReportServerTokenStore>();
        }
        else
        {
            builder.Services.AddSingleton<IReportServerTokenStore, MemoryCacheReportServerTokenStore>();
        }

        builder.Services.AddSingleton<ReportServerTokenIssuer>();

        if (!options.IsConfigured)
        {
            // Demo mode: no auth wiring (and thus no AuthenticationStateProvider). The typed API
            // client resolves IAccessTokenProvider as null and calls anonymously.
            return options;
        }

        builder.Services
            .AddAuthentication(authOptions =>
            {
                authOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, oidc =>
            {
                oidc.Authority = options.Authority;
                oidc.ClientId = options.ClientId;
                oidc.ClientSecret = options.ClientSecret;
                oidc.RequireHttpsMetadata = options.RequireHttpsMetadata;
                oidc.ResponseType = "code";
                oidc.UsePkce = true;
                oidc.SaveTokens = false; // tokens are kept server-side in IReportServerTokenStore
                oidc.GetClaimsFromUserInfoEndpoint = true;
                oidc.MapInboundClaims = false;
                oidc.Scope.Clear();
                oidc.Scope.Add("openid");
                oidc.Scope.Add("profile");
                oidc.Scope.Add("email");
                foreach (var scope in options.Scopes)
                {
                    if (!string.IsNullOrWhiteSpace(scope))
                    {
                        oidc.Scope.Add(scope);
                    }
                }

                oidc.TokenValidationParameters.NameClaimType = "preferred_username";
                oidc.TokenValidationParameters.RoleClaimType = "roles";

                oidc.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = context =>
                    {
                        var response = context.TokenEndpointResponse;
                        var subject = context.Principal?.FindFirstValue("sub");
                        if (response is not null && !string.IsNullOrWhiteSpace(subject))
                        {
                            var expiresIn = int.TryParse(response.ExpiresIn, out var seconds) ? seconds : 300;
                            var store = context.HttpContext.RequestServices.GetRequiredService<IReportServerTokenStore>();
                            store.Set(subject, new ReportServerTokenSet(
                                response.AccessToken ?? string.Empty,
                                response.RefreshToken,
                                DateTimeOffset.UtcNow.AddSeconds(expiresIn)));
                        }

                        return Task.CompletedTask;
                    },
                    OnSignedOutCallbackRedirect = context =>
                    {
                        var subject = context.HttpContext.User.FindFirstValue("sub");
                        if (!string.IsNullOrWhiteSpace(subject))
                        {
                            context.HttpContext.RequestServices
                                .GetRequiredService<IReportServerTokenStore>()
                                .Remove(subject);
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        // Circuit-scoped: the InteractiveServer leg of IAccessTokenProvider. Registered only here,
        // where AuthenticationStateProvider (its dependency) exists. The typed API client attaches
        // this token per request (see ApiClientBase); never via a server-side DelegatingHandler,
        // whose cached factory scope can leak tokens across users.
        builder.Services.AddScoped<IAccessTokenProvider, ServerAccessTokenProvider>();
        return options;
    }
}
