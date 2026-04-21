namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IDatabaseField
{
    Guid Id { get; }
    string Name { get; }
    DatabaseFieldType Type { get; }
    bool IsPrimary { get; }
    IFieldConfig? Config { get; }
    bool IsVisible { get; }
    int? Width { get; }
}
