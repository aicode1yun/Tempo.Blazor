namespace Tempo.Blazor.NotionEditor.Models;

public interface IDateMention : IInlineMention
{
    DateTime Date { get; }
    DateTime? EndDate { get; }
    string? TimeZone { get; }
    bool IncludeTime { get; }
}
