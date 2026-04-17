namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Represents a user-selected mapping from CSV column to diagram semantic field.</summary>
public sealed class CsvColumnMapping
{
    public string SemanticField { get; set; } = string.Empty;
    public string SelectedColumn { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}
