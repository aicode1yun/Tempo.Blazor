using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Helpers;

/// <summary>Generates notifications from comment workflow events.</summary>
public class CommentNotificationOrchestrator
{
    private readonly INotificationService _notificationService;

    public CommentNotificationOrchestrator(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Notifies authors of parent entries when a new reply is added.</summary>
    public async Task OnNewReplyAsync(IBlockComment thread, INotionCommentEntry newEntry, CancellationToken ct = default)
    {
        if (newEntry.ParentEntryId is null) return;

        var parent = thread.Thread.FirstOrDefault(e => e.Id == newEntry.ParentEntryId);
        if (parent is null) return;
        if (parent.AuthorUserId == newEntry.AuthorUserId) return; // don't self-notify
        if (!thread.SubscribedUserIds.Contains(parent.AuthorUserId)) return;

        await _notificationService.NotifyAsync(new NotificationEvent
        {
            Type = NotificationType.Reply,
            RecipientUserId = parent.AuthorUserId,
            SenderUserId = newEntry.AuthorUserId,
            SenderName = newEntry.AuthorDisplayName,
            SenderAvatarUrl = newEntry.AuthorAvatarUrl,
            Message = $"{newEntry.AuthorDisplayName} replied to your comment",
            DeepLink = $"/page/{thread.BlockId}#comment-{thread.Id}",
            ThreadId = thread.Id.ToString(),
            EntryId = newEntry.Id.ToString()
        }, ct);
    }

    /// <summary>Notifies mentioned users.</summary>
    public async Task OnMentionAsync(INotionCommentEntry entry, IEnumerable<string> mentionedUserIds, string threadId, string pageId, CancellationToken ct = default)
    {
        foreach (var userId in mentionedUserIds.Distinct())
        {
            if (userId == entry.AuthorUserId) continue;

            await _notificationService.NotifyAsync(new NotificationEvent
            {
                Type = NotificationType.Mention,
                RecipientUserId = userId,
                SenderUserId = entry.AuthorUserId,
                SenderName = entry.AuthorDisplayName,
                SenderAvatarUrl = entry.AuthorAvatarUrl,
                Message = $"{entry.AuthorDisplayName} mentioned you in a comment",
                DeepLink = $"/page/{pageId}#comment-{threadId}",
                ThreadId = threadId,
                EntryId = entry.Id.ToString()
            }, ct);
        }
    }

    /// <summary>Notifies the entry author when someone reacts.</summary>
    public async Task OnReactionAsync(INotionCommentEntry entry, string reactionEmoji, string reactorUserId, string reactorName, string threadId, string pageId, CancellationToken ct = default)
    {
        if (entry.AuthorUserId == reactorUserId) return;

        await _notificationService.NotifyAsync(new NotificationEvent
        {
            Type = NotificationType.Reaction,
            RecipientUserId = entry.AuthorUserId,
            SenderUserId = reactorUserId,
            SenderName = reactorName,
            Message = $"{reactorName} reacted {reactionEmoji} to your comment",
            DeepLink = $"/page/{pageId}#comment-{threadId}",
            ThreadId = threadId,
            EntryId = entry.Id.ToString()
        }, ct);
    }

    /// <summary>Notifies thread participants when a thread is resolved.</summary>
    public async Task OnThreadResolvedAsync(IBlockComment thread, string resolverUserId, string resolverName, CancellationToken ct = default)
    {
        var participants = thread.Thread
            .Select(e => e.AuthorUserId)
            .Distinct()
            .Where(id => id != resolverUserId && thread.SubscribedUserIds.Contains(id));

        foreach (var userId in participants)
        {
            await _notificationService.NotifyAsync(new NotificationEvent
            {
                Type = NotificationType.ThreadResolved,
                RecipientUserId = userId,
                SenderUserId = resolverUserId,
                SenderName = resolverName,
                Message = $"{resolverName} resolved a comment thread",
                DeepLink = $"/page/{thread.BlockId}#comment-{thread.Id}",
                ThreadId = thread.Id.ToString()
            }, ct);
        }
    }
}
