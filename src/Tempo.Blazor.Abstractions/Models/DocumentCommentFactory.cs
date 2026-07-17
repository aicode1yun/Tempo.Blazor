namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Builds document comment threads and entries from create/reply requests.</summary>
public static class DocumentCommentFactory
{
    /// <summary>Creates a new open thread with a single initial comment.</summary>
    /// <param name="request">Anchor and body for the new thread.</param>
    /// <param name="author">Author of the initial comment.</param>
    /// <param name="createdAt">Creation timestamp; defaults to now when omitted.</param>
    public static DocumentCommentThread CreateThread(
        DocumentCommentThreadCreateRequest request,
        DocumentCommentUser author,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(author);

        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        return new DocumentCommentThread
        {
            Id = Guid.NewGuid().ToString("N"),
            Anchor = request.Anchor,
            Status = DocumentCommentThreadStatus.Open,
            Comments = [CreateComment(request.Body, request.Mentions, author, timestamp)],
            Kind = request.Kind,
            Color = request.Color,
            StampText = request.StampText,
            InkStrokes = request.InkStrokes is null ? [] : [.. request.InkStrokes]
        };
    }

    /// <summary>Creates a reply comment for an existing thread.</summary>
    /// <param name="request">Target thread and reply body.</param>
    /// <param name="author">Author of the reply.</param>
    /// <param name="createdAt">Creation timestamp; defaults to now when omitted.</param>
    public static DocumentComment CreateReply(
        DocumentCommentReplyRequest request,
        DocumentCommentUser author,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(author);

        return CreateComment(request.Body, request.Mentions, author, createdAt ?? DateTimeOffset.UtcNow);
    }

    private static DocumentComment CreateComment(
        string body,
        List<DocumentCommentMention> mentions,
        DocumentCommentUser author,
        DateTimeOffset createdAt)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            AuthorId = author.UserId,
            AuthorName = string.IsNullOrWhiteSpace(author.DisplayName) ? author.UserId : author.DisplayName,
            AuthorAvatarUrl = author.AvatarUrl,
            Body = body ?? string.Empty,
            CreatedAt = createdAt,
            Mentions = mentions is null ? [] : [.. mentions],
            CanEdit = true,
            CanDelete = true
        };
}
