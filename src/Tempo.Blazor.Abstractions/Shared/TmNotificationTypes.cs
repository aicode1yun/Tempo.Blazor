namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Well-known notification type keys used by Tempo components.</summary>
public static class TmNotificationTypes
{
    /// <summary>A user was mentioned.</summary>
    public const string Mention = "mention";

    /// <summary>A comment reply was added.</summary>
    public const string Reply = "reply";

    /// <summary>A reaction was added.</summary>
    public const string Reaction = "reaction";

    /// <summary>A comment thread was resolved.</summary>
    public const string ThreadResolved = "thread-resolved";

    /// <summary>A new comment thread was created.</summary>
    public const string NewThread = "new-thread";

    /// <summary>A Notion-style page was edited.</summary>
    public const string PageEdited = "page-edited";

    /// <summary>A page comment was added.</summary>
    public const string PageCommentAdded = "page-comment-added";

    /// <summary>A task was assigned.</summary>
    public const string TaskAssigned = "task-assigned";

    /// <summary>A page was shared.</summary>
    public const string PageShared = "page-shared";

    /// <summary>A work item assignment changed.</summary>
    public const string WorkItemAssigned = "work-item-assigned";

    /// <summary>A work item mention was created.</summary>
    public const string WorkItemMention = "work-item-mention";

    /// <summary>A work item deadline needs attention.</summary>
    public const string WorkItemDeadline = "work-item-deadline";
}
