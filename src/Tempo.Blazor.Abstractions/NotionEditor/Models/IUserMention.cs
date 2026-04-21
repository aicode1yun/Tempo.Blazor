namespace Tempo.Blazor.NotionEditor.Models;

public interface IUserMention : IInlineMention
{
    string UserId { get; }
    string DisplayName { get; }
    string? AvatarUrl { get; }
}
