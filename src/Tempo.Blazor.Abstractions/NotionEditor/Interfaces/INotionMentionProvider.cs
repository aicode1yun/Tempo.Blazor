namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

public interface INotionMentionProvider
{
    Task<IEnumerable<IMentionUser>> SearchUsersAsync(string query);
    Task<IEnumerable<INotionPage>> SearchPagesAsync(string query);
}
