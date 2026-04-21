namespace Tempo.Blazor.NotionEditor.Models;

public interface IMentionUser
{
    string UserId { get; }
    string DisplayName { get; }
    string? AvatarUrl { get; }
    string? Email { get; }
}
