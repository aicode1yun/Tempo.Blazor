namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IFormulaFieldConfig : IFieldConfig
{
    string Expression { get; }
    DatabaseFieldType ResultType { get; }
}
