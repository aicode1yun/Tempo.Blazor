namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class DatabaseField : IDatabaseField
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DatabaseFieldType Type { get; set; }
    public bool IsPrimary { get; set; }
    public IFieldConfig? Config { get; set; }
    public bool IsVisible { get; set; } = true;
    public int? Width { get; set; }
}
