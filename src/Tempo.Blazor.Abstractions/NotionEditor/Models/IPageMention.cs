namespace Tempo.Blazor.NotionEditor.Models;

public interface IPageMention : IInlineMention
{
    Guid PageId { get; }
    string PageTitle { get; }
    string? PageIconEmoji { get; }
}
