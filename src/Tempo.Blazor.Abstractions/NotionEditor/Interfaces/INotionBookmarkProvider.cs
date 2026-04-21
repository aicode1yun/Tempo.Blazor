namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

public interface INotionBookmarkProvider
{
    Task<IBookmarkBlockContent> ResolveBookmarkAsync(string url);
}
