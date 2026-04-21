namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface INumberFieldConfig : IFieldConfig
{
    NumberFormat Format { get; }
}
