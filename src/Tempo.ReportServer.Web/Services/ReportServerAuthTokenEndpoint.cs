using System.Security.Claims;
using Tempo.Reporting.Abstractions.Auth;

namespace Tempo.ReportServer.Web.Services;

/// <summary>
/// Maps the Blazor host's same-origin <c>GET /auth/token</c> hand-out endpoint used by the
/// WebAssembly leg (<c>WasmAccessTokenProvider</c>). It returns the signed-in user's short-lived
/// access token (refreshed if needed) from the server-side <see cref="ReportServerTokenIssuer"/>.
/// It is authenticated by the FE session cookie, NOT CORS-enabled, and is not a data proxy — the
/// refresh token never leaves the server.
/// </summary>
public static class ReportServerAuthTokenEndpoint
{
    /// <summary>Maps <c>GET /auth/token</c> with <c>RequireAuthorization()</c>.</summary>
    public static IEndpointConventionBuilder MapReportServerAuthTokenEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapGet("/auth/token", async (
            ClaimsPrincipal user,
            ReportServerTokenIssuer issuer,
            CancellationToken cancellationToken) =>
        {
            var subject = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Results.Unauthorized();
            }

            var tokens = await issuer.GetValidTokensAsync(subject, forceRefresh: false, cancellationToken)
                .ConfigureAwait(false);
            return tokens is null
                ? Results.Unauthorized()
                : Results.Ok(new AccessTokenResponse(tokens.AccessToken, tokens.ExpiresUtc));
        }).RequireAuthorization();
    }
}
