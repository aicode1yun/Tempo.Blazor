using System.Net;
using System.Net.Sockets;

namespace Tempo.ReportServer.Api.Scheduling;

/// <summary>
/// TOCTOU-safe connect primitive for outbound webhook delivery. The <see cref="ScheduledReportWebhookGuard"/>
/// validates the resolved addresses of a target before the request, but a stock <see cref="HttpClient"/>
/// re-resolves the host when it opens the socket — a DNS-rebinding attacker can answer the guard with a
/// public address and the connect with a private one (169.254.169.254, 10.x, loopback). This connector
/// resolves the host <b>once</b>, validates <b>every</b> returned address, and then pins the socket to one
/// of those exact validated addresses, so no second lookup can smuggle in a rebound private target.
/// </summary>
public static class ScheduledReportWebhookConnector
{
    /// <summary>Opens a stream to a concrete IP endpoint. Abstracted so the pinning logic is unit-testable.</summary>
    public delegate ValueTask<Stream> SocketConnector(IPEndPoint endpoint, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves <paramref name="host"/> once, rejects the connection if <b>any</b> resolved address is
    /// non-public (unless <see cref="ScheduledReportWebhookOptions.AllowPrivateNetworks"/> is set), then
    /// connects <paramref name="socketConnector"/> to the first validated address that accepts. The socket
    /// is pinned to a validated address; the host is never re-resolved after validation.
    /// </summary>
    /// <param name="host">The target host (a DNS name or an IP literal).</param>
    /// <param name="port">The target port.</param>
    /// <param name="options">Webhook policy controlling whether private targets are permitted.</param>
    /// <param name="resolver">Resolves a DNS name to addresses. Injected so rebinding can be tested deterministically.</param>
    /// <param name="socketConnector">Connects to a concrete endpoint. Injected so the pin can be asserted without real sockets.</param>
    /// <param name="cancellationToken">Cancellation for the resolve/connect.</param>
    public static async ValueTask<Stream> ConnectValidatedAsync(
        string host,
        int port,
        ScheduledReportWebhookOptions options,
        Func<string, IReadOnlyList<IPAddress>> resolver,
        SocketConnector socketConnector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(socketConnector);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        IReadOnlyList<IPAddress> addresses;
        if (IPAddress.TryParse(host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            addresses = resolver(host);
            if (addresses.Count == 0)
            {
                throw new InvalidOperationException($"Webhook delivery host '{host}' could not be resolved to an address.");
            }
        }

        // Validate EVERY resolved address before touching a socket: a host that mixes a public and a
        // private answer is rejected outright, never partially connected. This is the anti-rebinding gate.
        if (!options.AllowPrivateNetworks)
        {
            foreach (var address in addresses)
            {
                if (ScheduledReportWebhookGuard.IsBlockedAddress(address))
                {
                    throw new InvalidOperationException(
                        $"Webhook delivery host '{host}' resolves to the non-public address {address} and is blocked.");
                }
            }
        }

        // Pin: connect only to the exact addresses we just validated. No fresh DNS lookup happens here,
        // so the kernel can never be handed a rebound address between the check above and this connect.
        Exception? lastError = null;
        foreach (var address in addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await socketConnector(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException(
            $"Webhook delivery host '{host}' could not be connected on any validated address.",
            lastError);
    }

    /// <summary>
    /// Production <see cref="SocketConnector"/>: opens a TCP socket to the pinned endpoint and returns a
    /// <see cref="NetworkStream"/>. TLS negotiation (with correct SNI/cert validation against the request
    /// host name) is still performed by the owning <see cref="System.Net.Http.SocketsHttpHandler"/> on top.
    /// </summary>
    public static async ValueTask<Stream> ConnectSocketAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
