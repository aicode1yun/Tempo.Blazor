namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Represents a table parsed from SQL DDL.</summary>
public sealed class SqlTableDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<SqlColumnDefinition> Columns { get; set; } = [];
    public List<string> PrimaryKeys { get; set; } = [];
    public List<SqlForeignKeyDefinition> ForeignKeys { get; set; } = [];

    /// <summary>Detected SQL dialect of the source statement.</summary>
    public SqlDialect Dialect { get; set; } = SqlDialect.Generic;

    /// <summary>True if this table is a junction table for M:N relationships.</summary>
    public bool IsJunctionTable { get; set; }
}

/// <summary>Supported SQL dialects for parsing.</summary>
public enum SqlDialect
{
    Generic,
    MySql,
    PostgreSql,
    SqlServer
}
