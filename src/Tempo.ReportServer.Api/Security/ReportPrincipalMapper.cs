using System.Security.Claims;
using System.Text.Json;

namespace Tempo.ReportServer.Api.Security;

/// <summary>
/// Maps a validated OIDC (Keycloak) <see cref="ClaimsPrincipal"/>, or a delimited role string,
/// onto a <see cref="ReportSecurityContext"/>. Keycloak realm/client roles are the
/// <em>capability ceiling</em>; per-folder ACLs (loaded by the resolver) refine access below it.
/// </summary>
public static class ReportPrincipalMapper
{
    /// <summary>Claim carrying the tenant identifier (Keycloak protocol mapper), with fallbacks.</summary>
    public const string TenantClaimType = "tenant_id";

    /// <summary>Tenant used when the principal carries no tenant claim.</summary>
    public const string DefaultTenantId = "default";

    private const string ApiClientId = "tempo-report-api";

    /// <summary>Builds a user security context from a validated bearer principal.</summary>
    public static ReportSecurityContext FromClaimsPrincipal(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var subject = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        var tenantId = principal.FindFirstValue(TenantClaimType);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = principal.FindFirstValue("tenant") ?? DefaultTenantId;
        }

        var roleNames = new List<string>();
        roleNames.AddRange(ReadRealmRoles(principal));
        roleNames.AddRange(ReadClientRoles(principal, ApiClientId));
        roleNames.AddRange(principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value));
        roleNames.AddRange(principal.FindAll("roles").Select(claim => claim.Value));

        return ReportSecurityContext.ForUser(tenantId, subject, MapRoles(roleNames));
    }

    /// <summary>Parses a delimited list of role names (Keycloak names or built-in enum names).</summary>
    public static IReadOnlyList<ReportServerRole> ParseRoleNames(string value)
        => MapRoles((value ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IReadOnlyList<ReportServerRole> MapRoles(IEnumerable<string> roleNames)
        => roleNames
            .Select(MapRole)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .Distinct()
            .ToArray();

    private static ReportServerRole? MapRole(string roleName)
        => roleName?.Trim().ToLowerInvariant() switch
        {
            // Keycloak realm roles (capability ceiling).
            "report-admin" => ReportServerRole.TenantAdmin,
            "report-author" => ReportServerRole.Author,
            "report-viewer" => ReportServerRole.Viewer,
            // Keycloak client role granted to the machine-to-machine service account.
            "report.render" => ReportServerRole.Viewer,
            // Built-in enum names (dev header path / tests).
            "tenantadmin" => ReportServerRole.TenantAdmin,
            "admin" => ReportServerRole.TenantAdmin,
            "author" => ReportServerRole.Author,
            "viewer" => ReportServerRole.Viewer,
            _ => null,
        };

    private static IEnumerable<string> ReadRealmRoles(ClaimsPrincipal principal)
        => ReadRolesFromJson(principal.FindFirstValue("realm_access"));

    private static IEnumerable<string> ReadClientRoles(ClaimsPrincipal principal, string clientId)
    {
        var resourceAccess = principal.FindFirstValue("resource_access");
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
