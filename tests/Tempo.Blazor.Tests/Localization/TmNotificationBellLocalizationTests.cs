using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Notifications;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Tests.Localization;

/// <summary>
/// RED tests – TmNotificationBell must use ITmLocalizer for aria-label, title,
/// "Mark all as read" and "No notifications" strings.
/// </summary>
public class TmNotificationBellLocalizationTests : LocalizationTestBase
{
    private readonly InMemoryNotificationStore _store = new();

    public TmNotificationBellLocalizationTests()
    {
        Services.AddSingleton<INotificationService>(_store);
        Services.AddSingleton<INotificationBadgeState>(_store);
        Services.AddSingleton<NavigationManager>(new FakeNavManager());
    }

    [Fact]
    public void TmNotificationBell_AriaLabel_UsesLocalizer()
    {
        UseCzechLocalization();

        var cut = RenderComponent<TmNotificationBell>();

        cut.Find(".tm-notification-bell__button").GetAttribute("aria-label")
            .Should().Be("Oznámení");
    }

    [Fact]
    public void TmNotificationBell_AriaLabel_English_ShowsEnglishText()
    {
        var cut = RenderComponent<TmNotificationBell>();

        cut.Find(".tm-notification-bell__button").GetAttribute("aria-label")
            .Should().Be("Notifications");
    }

    [Fact]
    public void TmNotificationBell_Title_UsesLocalizer()
    {
        UseCzechLocalization();
        _store.NotifyAsync(MakeEvent("Test")).Wait();

        var cut = RenderComponent<TmNotificationBell>();
        cut.Find(".tm-notification-bell__button").Click();

        cut.Find(".tm-notification-bell__title").TextContent
            .Should().Be("Oznámení");
    }

    [Fact]
    public void TmNotificationBell_MarkAllRead_UsesLocalizer()
    {
        UseCzechLocalization();
        _store.NotifyAsync(MakeEvent("Test")).Wait();

        var cut = RenderComponent<TmNotificationBell>();
        cut.Find(".tm-notification-bell__button").Click();

        cut.Find(".tm-notification-bell__mark-all").TextContent.Trim()
            .Should().Be("Označit vše jako přečtené");
    }

    [Fact]
    public void TmNotificationBell_NoNotifications_UsesLocalizer()
    {
        UseCzechLocalization();

        var cut = RenderComponent<TmNotificationBell>();
        cut.Find(".tm-notification-bell__button").Click();

        cut.Find(".tm-notification-bell__empty").TextContent
            .Should().Be("Žádná oznámení");
    }

    [Fact]
    public void TmNotificationBell_NoNotifications_English_ShowsEnglishText()
    {
        var cut = RenderComponent<TmNotificationBell>();
        cut.Find(".tm-notification-bell__button").Click();

        cut.Find(".tm-notification-bell__empty").TextContent
            .Should().Be("No notifications");
    }

    private static NotificationEvent MakeEvent(string message) => new()
    {
        Type = NotificationType.Mention,
        RecipientUserId = "demo",
        SenderUserId = "alice",
        SenderName = "Alice",
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
