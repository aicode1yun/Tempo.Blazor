using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

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

/// <summary>Resolves a valid access token for the current server-rendered user, refreshing as needed.</summary>
public interface IServerAccessTokenProvider
{
    /// <summary>Returns a non-expired access token for the current user, or null when unavailable.</summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IServerAccessTokenProvider"/> for the InteractiveServer host: reads the current user's
/// token set from the store and, when it is within the refresh window, exchanges the refresh token at
/// the Keycloak token endpoint (plain HTTP form post — no Keycloak-specific SDK).
/// </summary>
public sealed class ServerAccessTokenProvider : IServerAccessTokenProvider
{
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromSeconds(60);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IReportServerTokenStore _tokenStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ReportServerOidcOptions _options;

    /// <summary>Creates the provider.</summary>
    public ServerAccessTokenProvider(
        IHttpContextAccessor httpContextAccessor,
        IReportServerTokenStore tokenStore,
        IHttpClientFactory httpClientFactory,
        ReportServerOidcOptions options)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var subject = _httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
            ?? _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var tokens = _tokenStore.Get(subject);
        if (tokens is null)
        {
            return null;
        }

        if (tokens.ExpiresUtc - DateTimeOffset.UtcNow > RefreshWindow ||
            string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return tokens.AccessToken;
        }

        var refreshed = await RefreshAsync(tokens.RefreshToken, cancellationToken).ConfigureAwait(false);
        if (refreshed is null)
        {
            return tokens.AccessToken;
        }

        _tokenStore.Set(subject, refreshed);
        return refreshed.AccessToken;
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

/// <summary>Attaches the current user's access token (from <see cref="IServerAccessTokenProvider"/>) to outgoing API calls.</summary>
public sealed class ReportServerAccessTokenHandler : DelegatingHandler
{
    private readonly IServerAccessTokenProvider _accessTokenProvider;

    /// <summary>Creates the handler.</summary>
    public ReportServerAccessTokenHandler(IServerAccessTokenProvider accessTokenProvider)
        => _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Headers.Authorization is null)
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
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
    /// (<see cref="IReportServerTokenStore"/>, <see cref="IServerAccessTokenProvider"/>) when
    /// <c>Authentication:Oidc</c> is configured. Returns the bound options so the caller can decide
    /// whether to attach the bearer handler to the API client. No-op (returns a not-configured
    /// options instance) when the section is empty, so the self-contained demo keeps running.
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
        builder.Services.AddSingleton<IReportServerTokenStore, MemoryCacheReportServerTokenStore>();
        builder.Services.AddScoped<IServerAccessTokenProvider, ServerAccessTokenProvider>();
        builder.Services.AddTransient<ReportServerAccessTokenHandler>();

        if (!options.IsConfigured)
        {
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
        return options;
    }
}
