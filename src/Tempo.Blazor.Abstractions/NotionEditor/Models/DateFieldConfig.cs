namespace Tempo.Blazor.NotionEditor.Models;

public class DateFieldConfig : IDateFieldConfig
{
    public string DateFormat { get; set; } = string.Empty;
    public string? TimeFormat { get; set; }
    public bool IncludeTime { get; set; }
}
