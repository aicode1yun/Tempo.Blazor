namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Represents a column parsed from SQL DDL.</summary>
public sealed class SqlColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public bool IsNullable { get; set; } = true;
    public bool IsUnique { get; set; }
}
