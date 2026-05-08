using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Notifications;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Notifications;

public class TmNotificationBellTests : LocalizationTestBase
{
    private readonly InMemoryNotificationStore _store = new();
    private readonly NotificationBadgeState _badge = new();

    public TmNotificationBellTests()
    {
        Services.AddSingleton<INotificationService>(_store);
        Services.AddSingleton<INotificationBadgeState>(_badge);
        Services.AddSingleton<NavigationManager>(new FakeNavManager());
    }

    [Fact]
    public void Bell_ShowsBadge_WhenUnreadNotifications()
    {
        _store.NotifyAsync(MakeEvent("alice", "Test")).Wait();
        var cut = RenderComponent<TmNotificationBell>();
        cut.FindAll(".tm-notification-bell__badge").Should().HaveCount(1);
    }

    [Fact]
    public void Bell_HidesBadge_WhenNoUnread()
    {
        var cut = RenderComponent<TmNotificationBell>();
        cut.FindAll(".tm-notification-bell__badge").Should().BeEmpty();
    }

    [Fact]
    public void Bell_Click_OpensPanel()
    {
        _store.NotifyAsync(MakeEvent("alice", "Test")).Wait();
        var cut = RenderComponent<TmNotificationBell>();
        cut.Find(".tm-notification-bell__button").Click();
        cut.FindAll(".tm-notification-bell__dropdown").Should().HaveCount(1);
    }

    [Fact]
    public void Bell_MarkAllRead_ClearsBadge()
    {
        _store.NotifyAsync(MakeEvent("alice", "Test")).Wait();
        var cut = RenderComponent<TmNotificationBell>();
        cut.Find(".tm-notification-bell__button").Click();
        cut.Find(".tm-notification-bell__mark-all").Click();
        cut.FindAll(".tm-notification-bell__badge").Should().BeEmpty();
    }

    [Fact]
    public void Bell_NotificationItem_RendersMessage()
    {
        _store.NotifyAsync(MakeEvent("alice", "Deploy complete")).Wait();
        var cut = RenderComponent<TmNotificationBell>();
        cut.Find(".tm-notification-bell__button").Click();
        cut.Find(".tm-notification-bell__message").TextContent.Should().Contain("Deploy complete");
    }

    [Fact]
    public void Bell_Empty_RendersEmptyState()
    {
        var cut = RenderComponent<TmNotificationBell>();
        cut.Find(".tm-notification-bell__button").Click();
        cut.Find(".tm-notification-bell__empty").Should().NotBeNull();
    }

    [Fact]
    public void Bell_Filter_ShowsOnlyUnread()
    {
        _store.NotifyAsync(MakeEvent("alice", "Unread 1")).Wait();
        _store.MarkAllAsReadAsync("demo").Wait();
        _store.NotifyAsync(MakeEvent("bob", "Unread 2")).Wait();
        var cut = RenderComponent<TmNotificationBell>(p => p.Add(c => c.CurrentUserId, "demo"));
        cut.Find(".tm-notification-bell__button").Click();
        cut.Find(".tm-notification-bell__filter").Click(); // switch to only unread
        cut.FindAll(".tm-notification-bell__item").Should().HaveCount(1);
        cut.Find(".tm-notification-bell__message").TextContent.Should().Contain("Unread 2");
    }

    private static NotificationEvent MakeEvent(string sender, string message) => new()
    {
        Type = NotificationType.Mention,
        RecipientUserId = "demo",
        SenderUserId = sender,
        SenderName = sender,
        Message = message,
        CreatedAt = DateTime.UtcNow
    };

    private sealed class FakeNavManager : NavigationManager
    {
        public FakeNavManager()
        {
            Initialize("https://localhost/", "https://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}
