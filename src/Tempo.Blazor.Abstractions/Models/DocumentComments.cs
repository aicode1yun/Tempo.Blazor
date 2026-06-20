namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Normalized anchor kind for a document comment thread.</summary>
public enum DocumentCommentAnchorKind
{
    /// <summary>A point on a document page.</summary>
    Point,

    /// <summary>A rectangular area on a document page.</summary>
    Area,

    /// <summary>The whole document page.</summary>
    Page
}

/// <summary>Status of a document comment thread.</summary>
public enum DocumentCommentThreadStatus
{
    /// <summary>The thread still needs attention.</summary>
    Open,

    /// <summary>The thread has been resolved.</summary>
    Resolved
}

/// <summary>Document comment display mode.</summary>
public enum DocumentCommentMode
{
    /// <summary>Comments are visible but new anchors are not created by page clicks.</summary>
    Browse,

    /// <summary>Page clicks or drags create a new comment draft.</summary>
    Comment
}

/// <summary>Filter applied to the document comment thread panel.</summary>
public enum DocumentCommentThreadFilter
{
    /// <summary>Show open comment threads.</summary>
    Open,

    /// <summary>Show resolved comment threads.</summary>
    Resolved,

    /// <summary>Show comment threads mentioning the current user.</summary>
    Mentions
}

/// <summary>Normalized document page anchor for a comment thread.</summary>
public sealed class DocumentCommentAnchor
{
    /// <summary>One-based page number where the comment is anchored.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>Anchor kind.</summary>
    public DocumentCommentAnchorKind Kind { get; set; } = DocumentCommentAnchorKind.Point;

    /// <summary>Normalized horizontal position in the page, from 0 to 1.</summary>
    public double X { get; set; }

    /// <summary>Normalized vertical position in the page, from 0 to 1.</summary>
    public double Y { get; set; }

    /// <summary>Normalized width for area anchors, from 0 to 1.</summary>
    public double Width { get; set; }

    /// <summary>Normalized height for area anchors, from 0 to 1.</summary>
    public double Height { get; set; }

    /// <summary>Creates a point anchor.</summary>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="x">Normalized horizontal position.</param>
    /// <param name="y">Normalized vertical position.</param>
    public static DocumentCommentAnchor Point(int pageNumber, double x, double y)
    {
        return new DocumentCommentAnchor
        {
            PageNumber = pageNumber,
            Kind = DocumentCommentAnchorKind.Point,
            X = Clamp01(x),
            Y = Clamp01(y)
        };
    }

    /// <summary>Creates an area anchor.</summary>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="x">Normalized horizontal position.</param>
    /// <param name="y">Normalized vertical position.</param>
    /// <param name="width">Normalized width.</param>
    /// <param name="height">Normalized height.</param>
    public static DocumentCommentAnchor Area(int pageNumber, double x, double y, double width, double height)
    {
        var normalizedX = Clamp01(x);
        var normalizedY = Clamp01(y);
        return new DocumentCommentAnchor
        {
            PageNumber = pageNumber,
            Kind = DocumentCommentAnchorKind.Area,
            X = normalizedX,
            Y = normalizedY,
            Width = Math.Min(Clamp01(width), 1 - normalizedX),
            Height = Math.Min(Clamp01(height), 1 - normalizedY)
        };
    }

    /// <summary>Creates a page-level anchor.</summary>
    /// <param name="pageNumber">One-based page number.</param>
    public static DocumentCommentAnchor Page(int pageNumber)
    {
        return new DocumentCommentAnchor
        {
            PageNumber = pageNumber,
            Kind = DocumentCommentAnchorKind.Page
        };
    }

    private static double Clamp01(double value)
    {
        return double.IsFinite(value) ? Math.Min(Math.Max(value, 0), 1) : 0;
    }
}

/// <summary>User available for document comment mentions.</summary>
public sealed class DocumentCommentUser
{
    /// <summary>Stable user identifier.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Displayed user name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional email address.</summary>
    public string? Email { get; set; }

    /// <summary>Optional avatar image URL.</summary>
    public string? AvatarUrl { get; set; }
}

/// <summary>Mention metadata stored with a document comment.</summary>
public sealed class DocumentCommentMention
{
    /// <summary>Stable user identifier.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Displayed mention text.</summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>Reaction metadata stored with a document comment.</summary>
public sealed class DocumentCommentReaction
{
    /// <summary>Emoji or compact reaction value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>User ids that applied this reaction.</summary>
    public List<string> UserIds { get; set; } = [];
}

/// <summary>Single comment entry inside a document comment thread.</summary>
public sealed class DocumentComment
{
    /// <summary>Stable comment identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Author user identifier.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>Displayed author name.</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>Optional author avatar image URL.</summary>
    public string? AuthorAvatarUrl { get; set; }

    /// <summary>Plain text comment body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last edit timestamp, when edited.</summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>Users mentioned by this comment.</summary>
    public List<DocumentCommentMention> Mentions { get; set; } = [];

    /// <summary>Reactions applied to this comment.</summary>
    public List<DocumentCommentReaction> Reactions { get; set; } = [];

