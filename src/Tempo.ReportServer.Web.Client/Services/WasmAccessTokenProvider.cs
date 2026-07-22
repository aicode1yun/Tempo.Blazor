using System.Net;
using System.Net.Http.Json;
using Tempo.Reporting.Abstractions.Auth;

namespace Tempo.ReportServer.Web.Services;

/// <summary>
/// The InteractiveWebAssembly leg of <see cref="IAccessTokenProvider"/>. Holds the access token
/// <b>in memory only</b> — never in local/session storage — and (re)fetches it from the Blazor
/// host's same-origin <c>GET /auth/token</c> endpoint, which the browser calls with the session
/// cookie attached automatically. A page refresh or a new tab simply re-fetches; the refresh token
/// never reaches the browser.
/// </summary>
public sealed class WasmAccessTokenProvider : IAccessTokenProvider
{
    /// <summary>Named <see cref="HttpClient"/> pointing at the Blazor host origin (same origin as the WASM bundle).</summary>
    public const string HostHttpClientName = "ReportServerHost";

    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    /// <summary>Creates the provider.</summary>
    public WasmAccessTokenProvider(IHttpClientFactory httpClientFactory)
        => _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

    /// <inheritdoc />
    public async ValueTask<string?> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && IsCurrent())
        {
            return _accessToken;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check under the lock: a concurrent caller may already have refreshed.
            if (!forceRefresh && IsCurrent())
            {
                return _accessToken;
            }

            var host = _httpClientFactory.CreateClient(HostHttpClientName);
            using var response = await host.GetAsync("auth/token", cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Not signed in — drop any stale token; the UI reflects it via deserialized auth state.
                _accessToken = null;
                _expiresAt = default;
                return null;
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content
                .ReadFromJsonAsync<AccessTokenResponse>(cancellationToken)
                .ConfigureAwait(false);
            _accessToken = payload?.AccessToken;
            _expiresAt = payload?.ExpiresAt ?? default;
            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsCurrent()
        => !string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _expiresAt - ExpirySkew;
}
