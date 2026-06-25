using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.NotionEditor.Helpers;

/// <summary>Generates notifications from comment workflow events.</summary>
public class CommentNotificationOrchestrator
{
    private readonly ITmNotificationService _notificationService;

    public CommentNotificationOrchestrator(ITmNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Notifies authors of parent entries when a new reply is added.</summary>
    public async Task OnNewReplyAsync(TmCommentThread thread, TmCommentEntry newEntry, CancellationToken ct = default)
    {
        if (newEntry.ParentEntryId is null) return;

        var parent = thread.Entries.FirstOrDefault(e => e.Id == newEntry.ParentEntryId);
        if (parent is null) return;
        if (parent.Author.Id == newEntry.Author.Id) return; // don't self-notify
        if (!ShouldNotifyParticipant(thread, parent.Author.Id)) return;

        await _notificationService.PublishAsync(new TmNotification
        {
            Type = TmNotificationTypes.Reply,
            Recipient = parent.Author,
            RecipientUserId = parent.Author.Id,
            Actor = newEntry.Author,
            Title = $"{newEntry.Author.DisplayName} replied to your comment",
            ActionUrl = $"/page/{thread.EntityRef.EntityId}#comment-{thread.Id}",
            EntityRef = thread.EntityRef,
            Metadata = Metadata(thread.Id, newEntry.Id)
        }, ct);
    }

    /// <summary>Notifies mentioned users.</summary>
    public async Task OnMentionAsync(TmCommentEntry entry, IEnumerable<string> mentionedUserIds, string threadId, string pageId, CancellationToken ct = default)
    {
        foreach (var userId in mentionedUserIds.Distinct())
        {
            if (userId == entry.Author.Id) continue;

            await _notificationService.PublishAsync(new TmNotification
            {
                Type = TmNotificationTypes.Mention,
                RecipientUserId = userId,
                Actor = entry.Author,
                Title = $"{entry.Author.DisplayName} mentioned you in a comment",
                ActionUrl = $"/page/{pageId}#comment-{threadId}",
                EntityRef = TmEntityRef.Create("page", pageId),
                Metadata = Metadata(threadId, entry.Id)
            }, ct);
        }
    }

    /// <summary>Notifies the entry author when someone reacts.</summary>
    public async Task OnReactionAsync(TmCommentEntry entry, string reactionEmoji, string reactorUserId, string reactorName, string threadId, string pageId, CancellationToken ct = default)
    {
        if (entry.Author.Id == reactorUserId) return;

        await _notificationService.PublishAsync(new TmNotification
        {
            Type = TmNotificationTypes.Reaction,
            Recipient = entry.Author,
            RecipientUserId = entry.Author.Id,
            Actor = new TmUserRef { Id = reactorUserId, DisplayName = reactorName },
            Title = $"{reactorName} reacted {reactionEmoji} to your comment",
            ActionUrl = $"/page/{pageId}#comment-{threadId}",
            EntityRef = TmEntityRef.Create("page", pageId),
            Metadata = Metadata(threadId, entry.Id, ("Reaction", reactionEmoji))
        }, ct);
    }

    /// <summary>Notifies thread participants when a thread is resolved.</summary>
    public async Task OnThreadResolvedAsync(TmCommentThread thread, string resolverUserId, string resolverName, CancellationToken ct = default)
    {
        var participants = thread.Entries
            .Select(e => e.Author.Id)
            .Distinct()
            .Where(id => id != resolverUserId && ShouldNotifyParticipant(thread, id));

        foreach (var userId in participants)
        {
            await _notificationService.PublishAsync(new TmNotification
            {
                Type = TmNotificationTypes.ThreadResolved,
                RecipientUserId = userId,
                Actor = new TmUserRef { Id = resolverUserId, DisplayName = resolverName },
                Title = $"{resolverName} resolved a comment thread",
                ActionUrl = $"/page/{thread.EntityRef.EntityId}#comment-{thread.Id}",
                EntityRef = thread.EntityRef,
                Metadata = Metadata(thread.Id, entryId: null)
            }, ct);
        }
    }

    private static bool ShouldNotifyParticipant(TmCommentThread thread, string userId)
        => thread.SubscribedUserIds.Count == 0 || thread.SubscribedUserIds.Contains(userId);

    private static Dictionary<string, object> Metadata(string threadId, string? entryId, params (string Key, object Value)[] additional)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["ThreadId"] = threadId
        };

        if (!string.IsNullOrWhiteSpace(entryId))
            metadata["EntryId"] = entryId;

        foreach (var (key, value) in additional)
        {
            metadata[key] = value;
        }

        return metadata;
    }
}
