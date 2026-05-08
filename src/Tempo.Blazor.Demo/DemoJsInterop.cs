using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// JS-interop helper exposed to E2E tests so they can inject demo notifications
/// directly without relying on mention orchestration (which targets other users).
/// </summary>
public static class DemoJsInterop
{
    private static INotificationService? _notificationService;
    private static InMemoryNotificationStore? _store;

    public static void Initialize(INotificationService service)
    {
        _notificationService = service;
        _store = service as InMemoryNotificationStore;
    }

    [JSInvokable("NotifyDemoAsync")]
    public static async Task NotifyDemoAsync(string message, string deepLink)
    {
        if (_notificationService is null)
            throw new InvalidOperationException("DemoJsInterop not initialized");

        await _notificationService.NotifyAsync(new NotificationEvent
        {
            Type = NotificationType.Mention,
            RecipientUserId = "demo",
            SenderUserId = "system",
            SenderName = "System",
            Message = message,
            DeepLink = deepLink
        });
    }

    [JSInvokable("ClearDemoNotificationsAsync")]
    public static Task ClearDemoNotificationsAsync()
    {
        _store?.ClearAll();
        return Task.CompletedTask;
    }
}
