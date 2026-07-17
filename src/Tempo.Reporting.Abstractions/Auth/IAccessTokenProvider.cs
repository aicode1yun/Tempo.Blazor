namespace Tempo.Reporting.Abstractions.Auth;

/// <summary>
/// Supplies the current user's API access token in whichever runtime the calling component
/// happens to execute (InteractiveServer circuit or InteractiveWebAssembly). Implemented once
/// per host — <c>ServerAccessTokenProvider</c> on the Blazor host and
/// <c>WasmAccessTokenProvider</c> in the WebAssembly leg — and consumed behind this single
/// abstraction so typed API clients never know which runtime produced the token. This is the
/// IdP-agnostic seam: how the token is obtained at login is app-specific; everything downstream
/// only depends on this contract.
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Returns a non-expired access token for the current user, or <see langword="null"/> when the
    /// user is not authenticated / no token is available. Pass <paramref name="forceRefresh"/> to
    /// bypass any cached token (used by the 401 → refresh → retry step in <see cref="ApiClientBase"/>).
    /// </summary>
    ValueTask<string?> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}
