namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Concrete implementation of <see cref="ICommentReaction"/>.</summary>
public class CommentReaction : ICommentReaction
{
    public required string Emoji { get; set; }
    public List<string> UserIds { get; set; } = new();

    IReadOnlyList<string> ICommentReaction.UserIds => UserIds;
}
