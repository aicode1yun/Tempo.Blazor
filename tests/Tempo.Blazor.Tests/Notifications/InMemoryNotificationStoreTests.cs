using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Tests.Notifications;

public class InMemoryNotificationStoreTests
{
    private readonly InMemoryNotificationStore _store = new();

    [Fact]
    public void NotifyAsync_AddsNotification()
    {
        var evt = NewEvent("u1", "u2", NotificationType.Reply, "hello");
        _store.NotifyAsync(evt).Wait();

        var list = _store.GetNotificationsAsync("u2").Result;
        list.Should().ContainSingle();
        list[0].Event.Message.Should().Be("hello");
    }

    [Fact]
    public void NotifyAsync_IncrementsBadge()
    {
        int changed = 0;
        _store.OnChanged += () => changed++;

        _store.NotifyAsync(NewEvent("u1", "u2", NotificationType.Reply)).Wait();

        _store.UnreadCount.Should().Be(1);
        changed.Should().Be(1);
    }

    [Fact]
    public void MarkAsReadAsync_DecrementsBadge()
    {
        _store.NotifyAsync(NewEvent("u1", "u2", NotificationType.Reply)).Wait();
        var list = _store.GetNotificationsAsync("u2").Result;

        _store.MarkAsReadAsync(list[0].Id, "u2").Wait();

        _store.UnreadCount.Should().Be(0);
    }

    [Fact]
    public void MarkAllAsReadAsync_ResetsBadge()
    {
        _store.NotifyAsync(NewEvent("u1", "u2", NotificationType.Reply)).Wait();
        _store.NotifyAsync(NewEvent("u1", "u2", NotificationType.Mention)).Wait();

        _store.MarkAllAsReadAsync("u2").Wait();

        _store.UnreadCount.Should().Be(0);
        _store.GetUnreadCountAsync("u2").Result.Should().Be(0);
    }

    [Fact]
    public void GetUnreadCountAsync_ReturnsUnreadOnly()
    {
        _store.NotifyAsync(NewEvent("u1", "u2", NotificationType.Reply)).Wait();
        _store.NotifyAsync(NewEvent("u1", "u2", NotificationType.Reply)).Wait();
        var list = _store.GetNotificationsAsync("u2").Result;
        _store.MarkAsReadAsync(list[0].Id, "u2").Wait();

        _store.GetUnreadCountAsync("u2").Result.Should().Be(1);
    }

    private static NotificationEvent NewEvent(
        string sender, string recipient, NotificationType type, string message = "msg")
        => new()
        {
            Type = type,
            SenderUserId = sender,
            RecipientUserId = recipient,
            Message = message,
            CreatedAt = DateTime.UtcNow
        };
}
