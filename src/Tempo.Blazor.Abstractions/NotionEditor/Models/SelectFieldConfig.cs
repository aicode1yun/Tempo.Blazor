namespace Tempo.Blazor.NotionEditor.Models;

public class SelectFieldConfig : ISelectFieldConfig
{
    public IReadOnlyList<SelectFieldOption> Options { get; set; } = new List<SelectFieldOption>();
}
