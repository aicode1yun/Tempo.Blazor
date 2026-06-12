using FluentAssertions;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using Tempo.Blazor.Demo.Api.Services;
using Tempo.Blazor.EmailTemplates.Abstractions.Contracts;

namespace Tempo.Blazor.Demo.Api.Tests;

public class SmtpEmailSenderTests
{
    private static EmailMessage Sample() => new(
        From: null, To: new[] { "to@example.com" }, Cc: new[] { "cc@example.com" },
        Subject: "Hello", Html: "<p>Hi</p>", Text: "Hi");

    private static (SmtpEmailSender sender, ISmtpClientWrapper client) Build(SmtpOptions? options = null)
    {
        var client = Substitute.For<ISmtpClientWrapper>();
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);
        var opts = Options.Create(options ?? new SmtpOptions { RetryDelay = TimeSpan.Zero });
        return (new SmtpEmailSender(factory, opts, NullLogger<SmtpEmailSender>.Instance), client);
    }

    [Fact]
    public async Task Send_BuildsMultipartMessage_WithRecipientsAndBodies()
    {
        var (sender, client) = Build();
        MimeMessage? captured = null;
        await client.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());

        await sender.SendAsync(Sample());

        captured.Should().NotBeNull();
        captured!.Subject.Should().Be("Hello");
        captured.To.Mailboxes.Should().ContainSingle(m => m.Address == "to@example.com");
        captured.Cc.Mailboxes.Should().ContainSingle(m => m.Address == "cc@example.com");
        captured.HtmlBody.Should().Contain("Hi");
        captured.TextBody.Should().Contain("Hi");
        captured.From.Mailboxes.Should().ContainSingle(m => m.Address == "no-reply@tempo.local");
    }

    [Fact]
    public async Task Send_ConnectsAndDisconnects()
    {
        var (sender, client) = Build();

        await sender.SendAsync(Sample());

        await client.Received(1).ConnectAsync("localhost", 2525, Arg.Any<MailKit.Security.SecureSocketOptions>(), Arg.Any<CancellationToken>());
        await client.Received(1).DisconnectAsync(true, Arg.Any<CancellationToken>());
        await client.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_RetriesTransientFailures_ThenSucceeds()
    {
        var (sender, client) = Build(new SmtpOptions { RetryDelay = TimeSpan.Zero, MaxRetries = 3 });
        var calls = 0;
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls++;
                return calls < 3 ? throw new SmtpProtocolException("transient") : Task.CompletedTask;
            });

        await sender.SendAsync(Sample());

        calls.Should().Be(3);
    }

    [Fact]
    public async Task Send_FatalFailure_DoesNotRetry()
    {
        var (sender, client) = Build(new SmtpOptions { RetryDelay = TimeSpan.Zero, MaxRetries = 3 });
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("fatal"));

        var act = () => sender.SendAsync(Sample());

        await act.Should().ThrowAsync<InvalidOperationException>();
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_WithCredentials_Authenticates()
    {
        var (sender, client) = Build(new SmtpOptions
        {
            RetryDelay = TimeSpan.Zero, Username = "user", Password = "pass",
        });

        await sender.SendAsync(Sample());

        await client.Received(1).AuthenticateAsync("user", "pass", Arg.Any<CancellationToken>());
    }
}
