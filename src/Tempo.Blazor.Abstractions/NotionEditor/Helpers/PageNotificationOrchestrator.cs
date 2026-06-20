using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Helpers;

/// <summary>Generates page-level notifications for users watching a page or subtree.</summary>
public sealed class PageNotificationOrchestrator
{
    private readonly INotificationService _notificationService;

    public PageNotificationOrchestrator(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task OnPageEditedAsync(
        INotionWatchProvider watchProvider,
        string pageId,
        string pageTitle,
        string? actorUserId,
        string? actorName,
        CancellationToken cancellationToken = default)
        => NotifyWatchersAsync(
            watchProvider,
            pageId,
            NotificationType.PageEdited,
            actorUserId,
            actorName,
            $"{DisplayName(actorName, actorUserId)} edited {DisplayTitle(pageTitle)}",
            $"/notion-editor?page={Uri.EscapeDataString(pageId)}",
            entryId: pageId,
            cancellationToken);

    public Task OnPageCommentAddedAsync(
        INotionWatchProvider watchProvider,
        string pageId,
        string pageTitle,
        string? actorUserId,
        string? actorName,
        string commentId,
        CancellationToken cancellationToken = default)
        => NotifyWatchersAsync(
            watchProvider,
            pageId,
            NotificationType.PageCommentAdded,
            actorUserId,
            actorName,
            $"{DisplayName(actorName, actorUserId)} commented on {DisplayTitle(pageTitle)}",
            $"/notion-editor?page={Uri.EscapeDataString(pageId)}#comment-{Uri.EscapeDataString(commentId)}",
            entryId: commentId,
            cancellationToken);

    public async Task OnTaskAssignedAsync(
        INotionWatchProvider watchProvider,
        string pageId,
        string pageTitle,
        string taskId,
        string assigneeUserId,
        string? actorUserId,
        string? actorName,
        CancellationToken cancellationToken = default)
    {
        var recipients = (await GetWatcherRecipientsAsync(watchProvider, pageId, actorUserId, cancellationToken))
            .Append(assigneeUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Where(id => !string.Equals(id, actorUserId, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var recipient in recipients)
        {
            await _notificationService.NotifyAsync(new NotificationEvent
            {
                Type = NotificationType.TaskAssigned,
                RecipientUserId = recipient,
                SenderUserId = actorUserId,
                SenderName = actorName,
                Message = $"{DisplayName(actorName, actorUserId)} assigned a task on {DisplayTitle(pageTitle)}",
                DeepLink = $"/notion-editor?page={Uri.EscapeDataString(pageId)}#block-{Uri.EscapeDataString(taskId)}",
                EntryId = taskId
            }, cancellationToken);
        }
    }

    public Task OnPageSharedAsync(
        INotionWatchProvider watchProvider,
        string pageId,
        string pageTitle,
        string? actorUserId,
        string? actorName,
        CancellationToken cancellationToken = default)
        => NotifyWatchersAsync(
            watchProvider,
            pageId,
            NotificationType.PageShared,
            actorUserId,
            actorName,
            $"{DisplayName(actorName, actorUserId)} shared {DisplayTitle(pageTitle)}",
            $"/notion-editor?page={Uri.EscapeDataString(pageId)}",
            entryId: pageId,
            cancellationToken);

    private async Task NotifyWatchersAsync(
        INotionWatchProvider watchProvider,
        string pageId,
        NotificationType type,
        string? actorUserId,
        string? actorName,
        string message,
        string deepLink,
        string entryId,
        CancellationToken cancellationToken)
    {
        foreach (var recipient in await GetWatcherRecipientsAsync(watchProvider, pageId, actorUserId, cancellationToken))
        {
            await _notificationService.NotifyAsync(new NotificationEvent
            {
                Type = type,
                RecipientUserId = recipient,
                SenderUserId = actorUserId,
                SenderName = actorName,
                Message = message,
                DeepLink = deepLink,
                EntryId = entryId
            }, cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<string>> GetWatcherRecipientsAsync(
        INotionWatchProvider watchProvider,
        string pageId,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var watchers = await watchProvider.GetWatchersAsync(pageId, cancellationToken);
        return watchers
            .Select(w => w.UserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Where(id => !string.Equals(id, actorUserId, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string DisplayName(string? actorName, string? actorUserId)
        => string.IsNullOrWhiteSpace(actorName) ? actorUserId ?? "Someone" : actorName;

    private static string DisplayTitle(string pageTitle)
        => string.IsNullOrWhiteSpace(pageTitle) ? "an untitled page" : pageTitle.Trim();
}
