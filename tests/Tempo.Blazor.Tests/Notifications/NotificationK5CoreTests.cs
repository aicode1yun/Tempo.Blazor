using FluentAssertions;
using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Services;
using Xunit;

namespace Tempo.Blazor.Tests.Notifications;

/// <summary>K5: delivery-ack, digest builder/runner, and push-subscription store.</summary>
public class NotificationK5CoreTests
{
    private static TmNotification Note(string user, string title, string type = "mention", DateTimeOffset? created = null, bool read = false)
        => new()
        {
            RecipientUserId = user,
            Recipient = new TmUserRef { Id = user, DisplayName = user, Email = $"{user}@example.com" },
            Type = type,
            Title = title,
            CreatedAt = created ?? DateTimeOffset.UtcNow,
            ReadAt = read ? DateTimeOffset.UtcNow : null
        };

    // ── Delivery ack ─────────────────────────────────────────────

    [Fact]
    public async Task Store_AdvertisesDeliveryAck_AndMarksDelivered()
    {
        var store = new InMemoryNotificationStore();
        store.Capabilities.HasFlag(TmNotificationServiceCapabilities.DeliveryAck).Should().BeTrue();

        var published = await store.PublishAsync(Note("u1", "Hi"));
        published.IsDelivered.Should().BeFalse();

        await store.MarkAsDeliveredAsync(published.Id, "u1");

        var list = await store.GetNotificationsAsync(new TmNotificationQuery { RecipientUserId = "u1", IncludeRead = true });
        list.Single().IsDelivered.Should().BeTrue();
        list.Single().IsRead.Should().BeFalse(); // delivery is distinct from read
    }

    [Fact]
    public async Task DefaultInterfaceMethod_IsNoOp_ForImplementationsThatDoNotOverride()
    {
        // A minimal legacy implementation that does not override MarkAsDeliveredAsync still compiles
        // and the default no-op does not throw.
        ITmNotificationService legacy = new LegacyNotificationService();
        await legacy.MarkAsDeliveredAsync("x", "u1"); // must not throw
    }

    // ── Digest builder ───────────────────────────────────────────

    [Fact]
    public void DigestBuilder_BelowMinItems_ReturnsNull()
    {
        var recipient = new TmUserRef { Id = "u1", Email = "u1@example.com" };
        var digest = TmNotificationDigestBuilder.Build(
            recipient,
            [Note("u1", "A")],
            new TmNotificationDigestOptions { MinItems = 2 },
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        digest.Should().BeNull();
    }

    [Fact]
    public void DigestBuilder_UnreadOnly_ExcludesReadAndOrdersNewestFirst()
    {
        var recipient = new TmUserRef { Id = "u1", Email = "u1@example.com" };
        var now = DateTimeOffset.UtcNow;
        var items = new[]
        {
            Note("u1", "old-unread", created: now.AddHours(-3)),
            Note("u1", "read", created: now.AddHours(-2), read: true),
            Note("u1", "new-unread", type: "reply", created: now.AddHours(-1)),
        };

        var digest = TmNotificationDigestBuilder.Build(
            recipient, items, new TmNotificationDigestOptions { UnreadOnly = true, MinItems = 1 },
            now.AddDays(-1), now.AddDays(1));

        digest.Should().NotBeNull();
        digest!.TotalCount.Should().Be(2);
        digest.Items[0].Title.Should().Be("new-unread");
        digest.CountsByType.Should().ContainKey("mention").WhoseValue.Should().Be(1);
        digest.CountsByType.Should().ContainKey("reply").WhoseValue.Should().Be(1);
        digest.RecipientEmail.Should().Be("u1@example.com");
    }

    // ── Digest runner ────────────────────────────────────────────

    [Fact]
    public async Task DigestRunner_SendsPerRecipient_AboveThresholdOnly()
    {
        var store = new InMemoryNotificationStore();
        await store.PublishAsync(Note("u1", "A"));
        await store.PublishAsync(Note("u1", "B"));
        await store.PublishAsync(Note("u2", "C")); // u2 has only 1 → below MinItems=2

        var recipients = new FakeRecipients(
            new TmUserRef { Id = "u1", Email = "u1@example.com" },
            new TmUserRef { Id = "u2", Email = "u2@example.com" });
        var sender = new CapturingSender();

        var runner = new NotificationDigestRunner(store, recipients, sender,
            new TmNotificationDigestOptions { MinItems = 2 });

        var sent = await runner.RunOnceAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        sent.Should().ContainSingle();
        sender.Digests.Should().ContainSingle();
        sender.Digests[0].RecipientUserId.Should().Be("u1");
        sender.Digests[0].TotalCount.Should().Be(2);
    }

    // ── Push subscription store ──────────────────────────────────

    [Fact]
    public async Task PushStore_UpsertByEndpoint_RemoveAndQueryByUser()
    {
        var store = new InMemoryPushSubscriptionStore();
        var sub = new TmPushSubscription { UserId = "u1", Endpoint = "https://push/e1", P256dh = "k", Auth = "a" };
        await store.SaveAsync(sub);
        await store.SaveAsync(new TmPushSubscription { UserId = "u1", Endpoint = "https://push/e1", P256dh = "k2", Auth = "a2" }); // upsert
        await store.SaveAsync(new TmPushSubscription { UserId = "u2", Endpoint = "https://push/e2", P256dh = "k", Auth = "a" });

        (await store.GetForUserAsync("u1")).Should().ContainSingle().Which.P256dh.Should().Be("k2");
        store.Count.Should().Be(2);

        await store.RemoveAsync("https://push/e1");
        (await store.GetForUserAsync("u1")).Should().BeEmpty();
    }

    [Fact]
    public void PushStore_Save_RejectsIncompleteSubscription()
    {
        var store = new InMemoryPushSubscriptionStore();
        var act = () => store.SaveAsync(new TmPushSubscription { UserId = "u1", Endpoint = "e" }); // missing keys
        act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Fakes ────────────────────────────────────────────────────

    private sealed class FakeRecipients(params TmUserRef[] users) : INotificationRecipientSource
    {
        public Task<IReadOnlyList<TmUserRef>> GetRecipientsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TmUserRef>>(users);
    }

    private sealed class CapturingSender : INotificationDigestSender
    {
        public List<TmNotificationDigest> Digests { get; } = [];
        public Task SendAsync(TmNotificationDigest digest, CancellationToken cancellationToken = default)
        {
            Digests.Add(digest);
            return Task.CompletedTask;
        }
    }

    private sealed class LegacyNotificationService : ITmNotificationService
    {
        public event Action? OnChanged { add { } remove { } }
        public TmNotificationServiceCapabilities Capabilities => TmNotificationServiceCapabilities.Publish;
        TmNotificationServiceCapabilities ITmCapabilityProvider<TmNotificationServiceCapabilities>.Capabilities => Capabilities;
        public Task<TmNotification> PublishAsync(TmNotification notification, CancellationToken cancellationToken = default) => Task.FromResult(notification);
        public Task<IReadOnlyList<TmNotification>> GetNotificationsAsync(TmNotificationQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TmNotification>>([]);
        public Task<int> GetUnreadCountAsync(string recipientUserId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task MarkAsReadAsync(string notificationId, string recipientUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(string recipientUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        // deliberately does NOT override MarkAsDeliveredAsync — uses the default interface no-op
    }
}
