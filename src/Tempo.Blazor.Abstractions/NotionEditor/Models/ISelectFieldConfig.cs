namespace Tempo.Blazor.NotionEditor.Models;

public interface ISelectFieldConfig : IFieldConfig
{
    IReadOnlyList<SelectFieldOption> Options { get; }
}
