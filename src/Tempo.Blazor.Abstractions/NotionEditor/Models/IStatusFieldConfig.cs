namespace Tempo.Blazor.NotionEditor.Models;

public interface IStatusFieldConfig : IFieldConfig
{
    IReadOnlyList<StatusGroup> Groups { get; }
}
