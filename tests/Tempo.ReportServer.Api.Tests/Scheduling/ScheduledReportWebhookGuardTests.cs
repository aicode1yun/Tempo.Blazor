using System.Net;
using Tempo.ReportServer.Api.Scheduling;

namespace Tempo.ReportServer.Api.Tests.Scheduling;

/// <summary>SSRF specification for <see cref="ScheduledReportWebhookGuard"/>.</summary>
public sealed class ScheduledReportWebhookGuardTests
{
    private static readonly ScheduledReportWebhookOptions Default = new();

    [Fact]
    public void Validate_PublicHttpsTarget_IsAllowed()
    {
        var uri = ScheduledReportWebhookGuard.Validate(
            "https://hooks.example.com/report",
            Default,
            _ => [IPAddress.Parse("93.184.216.34")]);

        uri.Host.Should().Be("hooks.example.com");
    }

    [Fact]
    public void Validate_HttpScheme_IsRejected()
    {
        var act = () => ScheduledReportWebhookGuard.Validate("http://hooks.example.com/report", Default, _ => [IPAddress.Parse("93.184.216.34")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*scheme*");
    }

    [Theory]
    [InlineData("https://169.254.169.254/latest/meta-data")] // cloud metadata (link-local)
    [InlineData("https://10.0.0.5/hook")] // 10/8 private
    [InlineData("https://172.16.4.4/hook")] // 172.16/12 private
    [InlineData("https://192.168.1.10/hook")] // 192.168/16 private
    [InlineData("https://127.0.0.1/hook")] // loopback
    [InlineData("https://[::1]/hook")] // IPv6 loopback
    [InlineData("https://0.0.0.0/hook")] // 0/8 unspecified
    [InlineData("https://0.1.2.3/hook")] // 0/8 "this host"
    [InlineData("https://224.0.0.1/hook")] // 224/4 multicast
    [InlineData("https://239.255.255.250/hook")] // 224/4 multicast (SSDP)
    [InlineData("https://240.0.0.1/hook")] // 240/4 reserved
    [InlineData("https://255.255.255.255/hook")] // limited broadcast
    [InlineData("https://[ff02::1]/hook")] // IPv6 multicast
    [InlineData("https://[64:ff9b::a9fe:a9fe]/hook")] // NAT64 embedding 169.254.169.254
    public void Validate_PrivateOrLinkLocalLiteral_IsRejected(string target)
    {
        var act = () => ScheduledReportWebhookGuard.Validate(target, Default);

        act.Should().Throw<InvalidOperationException>().WithMessage("*blocked*");
    }

    [Fact]
    public void Validate_DnsNameResolvingToPrivate_IsRejected()
    {
        var act = () => ScheduledReportWebhookGuard.Validate(
            "https://internal.example.com/hook",
            Default,
            _ => [IPAddress.Parse("10.1.2.3")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*blocked*");
    }

    [Fact]
    public void Validate_PrivateTarget_WhenAllowed_IsPermitted()
    {
        var options = Default with { AllowPrivateNetworks = true };

        var uri = ScheduledReportWebhookGuard.Validate("https://10.0.0.5/hook", options);

        uri.Host.Should().Be("10.0.0.5");
    }

    [Fact]
    public void Validate_NonAbsoluteTarget_IsRejected()
    {
        var act = () => ScheduledReportWebhookGuard.Validate("/relative/path", Default);

        act.Should().Throw<InvalidOperationException>().WithMessage("*absolute URL*");
    }
}
