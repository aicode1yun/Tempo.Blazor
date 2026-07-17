namespace Tempo.Reporting.Abstractions.Auth;

/// <summary>
/// Payload of the Blazor host's same-origin <c>GET /auth/token</c> hand-out endpoint: a short-lived
/// access token plus its absolute expiry. The WebAssembly leg caches this in memory only and
/// re-fetches when it nears expiry. The refresh token is never part of this payload — it stays in
/// the server-side token store.
/// </summary>
public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
