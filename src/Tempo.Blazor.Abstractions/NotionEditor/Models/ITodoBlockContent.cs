namespace Tempo.Blazor.NotionEditor.Models;

public interface ITodoBlockContent : ITextBlockContent
{
    bool IsChecked { get; }
    string? AssigneeId { get; }
    string? AssigneeDisplayName { get; }
    DateTime? DueDate { get; }
    bool IsOverdue { get; }
}
