namespace Tempo.Blazor.NotionEditor.Models;

public interface IDateFieldConfig : IFieldConfig
{
    string DateFormat { get; }
    string? TimeFormat { get; }
    bool IncludeTime { get; }
}
