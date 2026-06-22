using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Demo.Shared;

public record CommentDto(
    string Id,
    string AuthorName,
    string? AuthorAvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string HtmlContent,
    bool CanEdit,
    bool CanDelete)
{
    public TmCommentEntry ToCommentEntry()
        => new()
        {
            Id = Id,
            ThreadId = Id,
            Author = new TmUserRef
            {
                DisplayName = AuthorName,
                AvatarUrl = AuthorAvatarUrl
            },
            CreatedAt = CreatedAt,
            EditedAt = UpdatedAt,
            Body = HtmlContent,
            BodyFormat = TmCommentBodyFormat.Html,
            CanEdit = CanEdit,
            CanDelete = CanDelete
        };
}