    /// <summary>Whether the current viewer may edit this comment.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Whether the current viewer may delete this comment.</summary>
    public bool CanDelete { get; set; }
}

/// <summary>Document comment thread anchored to a page location.</summary>
public sealed class DocumentCommentThread
{
    /// <summary>Stable thread identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Thread anchor on the document page.</summary>
    public DocumentCommentAnchor Anchor { get; set; } = DocumentCommentAnchor.Page(1);

    /// <summary>Thread status.</summary>
    public DocumentCommentThreadStatus Status { get; set; } = DocumentCommentThreadStatus.Open;

    /// <summary>Comments in the thread.</summary>
    public List<DocumentComment> Comments { get; set; } = [];

    /// <summary>User id that resolved the thread, when resolved.</summary>
    public string? ResolvedByUserId { get; set; }

    /// <summary>Displayed name of the user that resolved the thread.</summary>
    public string? ResolvedByName { get; set; }

    /// <summary>Timestamp when the thread was resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }
}

/// <summary>Payload emitted when a new document comment thread is requested.</summary>
public sealed class DocumentCommentThreadCreateRequest
{
    /// <summary>Anchor requested for the new thread.</summary>
    public DocumentCommentAnchor Anchor { get; set; } = DocumentCommentAnchor.Page(1);

    /// <summary>Plain text body for the first comment.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Mentions detected in the body.</summary>
    public List<DocumentCommentMention> Mentions { get; set; } = [];
}

/// <summary>Payload emitted when a reply is requested.</summary>
public sealed class DocumentCommentReplyRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Plain text reply body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Mentions detected in the body.</summary>
    public List<DocumentCommentMention> Mentions { get; set; } = [];
}

/// <summary>Payload emitted for thread status changes.</summary>
public sealed class DocumentCommentThreadStatusRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;
}

/// <summary>Payload emitted when a comment edit is requested.</summary>
public sealed class DocumentCommentEditRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Target comment identifier.</summary>
    public string CommentId { get; set; } = string.Empty;

    /// <summary>Updated plain text body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Mentions detected in the body.</summary>
    public List<DocumentCommentMention> Mentions { get; set; } = [];
}

/// <summary>Payload emitted when a comment delete is requested.</summary>
public sealed class DocumentCommentDeleteRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Target comment identifier.</summary>
    public string CommentId { get; set; } = string.Empty;
}

/// <summary>Payload emitted when a comment reaction is toggled.</summary>
public sealed class DocumentCommentReactionToggleRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Target comment identifier.</summary>
    public string CommentId { get; set; } = string.Empty;

    /// <summary>Emoji or compact reaction value.</summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>Payload emitted when a comment selection changes.</summary>
public sealed class DocumentCommentSelectionChangeRequest
{
    /// <summary>Selected thread identifier, or null when selection is cleared.</summary>
    public string? ThreadId { get; set; }
}

/// <summary>Helper methods for document comment models.</summary>
public static class DocumentCommentHelper
{
    /// <summary>Returns true when an anchor is usable by the viewer.</summary>
    /// <param name="anchor">Anchor to validate.</param>
    public static bool IsValidAnchor(DocumentCommentAnchor? anchor)
    {
        if (anchor is null || anchor.PageNumber < 1)
        {
            return false;
        }

        if (!IsNormalized(anchor.X) || !IsNormalized(anchor.Y))
        {
            return false;
        }

        return anchor.Kind != DocumentCommentAnchorKind.Area
            || (anchor.Width > 0 && anchor.Height > 0 && IsNormalized(anchor.Width) && IsNormalized(anchor.Height));
    }

    /// <summary>Counts open threads.</summary>
    /// <param name="threads">Threads to inspect.</param>
    public static int CountOpenThreads(IEnumerable<DocumentCommentThread>? threads)
    {
        return threads?.Count(thread => thread.Status == DocumentCommentThreadStatus.Open) ?? 0;
    }

    /// <summary>Counts threads where any comment mentions a given user.</summary>
    /// <param name="threads">Threads to inspect.</param>
    /// <param name="userId">Stable user identifier.</param>
    public static int CountMentionedThreads(IEnumerable<DocumentCommentThread>? threads, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || threads is null)
        {
            return 0;
        }

        return threads.Count(thread => thread.Comments.Any(comment =>
            comment.Mentions.Any(mention => string.Equals(mention.UserId, userId, StringComparison.Ordinal))));
    }

    /// <summary>Returns true when a thread mentions a given user.</summary>
    /// <param name="thread">Thread to inspect.</param>
    /// <param name="userId">Stable user identifier.</param>
    public static bool MentionsUser(DocumentCommentThread? thread, string? userId)
    {
        return !string.IsNullOrWhiteSpace(userId)
            && thread?.Comments.Any(comment =>
                comment.Mentions.Any(mention => string.Equals(mention.UserId, userId, StringComparison.Ordinal))) == true;
    }

    private static bool IsNormalized(double value)
    {
        return double.IsFinite(value) && value >= 0 && value <= 1;
    }
}
