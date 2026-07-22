using System.Net;
using System.Net.Http.Headers;

namespace Tempo.Reporting.Abstractions.Auth;

/// <summary>
/// Base class for typed HTTP clients that call the Tempo Report Server API from either Blazor
/// runtime. It attaches the bearer token <b>per request</b>, read from the consuming scope's
/// <see cref="IAccessTokenProvider"/>, and performs the standard
/// <c>401 → force-refresh → retry once</c> step.
/// </summary>
/// <remarks>
/// The token is deliberately attached here — inside the client that is constructed in the calling
/// scope (per circuit on the server, app scope in WASM) — and <b>never</b> via a
/// <see cref="DelegatingHandler"/> registered with <c>AddHttpMessageHandler</c>. On the server,
/// <see cref="IHttpClientFactory"/> builds and caches message handlers in their own DI scope, so a
/// handler would not see the circuit's scoped services and could reuse one user's token for another
/// (a cross-user data leak). Per-request attachment from the scoped provider avoids that trap while
/// working identically in both runtimes.
/// </remarks>
public abstract class ApiClientBase
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider? _accessTokenProvider;

    /// <summary>Creates the base client.</summary>
    /// <param name="httpClient">Transport whose base address points at the API host.</param>
    /// <param name="accessTokenProvider">
    /// Scoped token source; <see langword="null"/> sends requests anonymously (e.g. the
    /// self-contained demo or API-key transports that carry their own credentials).
    /// </param>
    protected ApiClientBase(HttpClient httpClient, IAccessTokenProvider? accessTokenProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _accessTokenProvider = accessTokenProvider;
    }

    /// <summary>The underlying transport (base address already configured).</summary>
    protected HttpClient HttpClient => _httpClient;

    /// <summary>
    /// Sends the request produced by <paramref name="requestFactory"/> with a freshly attached bearer
    /// token. On a 401 the token is force-refreshed and the request is rebuilt and retried exactly
    /// once. A factory (not a single message) is required because an <see cref="HttpRequestMessage"/>
    /// cannot be resent.
    /// </summary>
    protected async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestFactory);

        var response = await SendWithTokenAsync(requestFactory(), forceRefresh: false, cancellationToken)
            .ConfigureAwait(false);

        if (_accessTokenProvider is not null && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            response = await SendWithTokenAsync(requestFactory(), forceRefresh: true, cancellationToken)
                .ConfigureAwait(false);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpRequestMessage request,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (_accessTokenProvider is not null && request.Headers.Authorization is null)
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(forceRefresh, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
