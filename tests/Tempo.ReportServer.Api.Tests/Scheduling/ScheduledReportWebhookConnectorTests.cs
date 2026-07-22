using System.Net;
using System.Net.Sockets;
using Tempo.ReportServer.Api.Scheduling;

namespace Tempo.ReportServer.Api.Tests.Scheduling;

/// <summary>
/// DNS-rebinding / TOCTOU specification for <see cref="ScheduledReportWebhookConnector"/>: the connect
/// path must resolve once, reject if any resolved address is non-public, and pin the socket to a
/// validated address rather than re-resolving the host.
/// </summary>
public sealed class ScheduledReportWebhookConnectorTests
{
    private static readonly ScheduledReportWebhookOptions Default = new();

    private sealed class RecordingConnector
    {
        public List<IPEndPoint> Endpoints { get; } = [];

        public bool Fail { get; set; }

        public ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
        {
            Endpoints.Add(endpoint);
            if (Fail)
            {
                throw new SocketException();
            }

            return ValueTask.FromResult<Stream>(new MemoryStream());
        }
    }

    [Fact]
    public async Task Connect_WhenHostResolvesToMixOfPublicAndPrivate_IsRejected_AndNeverConnects()
    {
        var connector = new RecordingConnector();

        var act = () => ScheduledReportWebhookConnector.ConnectValidatedAsync(
            "rebind.example.com",
            443,
            Default,
            // A rebinding attacker answers the guard with a public + a private address.
            _ => [IPAddress.Parse("93.184.216.34"), IPAddress.Parse("169.254.169.254")],
            connector.ConnectAsync).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*non-public*");
        connector.Endpoints.Should().BeEmpty("a rebinding host must never reach the socket");
    }

    [Fact]
    public async Task Connect_WhenAllAddressesPublic_PinsTheValidatedAddress()
    {
        var connector = new RecordingConnector();
        var pinned = IPAddress.Parse("93.184.216.34");

        await using var stream = await ScheduledReportWebhookConnector.ConnectValidatedAsync(
            "hooks.example.com",
            8443,
            Default,
            _ => [pinned],
            connector.ConnectAsync);

        stream.Should().NotBeNull();
        connector.Endpoints.Should().ContainSingle();
        connector.Endpoints[0].Address.Should().Be(pinned);
        connector.Endpoints[0].Port.Should().Be(8443);
    }

    [Fact]
    public async Task Connect_WhenFirstValidatedAddressFails_PinsToNextValidatedAddress()
    {
        var first = IPAddress.Parse("93.184.216.34");
        var second = IPAddress.Parse("198.51.100.7");
        var connector = new FailFirstConnector(first);

        await using var stream = await ScheduledReportWebhookConnector.ConnectValidatedAsync(
            "hooks.example.com",
            443,
            Default,
            _ => [first, second],
            connector.ConnectAsync);

        stream.Should().NotBeNull();
        // Both validated addresses are attempted, in order — the pin never falls back to a fresh lookup.
        connector.Endpoints.Select(e => e.Address).Should().Equal(first, second);
    }

    [Fact]
    public async Task Connect_ToPrivateLiteral_IsRejected()
    {
        var connector = new RecordingConnector();

        var act = () => ScheduledReportWebhookConnector.ConnectValidatedAsync(
            "169.254.169.254",
            80,
            Default,
            _ => throw new InvalidOperationException("resolver must not be called for a literal"),
            connector.ConnectAsync).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*non-public*");
        connector.Endpoints.Should().BeEmpty();
    }

    [Fact]
    public async Task Connect_ToPrivate_WhenPrivateNetworksAllowed_Pins()
    {
        var connector = new RecordingConnector();
        var options = Default with { AllowPrivateNetworks = true };
        var privateAddress = IPAddress.Parse("10.0.0.5");

        await using var stream = await ScheduledReportWebhookConnector.ConnectValidatedAsync(
            "10.0.0.5",
            443,
            options,
            _ => [privateAddress],
            connector.ConnectAsync);

        stream.Should().NotBeNull();
        connector.Endpoints.Should().ContainSingle();
        connector.Endpoints[0].Address.Should().Be(privateAddress);
    }

    private sealed class FailFirstConnector
    {
        private readonly IPAddress _failFor;

        public FailFirstConnector(IPAddress failFor) => _failFor = failFor;

        public List<IPEndPoint> Endpoints { get; } = [];

        public ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
        {
            Endpoints.Add(endpoint);
            if (endpoint.Address.Equals(_failFor))
            {
                throw new SocketException();
            }

            return ValueTask.FromResult<Stream>(new MemoryStream());
        }
    }
}
