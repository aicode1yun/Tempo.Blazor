using System.Security.Claims;
using System.Text.Json;

namespace Tempo.ReportServer.Web.Services;

/// <summary>
/// Portal-side role used for <em>UI gating only</em>. The server API remains the security authority
/// (it maps the same Keycloak roles onto its own <c>ReportServerRole</c> and enforces per-folder ACLs);
/// this enum lets the shell hide pages/actions the signed-in user cannot use. Kept independent of the
/// server security types so the WebAssembly leg needs no reference to the API host project.
/// </summary>
public enum PortalRole
{
    /// <summary>Can view and render reports.</summary>
    Viewer = 0,

    /// <summary>Can author reports, schedules and revisions.</summary>
    Author = 1,

    /// <summary>Tenant administrator: data sources, permissions and API keys.</summary>
    Admin = 2,
}

/// <summary>
/// Reads identity, tenant and roles from a signed-in Keycloak <see cref="ClaimsPrincipal"/> using the
/// same claim shapes and role names as the server's principal mapper, so UI and backend agree on the
/// tenant/role ceiling.
/// </summary>
public static class PortalClaims
{
    /// <summary>Claim carrying the tenant identifier (Keycloak protocol mapper), with fallbacks.</summary>
    public const string TenantClaimType = "tenant_id";

    /// <summary>Tenant used when the principal carries no tenant claim.</summary>
    public const string DefaultTenantId = "default";

    private const string ApiClientId = "tempo-report-api";

    /// <summary>Resolves the tenant id from the principal, falling back to <see cref="DefaultTenantId"/>.</summary>
    public static string ReadTenant(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var tenantId = principal.FindFirst(TenantClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = principal.FindFirst("tenant")?.Value;
        }

        return string.IsNullOrWhiteSpace(tenantId) ? DefaultTenantId : tenantId;
    }

    /// <summary>Resolves a human display name (preferred_username → name → email → sub → "User").</summary>
    public static string ReadDisplayName(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? principal.FindFirst("sub")?.Value
            ?? "User";
    }

    /// <summary>Maps the principal's Keycloak realm/client/role claims onto distinct <see cref="PortalRole"/>s.</summary>
    public static IReadOnlyList<PortalRole> ReadRoles(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var names = new List<string>();
        names.AddRange(ReadRolesFromJson(principal.FindFirst("realm_access")?.Value));
        names.AddRange(ReadClientRoles(principal.FindFirst("resource_access")?.Value, ApiClientId));
        names.AddRange(principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value));
        names.AddRange(principal.FindAll("roles").Select(claim => claim.Value));

        return names
            .Select(MapRole)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .Distinct()
            .ToArray();
    }

    /// <summary>Whether <paramref name="roles"/> reaches at least <paramref name="minimumRole"/> by rank.</summary>
    public static bool Reaches(IReadOnlyList<PortalRole> roles, PortalRole minimumRole)
    {
        ArgumentNullException.ThrowIfNull(roles);
        for (var i = 0; i < roles.Count; i++)
        {
            if (roles[i] >= minimumRole)
            {
                return true;
            }
        }

        return false;
    }

    private static PortalRole? MapRole(string roleName)
        => roleName?.Trim().ToLowerInvariant() switch
        {
            "report-admin" => PortalRole.Admin,
            "report-author" => PortalRole.Author,
            "report-viewer" => PortalRole.Viewer,
            "report.render" => PortalRole.Viewer,
            "tenantadmin" => PortalRole.Admin,
            "admin" => PortalRole.Admin,
            "author" => PortalRole.Author,
            "viewer" => PortalRole.Viewer,
            _ => null,
        };

    private static IEnumerable<string> ReadClientRoles(string? resourceAccess, string clientId)
    {
        if (string.IsNullOrWhiteSpace(resourceAccess))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(resourceAccess);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(clientId, out var client) &&
                client.TryGetProperty("roles", out var roles) &&
                roles.ValueKind == JsonValueKind.Array)
            {
                return roles.EnumerateArray()
                    .Where(role => role.ValueKind == JsonValueKind.String)
                    .Select(role => role.GetString() ?? string.Empty)
                    .ToArray();
            }
        }
        catch (JsonException)
        {
            // Malformed claim: treat as no client roles.
        }

        return [];
    }

    private static IEnumerable<string> ReadRolesFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("roles", out var roles) &&
                roles.ValueKind == JsonValueKind.Array)
            {
                return roles.EnumerateArray()
                    .Where(role => role.ValueKind == JsonValueKind.String)
                    .Select(role => role.GetString() ?? string.Empty)
                    .ToArray();
            }
        }
        catch (JsonException)
        {
            // Malformed claim: treat as no roles.
        }

        return [];
    }
}
