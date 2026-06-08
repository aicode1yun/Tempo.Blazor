using System.Text.Json;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.NotionEditor.UI;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionWatchNotificationsTests : LocalizationTestBase
{
    private readonly InMemoryNotificationStore _notifications = new();
    private readonly TestWatchProvider _watchProvider = new();

    public TmNotionWatchNotificationsTests()
    {
        Services.AddSingleton<INotificationService>(_notifications);
        Services.AddSingleton<INotificationBadgeState>(_notifications);
        Services.AddSingleton<NavigationManager>(new FakeNavManager());
    }

    [Fact]
    public void NotificationType_AppendsPageTypes_WithoutChangingExistingValues()
    {
        ((int)NotificationType.Mention).Should().Be(0);
        ((int)NotificationType.Reply).Should().Be(1);
        ((int)NotificationType.Reaction).Should().Be(2);
        ((int)NotificationType.ThreadResolved).Should().Be(3);
        ((int)NotificationType.NewThread).Should().Be(4);
        ((int)NotificationType.PageEdited).Should().Be(5);
        ((int)NotificationType.PageCommentAdded).Should().Be(6);
        ((int)NotificationType.TaskAssigned).Should().Be(7);
        ((int)NotificationType.PageShared).Should().Be(8);

        var json = JsonSerializer.Serialize(NotificationType.PageEdited);
        JsonSerializer.Deserialize<NotificationType>(json).Should().Be(NotificationType.PageEdited);
    }

    [Fact]
    public async Task PageNotificationOrchestrator_NotifiesWatchers_AndSkipsActor()
    {
        await _watchProvider.WatchAsync("page-1", "alice", includeChildren: false);
        await _watchProvider.WatchAsync("page-1", "bob", includeChildren: false);
        var orchestrator = new PageNotificationOrchestrator(_notifications);

        await orchestrator.OnPageEditedAsync(_watchProvider, "page-1", "Launch Plan", "alice", "Alice");

        (await _notifications.GetNotificationsAsync("alice")).Should().BeEmpty();
        var bob = await _notifications.GetNotificationsAsync("bob");
        bob.Should().ContainSingle();
        bob[0].Event.Type.Should().Be(NotificationType.PageEdited);
        bob[0].Event.DeepLink.Should().Contain("page-1");
    }

    [Fact]
    public void WatchButton_TogglesWatch_AndIncludeChildren()
    {
        var cut = RenderComponent<TmNotionWatchButton>(parameters => parameters
            .Add(p => p.Provider, _watchProvider)
            .Add(p => p.PageId, "page-1")
            .Add(p => p.UserId, "alice"));

        cut.Find("[data-testid='notion-watch-toggle']").Click();
        _watchProvider.IsWatchingAsync("page-1", "alice").Result.Should().BeTrue();

        cut.Find("[data-testid='notion-watch-include-children']").Change(true);
        _watchProvider.GetWatchersAsync("page-1").Result.Single().IncludeChildren.Should().BeTrue();

        cut.Find("[data-testid='notion-watch-toggle']").Click();
        _watchProvider.IsWatchingAsync("page-1", "alice").Result.Should().BeFalse();
    }

    [Fact]
    public void NotificationCenter_ShowsBadge_MarksAllRead_AndNavigates()
    {
        _notifications.NotifyAsync(new NotificationEvent
        {
            Type = NotificationType.PageEdited,
            RecipientUserId = "alice",
            SenderUserId = "bob",
            SenderName = "Bob",
            Message = "Bob edited Launch Plan",
            DeepLink = "/notion-editor?page=page-1"
        }).Wait();

        var nav = (FakeNavManager)Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<TmNotionNotificationCenter>(parameters => parameters
            .Add(p => p.CurrentUserId, "alice")
            .Add(p => p.PollInterval, TimeSpan.Zero));

        cut.Find("[data-testid='notion-notification-badge']").TextContent.Should().Be("1");
        cut.Find("[data-testid='notion-notification-toggle']").Click();
        cut.Find("[data-testid='notion-notification-item']").TextContent.Should().Contain("Bob edited Launch Plan");
        cut.Find("[data-testid='notion-notification-item']").Click();

        nav.Uri.Should().Contain("/notion-editor?page=page-1");
        _notifications.GetUnreadCountAsync("alice").Result.Should().Be(0);
    }

    private sealed class TestWatchProvider : INotionWatchProvider
    {
        private readonly Dictionary<(string PageId, string UserId), NotionWatchSubscriptionDto> _items = new();

        public Task WatchAsync(string pageId, string userId, bool includeChildren, CancellationToken cancellationToken = default)
        {
            _items[(pageId, userId)] = new NotionWatchSubscriptionDto
            {
                PageId = pageId,
                UserId = userId,
                IncludeChildren = includeChildren
            };
            return Task.CompletedTask;
        }

        public Task UnwatchAsync(string pageId, string userId, CancellationToken cancellationToken = default)
        {
            _items.Remove((pageId, userId));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<NotionWatchSubscriptionDto>> GetWatchersAsync(string pageId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<NotionWatchSubscriptionDto> watchers = _items.Values
                .Where(item => string.Equals(item.PageId, pageId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Task.FromResult(watchers);
        }

        public Task<bool> IsWatchingAsync(string pageId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.ContainsKey((pageId, userId)));
    }

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
