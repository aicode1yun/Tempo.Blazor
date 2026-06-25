using FluentAssertions;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Tests.Notifications;

public class InMemoryNotificationStoreTests
{
    private readonly InMemoryNotificationStore _store = new();

    [Fact]
    public void NotifyAsync_AddsNotification()
    {
        var notification = NewNotification("u1", "u2", TmNotificationTypes.Reply, "hello");
        _store.PublishAsync(notification).Wait();

        var list = GetNotifications("u2");
        list.Should().ContainSingle();
        list[0].Title.Should().Be("hello");
    }

    [Fact]
    public void NotifyAsync_IncrementsBadge()
    {
        int changed = 0;
        _store.OnChanged += () => changed++;

        _store.PublishAsync(NewNotification("u1", "u2", TmNotificationTypes.Reply)).Wait();

        _store.UnreadCount.Should().Be(1);
        changed.Should().Be(1);
    }

    [Fact]
    public void MarkAsReadAsync_DecrementsBadge()
    {
        _store.PublishAsync(NewNotification("u1", "u2", TmNotificationTypes.Reply)).Wait();
        var list = GetNotifications("u2");

        _store.MarkAsReadAsync(list[0].Id, "u2").Wait();

        _store.UnreadCount.Should().Be(0);
    }

    [Fact]
    public void MarkAllAsReadAsync_ResetsBadge()
    {
        _store.PublishAsync(NewNotification("u1", "u2", TmNotificationTypes.Reply)).Wait();
        _store.PublishAsync(NewNotification("u1", "u2", TmNotificationTypes.Mention)).Wait();

        _store.MarkAllAsReadAsync("u2").Wait();

        _store.UnreadCount.Should().Be(0);
        _store.GetUnreadCountAsync("u2").Result.Should().Be(0);
    }

    [Fact]
    public void GetUnreadCountAsync_ReturnsUnreadOnly()
    {
        _store.PublishAsync(NewNotification("u1", "u2", TmNotificationTypes.Reply)).Wait();
        _store.PublishAsync(NewNotification("u1", "u2", TmNotificationTypes.Reply)).Wait();
        var list = GetNotifications("u2");
        _store.MarkAsReadAsync(list[0].Id, "u2").Wait();

        _store.GetUnreadCountAsync("u2").Result.Should().Be(1);
    }

    private IReadOnlyList<TmNotification> GetNotifications(string recipient)
        => _store.GetNotificationsAsync(new TmNotificationQuery { RecipientUserId = recipient }).Result;

    private static TmNotification NewNotification(
        string sender, string recipient, string type, string message = "msg")
        => new()
        {
            Type = type,
            RecipientUserId = recipient,
            Actor = new TmUserRef { Id = sender, DisplayName = sender },
            Title = message,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
