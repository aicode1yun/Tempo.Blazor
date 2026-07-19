using System.Net;
using System.Net.Sockets;

namespace Tempo.ReportServer.Api.Scheduling;

/// <summary>
/// Options for outbound webhook delivery. Constrains which URLs a tenant-supplied schedule may POST
/// to, closing the server-side request forgery (SSRF) hole where a schedule target points at an
/// internal service or a cloud metadata endpoint.
/// </summary>
public sealed record ScheduledReportWebhookOptions
{
    /// <summary>URL schemes a webhook target may use. Defaults to HTTPS only.</summary>
    public IReadOnlyList<string> AllowedSchemes { get; init; } = ["https"];

    /// <summary>
    /// When <see langword="false"/> (the default) a webhook target that resolves to a loopback,
    /// link-local, or RFC 1918 / unique-local private address is rejected. Set to <see langword="true"/>
    /// only for trusted on-premise deployments that intentionally deliver to internal hosts.
    /// </summary>
    public bool AllowPrivateNetworks { get; init; }

    /// <summary>Per-request HTTP timeout applied to the webhook client.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Validates a webhook delivery target against <see cref="ScheduledReportWebhookOptions"/>. Rejects
/// disallowed schemes and, unless private networks are explicitly permitted, any target whose host
/// resolves to a loopback, link-local (including the 169.254.169.254 cloud metadata address),
/// private, or unique-local address.
/// </summary>
public static class ScheduledReportWebhookGuard
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when <paramref name="target"/> is not an absolute
    /// URL, uses a disallowed scheme, or (unless private networks are allowed) resolves to a private,
    /// loopback, or link-local address. Returns the validated <see cref="Uri"/> on success.
    /// </summary>
    /// <param name="hostResolver">
    /// Resolves a DNS host name to its addresses. Defaults to <see cref="Dns.GetHostAddresses(string)"/>;
    /// overridable so the private-address rejection can be unit tested deterministically.
    /// </param>
    public static Uri Validate(
        string target,
        ScheduledReportWebhookOptions options,
        Func<string, IReadOnlyList<IPAddress>>? hostResolver = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Webhook delivery target '{target}' is not an absolute URL.");
        }

        if (!options.AllowedSchemes.Any(scheme => string.Equals(scheme, uri.Scheme, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Webhook delivery scheme '{uri.Scheme}' is not allowed. Permitted schemes: {string.Join(", ", options.AllowedSchemes)}.");
        }

        if (options.AllowPrivateNetworks)
        {
            return uri;
        }

        foreach (var address in ResolveAddresses(uri, hostResolver))
        {
            if (IsBlockedAddress(address))
            {
                throw new InvalidOperationException(
                    $"Webhook delivery target '{target}' resolves to the non-public address {address} and is blocked.");
            }
        }

        return uri;
    }

    private static IReadOnlyList<IPAddress> ResolveAddresses(Uri uri, Func<string, IReadOnlyList<IPAddress>>? hostResolver)
    {
        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            return [literal];
        }

        var resolver = hostResolver ?? (host => Dns.GetHostAddresses(host));
        var resolved = resolver(uri.Host);
        if (resolved.Count == 0)
        {
            throw new InvalidOperationException($"Webhook delivery host '{uri.Host}' could not be resolved to an address.");
        }

        return resolved;
    }

    /// <summary>True when the address is loopback, link-local, private, or otherwise non-public.</summary>
    public static bool IsBlockedAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsBlockedIPv4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsBlockedIPv6(address),
            _ => true,
        };
    }

    private static bool IsBlockedIPv4(byte[] bytes)
    {
        // 0.0.0.0/8 "this host" / unspecified (0.0.0.0 also caught by IsBlockedAddress).
        if (bytes[0] == 0)
        {
            return true;
        }

        // 10.0.0.0/8
        if (bytes[0] == 10)
        {
            return true;
        }

        // 172.16.0.0/12
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        // 169.254.0.0/16 link-local (covers the 169.254.169.254 cloud metadata endpoint)
        if (bytes[0] == 169 && bytes[1] == 254)
        {
            return true;
        }

        // 127.0.0.0/8 loopback (also caught by IPAddress.IsLoopback) and 100.64.0.0/10 CGNAT.
        if (bytes[0] == 127)
        {
            return true;
        }

        // 224.0.0.0/4 multicast and 240.0.0.0/4 reserved/experimental (covers 255.255.255.255 broadcast).
        if (bytes[0] >= 224)
        {
            return true;
        }

        return bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127;
    }

    private static bool IsBlockedIPv6(IPAddress address)
    {
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.IsIPv6Multicast)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();

        // fc00::/7 unique local (IsIPv6UniqueLocal already covers this on modern runtimes; kept explicit).
        if ((bytes[0] & 0xFE) == 0xFC)
        {
            return true;
        }

        // 64:ff9b::/96 NAT64 well-known prefix: an embedded IPv4 (bytes 12..15) can smuggle a private
        // or metadata target past an IPv4-only check, so block the whole prefix.
        ReadOnlySpan<byte> nat64Prefix = [0x00, 0x64, 0xFF, 0x9B, 0, 0, 0, 0, 0, 0, 0, 0];
        return bytes.AsSpan(0, 12).SequenceEqual(nat64Prefix);
    }
}
