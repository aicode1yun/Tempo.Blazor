namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Represents a foreign key relationship parsed from SQL DDL.</summary>
public sealed class SqlForeignKeyDefinition
{
    public string ColumnName { get; set; } = string.Empty;
    public string ReferenceTable { get; set; } = string.Empty;
    public string ReferenceColumn { get; set; } = string.Empty;
}
