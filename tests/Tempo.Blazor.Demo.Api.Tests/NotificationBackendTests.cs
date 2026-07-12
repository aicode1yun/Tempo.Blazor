using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Demo.Api.Hubs;
using Tempo.Blazor.Demo.Api.Services;
using Tempo.Blazor.EmailTemplates.Abstractions.Contracts;
using Tempo.Blazor.Notifications;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>K5: the SignalR broadcaster pushes to the recipient group; digest emails go out via IEmailSender.</summary>
public sealed class NotificationBackendTests
{
    private static (SignalRNotificationBroadcaster broadcaster, IHubClients clients, IClientProxy proxy) MakeBroadcaster(InMemoryNotificationStore store)
    {
        var proxy = Substitute.For<IClientProxy>();
        var clients = Substitute.For<IHubClients>();
        var hubContext = Substitute.For<IHubContext<TmNotificationHub>>();
        hubContext.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(proxy);
        return (new SignalRNotificationBroadcaster(store, hubContext), clients, proxy);
    }

    [Fact]
    public async Task Publish_PushesNotificationReceived_ToRecipientGroup()
    {
        var store = new InMemoryNotificationStore();
        var (broadcaster, clients, proxy) = MakeBroadcaster(store);

        var saved = await broadcaster.PublishAsync(new TmNotification
        {
            RecipientUserId = "u1", Type = "mention", Title = "Hi"
        });

        clients.Received(1).Group(SignalRNotificationService.GroupName("u1"));
        await proxy.Received(1).SendCoreAsync(
            SignalRNotificationService.HubMethods.NotificationReceived,
            Arg.Is<object?[]>(a => a.Length == 1 && ReferenceEquals(a[0], saved)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsRead_PushesNotificationsChanged_ToRecipientGroup()
    {
        var store = new InMemoryNotificationStore();
        var published = await store.PublishAsync(new TmNotification { RecipientUserId = "u1", Type = "t", Title = "T" });
        var (broadcaster, clients, proxy) = MakeBroadcaster(store);

        await broadcaster.MarkAsReadAsync(published.Id, "u1");

        clients.Received().Group(SignalRNotificationService.GroupName("u1"));
        await proxy.Received(1).SendCoreAsync(
            SignalRNotificationService.HubMethods.NotificationsChanged,
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Broadcaster_AdvertisesRealtimePush()
    {
        var store = new InMemoryNotificationStore();
        var (broadcaster, _, _) = MakeBroadcaster(store);
        broadcaster.Capabilities.HasFlag(TmNotificationServiceCapabilities.RealtimePush).Should().BeTrue();
        broadcaster.Capabilities.HasFlag(TmNotificationServiceCapabilities.DeliveryAck).Should().BeTrue();
    }

    [Fact]
    public async Task DigestSender_SendsHtmlEmail_ToRecipient()
    {
        var email = Substitute.For<IEmailSender>();
        var sender = new SmtpNotificationDigestSender(email);

        var digest = new TmNotificationDigest
        {
            RecipientUserId = "u1",
            Recipient = new TmUserRef { Id = "u1", Email = "u1@example.com", DisplayName = "User One" },
            Items = [new TmNotification { RecipientUserId = "u1", Type = "mention", Title = "You were mentioned" }]
        };

        await sender.SendAsync(digest);

        await email.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To.Contains("u1@example.com") &&
                m.Subject.Contains("1 update") &&
                m.Html.Contains("You were mentioned")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DigestSender_NoRecipientEmail_DoesNotSend()
    {
        var email = Substitute.For<IEmailSender>();
        var sender = new SmtpNotificationDigestSender(email);

        await sender.SendAsync(new TmNotificationDigest
        {
            RecipientUserId = "u1",
            Recipient = new TmUserRef { Id = "u1" }, // no email
            Items = [new TmNotification { RecipientUserId = "u1", Type = "t", Title = "T" }]
        });

        await email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }
}
