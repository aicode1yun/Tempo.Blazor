namespace Tempo.Blazor.NotionEditor.Models;

public class StatusFieldConfig : IStatusFieldConfig
{
    public IReadOnlyList<StatusGroup> Groups { get; set; } = new List<StatusGroup>();
}
