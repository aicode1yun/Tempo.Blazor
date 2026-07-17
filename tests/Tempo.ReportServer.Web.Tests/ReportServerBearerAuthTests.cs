using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tempo.Reporting.Abstractions.Auth;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Tests;

/// <summary>
/// Fáze 5 (step 2) — the IdP-agnostic bearer model shared by both InteractiveAuto runtimes:
/// the WebAssembly in-memory token provider (<see cref="WasmAccessTokenProvider"/>), the
/// same-origin <c>/auth/token</c> hand-out endpoint, and the per-request
/// <c>401 → force-refresh → retry</c> behaviour in <see cref="ApiClientBase"/>.
/// </summary>
public sealed class ReportServerBearerAuthTests
{
    // ---- WasmAccessTokenProvider: in-memory cache + refresh ---------------------------------

    [Fact]
    public async Task WasmProvider_CachesToken_WhileNotNearExpiry()
    {
        var count = 0;
        var handler = new StubHandler(_ =>
        {
            count++;
            return JsonOk(new AccessTokenResponse($"tok-{count}", DateTimeOffset.UtcNow.AddMinutes(5)));
        });
        var provider = CreateWasmProvider(handler);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        first.Should().Be("tok-1");
        second.Should().Be("tok-1", "a token still well inside its lifetime is served from memory");
        handler.CallCount.Should().Be(1, "the endpoint must not be hit again while the token is current");
    }

    [Fact]
    public async Task WasmProvider_ForceRefresh_BypassesCache()
    {
        var count = 0;
        var handler = new StubHandler(_ =>
        {
            count++;
            return JsonOk(new AccessTokenResponse($"tok-{count}", DateTimeOffset.UtcNow.AddMinutes(5)));
        });
        var provider = CreateWasmProvider(handler);

        var first = await provider.GetAccessTokenAsync();
        var refreshed = await provider.GetAccessTokenAsync(forceRefresh: true);

        first.Should().Be("tok-1");
        refreshed.Should().Be("tok-2");
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task WasmProvider_RefetchesToken_WhenNearExpiry()
    {
        var count = 0;
        var handler = new StubHandler(_ =>
        {
            count++;
            // 10s lifetime is inside the 30s skew, so the next call is forced to re-fetch.
            return JsonOk(new AccessTokenResponse($"tok-{count}", DateTimeOffset.UtcNow.AddSeconds(10)));
        });
        var provider = CreateWasmProvider(handler);

        _ = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        second.Should().Be("tok-2");
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task WasmProvider_ReturnsNull_OnUnauthorized()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var provider = CreateWasmProvider(handler);

        var token = await provider.GetAccessTokenAsync();

        token.Should().BeNull("an unauthenticated user has no access token");
        handler.CallCount.Should().Be(1);
    }

    // ---- ApiClientBase: bearer per request + 401 -> refresh -> retry once -------------------

    [Fact]
    public async Task ApiClientBase_Retries_AfterUnauthorized_WithRefreshedToken()
    {
        var handler = new StubHandler(request =>
            request.Headers.Authorization?.Parameter == "fresh-token"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var provider = new QueueTokenProvider();
        var client = new TestApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.local/") }, provider);

        using var response = await client.GetResourceAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(2, "the first 401 must trigger exactly one retry");
        handler.AuthHeaders.Should().Equal("stale-token", "fresh-token");
        provider.ForceRefreshCalls.Should().Equal(new[] { false, true }, "the retry must force a token refresh");
    }

    [Fact]
    public async Task ApiClientBase_DoesNotRetry_OnSuccess()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var provider = new QueueTokenProvider();
        var client = new TestApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.local/") }, provider);

        using var response = await client.GetResourceAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(1);
        provider.ForceRefreshCalls.Should().Equal(new[] { false });
    }

    [Fact]
    public async Task ApiClientBase_RetriesAtMostOnce_WhenStillUnauthorized()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var provider = new QueueTokenProvider();
        var client = new TestApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.local/") }, provider);

        using var response = await client.GetResourceAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        handler.CallCount.Should().Be(2, "the retry happens once and then the 401 is surfaced");
    }

    // ---- /auth/token hand-out endpoint -----------------------------------------------------

    [Fact]
    public async Task AuthTokenEndpoint_Returns401_WhenNotAuthenticated()
    {
        await using var host = await CreateAuthTokenHostAsync(_ => { });
        using var client = host.GetTestClient();

        using var response = await client.GetAsync("/auth/token");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthTokenEndpoint_Returns401_WhenAuthenticatedButNoStoredToken()
    {
        await using var host = await CreateAuthTokenHostAsync(_ => { });
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(TestSubHeader, "user-without-tokens");

        using var response = await client.GetAsync("/auth/token");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthTokenEndpoint_ReturnsAccessToken_ForAuthenticatedUser()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        await using var host = await CreateAuthTokenHostAsync(store =>
            store.Set("user-1", new ReportServerTokenSet("access-abc", "refresh-xyz", expiresAt)));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(TestSubHeader, "user-1");

        using var response = await client.GetAsync("/auth/token");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().Be("access-abc", "the hand-out returns the stored access token, never the refresh token");
    }

    [Fact]
    public async Task AuthTokenEndpoint_NeverExposesRefreshToken()
    {
        await using var host = await CreateAuthTokenHostAsync(store =>
            store.Set("user-1", new ReportServerTokenSet("access-abc", "refresh-secret", DateTimeOffset.UtcNow.AddMinutes(5))));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(TestSubHeader, "user-1");

        using var response = await client.GetAsync("/auth/token");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("refresh-secret", "the refresh token must stay server-side");
    }

    // ---- helpers ---------------------------------------------------------------------------

    private const string TestSubHeader = "X-Test-Sub";

    private static WasmAccessTokenProvider CreateWasmProvider(StubHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new WasmAccessTokenProvider(new StubHttpClientFactory(httpClient));
    }

    private static HttpResponseMessage JsonOk(AccessTokenResponse dto)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(dto) };

    private static async Task<WebApplication> CreateAuthTokenHostAsync(Action<IReportServerTokenStore> seed)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton(new ReportServerOidcOptions());
        builder.Services.AddSingleton<IReportServerTokenStore, MemoryCacheReportServerTokenStore>();
        builder.Services.AddSingleton<ReportServerTokenIssuer>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapReportServerAuthTokenEndpoint();

        await app.StartAsync();
        seed(app.Services.GetRequiredService<IReportServerTokenStore>());
        return app;
    }

    private sealed class TestApiClient : ApiClientBase
    {
        public TestApiClient(HttpClient httpClient, IAccessTokenProvider? provider)
            : base(httpClient, provider)
        {
        }

        public Task<HttpResponseMessage> GetResourceAsync(CancellationToken cancellationToken = default)
            => SendAsync(() => new HttpRequestMessage(HttpMethod.Get, "resource"), cancellationToken);
    }

    private sealed class QueueTokenProvider : IAccessTokenProvider
    {
        public List<bool> ForceRefreshCalls { get; } = [];

        public ValueTask<string?> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            ForceRefreshCalls.Add(forceRefresh);
            return ValueTask.FromResult<string?>(forceRefresh ? "fresh-token" : "stale-token");
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public int CallCount { get; private set; }

        public List<string?> AuthHeaders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            AuthHeaders.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(TestSubHeader, out var subject) || string.IsNullOrEmpty(subject))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim("sub", subject!)], "Test");
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
