namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class FormulaFieldConfig : IFormulaFieldConfig
{
    public string Expression { get; set; } = string.Empty;
    public DatabaseFieldType ResultType { get; set; }
}
