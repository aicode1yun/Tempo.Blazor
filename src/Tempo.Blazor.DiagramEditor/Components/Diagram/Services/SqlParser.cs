using System.Text.RegularExpressions;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Parses SQL DDL statements into structured table definitions.</summary>
public static class SqlParser
{
    public static List<SqlTableDefinition> Parse(string sql)
    {
        var tables = new List<SqlTableDefinition>();
        if (string.IsNullOrWhiteSpace(sql))
            return tables;

        sql = RemoveComments(sql);
        var dialect = DetectDialect(sql);

        var createTableRegex = new Regex(
            @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?" +
            @"(?:\[(?<schema2>[^\]\s\.]+)\]\.|`?(?<schema>[^`\s\.]+)`?\.)?" +
            @"(?:\[(?<table2>[^\]\s\(]+)\]|`?(?<table>[^`\s\(]+)`?|""(?<table3>[^""\s\(]+)"")\s*\((?<body>.*?)\)\s*(?:;|GO\s*$|ENGINE|CHARSET|TABLESPACE|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in createTableRegex.Matches(sql))
        {
            var tableName = match.Groups["table"].Success ? match.Groups["table"].Value :
                            match.Groups["table2"].Success ? match.Groups["table2"].Value :
                            match.Groups["table3"].Value;
            var body = match.Groups["body"].Value;
            var table = ParseTableBody(NormalizeName(tableName), body);
            table.Dialect = dialect;
            tables.Add(table);
        }

        DetectJunctionTables(tables);
        return tables;
    }

    private static SqlDialect DetectDialect(string sql)
    {
        if (Regex.IsMatch(sql, @"\[\w+\]", RegexOptions.None))
            return SqlDialect.SqlServer;
        if (Regex.IsMatch(sql, @"`\w+`"))
            return SqlDialect.MySql;
        if (Regex.IsMatch(sql, "\"\\w+\""))
            return SqlDialect.PostgreSql;
        return SqlDialect.Generic;
    }

    private static SqlTableDefinition ParseTableBody(string tableName, string body)
    {
        var table = new SqlTableDefinition { Name = tableName };
        var lines = SplitColumns(body);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (TryParseConstraintPrimaryKey(trimmed, out var pkColumns))
            {
                foreach (var pk in pkColumns)
                {
                    if (!table.PrimaryKeys.Contains(pk))
                        table.PrimaryKeys.Add(pk);

                    var col = table.Columns.FirstOrDefault(c => c.Name == pk);
                    if (col is not null)
                        col.IsPrimaryKey = true;
                }
                continue;
            }

            if (TryParseConstraintForeignKey(trimmed, out var fk))
            {
                table.ForeignKeys.Add(fk);
                var col = table.Columns.FirstOrDefault(c => c.Name == fk.ColumnName);
                if (col is not null)
                    col.IsForeignKey = true;
                continue;
            }

            var column = TryParseColumn(trimmed);
            if (column is not null)
            {
                table.Columns.Add(column);
                if (column.IsPrimaryKey && !table.PrimaryKeys.Contains(column.Name))
                    table.PrimaryKeys.Add(column.Name);
            }
        }

        return table;
    }

    private static SqlColumnDefinition? TryParseColumn(string line)
    {
        var regex = new Regex(
            @"^(?:`?\[?(?<name>[^`\s\]\(]+)\]?`?)\s+(?<type>[A-Z_0-9]+(?:\s*\([^\)]*\))?)" +
            @"(?<rest>.*)$",
            RegexOptions.IgnoreCase);

        var match = regex.Match(line);
        if (!match.Success)
            return null;

        var name = NormalizeName(match.Groups["name"].Value);
        var dataType = match.Groups["type"].Value.Trim().ToUpperInvariant();
        var rest = match.Groups["rest"].Value.ToUpperInvariant();

        return new SqlColumnDefinition
        {
            Name = name,
            DataType = MapDataType(dataType),
            IsPrimaryKey = rest.Contains("PRIMARY KEY"),
            IsForeignKey = rest.Contains("REFERENCES"),
            IsNullable = !rest.Contains("NOT NULL"),
            IsUnique = rest.Contains("UNIQUE")
        };
    }

    private static bool TryParseConstraintPrimaryKey(string line, out List<string> columns)
    {
        columns = [];
        var regex = new Regex(
            @"(?:CONSTRAINT\s+[^\s(]+\s+)?PRIMARY\s+KEY\s*\((?<cols>[^)]+)\)",
            RegexOptions.IgnoreCase);
        var match = regex.Match(line);

        if (match.Success)
        {
            var cols = match.Groups["cols"].Value;
            columns = cols.Split(',').Select(NormalizeName).ToList();
            return true;
        }
        return false;
    }

    private static bool TryParseConstraintForeignKey(string line, out SqlForeignKeyDefinition fk)
    {
        fk = new SqlForeignKeyDefinition();
        var regex = new Regex(
            @"(?:CONSTRAINT\s+[^\s(]+\s+)?FOREIGN\s+KEY\s*\((?:`?\[?)(?<col>[^`\s\]\)]+)(?:\]?`?)\)\s*" +
            @"REFERENCES\s+(?:`?\[?)(?<refTable>[^`\s\]\(]+)(?:\]?`?)(?:\s*\((?:`?\[?)(?<refCol>[^`\s\]\)]+)(?:\]?`?)\))?",
            RegexOptions.IgnoreCase);
        var match = regex.Match(line);

        if (match.Success)
        {
            fk.ColumnName = NormalizeName(match.Groups["col"].Value);
            fk.ReferenceTable = NormalizeName(match.Groups["refTable"].Value);
            fk.ReferenceColumn = match.Groups["refCol"].Success
                ? NormalizeName(match.Groups["refCol"].Value)
                : "id";
            return true;
        }
        return false;
    }

    private static List<string> SplitColumns(string body)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        int depth = 0;
        foreach (var ch in body)
        {
            if (ch == '(') depth++;
            else if (ch == ')') depth--;
            else if (ch == ',' && depth == 0)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0)
            result.Add(current.ToString());
        return result;
    }

    private static string NormalizeName(string name)
    {
        name = name.Trim();
        if ((name.StartsWith("'") && name.EndsWith("'")) ||
            (name.StartsWith("\"") && name.EndsWith("\"")) ||
            (name.StartsWith("[") && name.EndsWith("]")) ||
            (name.StartsWith("`") && name.EndsWith("`")))
        {
            name = name[1..^1];
        }
        return name;
    }

    private static string RemoveComments(string sql)
    {
        sql = Regex.Replace(sql, @"/\*[\s\S]*?\*/", string.Empty);
        sql = Regex.Replace(sql, @"--.*$", string.Empty, RegexOptions.Multiline);
        return sql;
    }

    private static string MapDataType(string dataType)
    {
        var upper = dataType.ToUpperInvariant();
        return upper switch
        {
            var s when s.StartsWith("INT") || s.StartsWith("INTEGER") || s.StartsWith("SERIAL") || s.StartsWith("BIGINT") || s.StartsWith("SMALLINT") || s.StartsWith("TINYINT") => "integer",
            var s when s.StartsWith("VARCHAR") || s.StartsWith("NVARCHAR") || s.StartsWith("CHAR") || s.StartsWith("NCHAR") || s.StartsWith("TEXT") || s.StartsWith("NTEXT") => "string",
            var s when s.StartsWith("DECIMAL") || s.StartsWith("NUMERIC") || s.StartsWith("MONEY") || s.StartsWith("SMALLMONEY") => "decimal",
            var s when s.StartsWith("FLOAT") || s.StartsWith("REAL") || s.StartsWith("DOUBLE") => "float",
            var s when s.StartsWith("BIT") || s.StartsWith("BOOLEAN") || s.StartsWith("BOOL") => "boolean",
            var s when s.StartsWith("DATETIME") || s.StartsWith("TIMESTAMP") || s.StartsWith("DATE") || s.StartsWith("TIME") || s.StartsWith("SMALLDATETIME") => "datetime",
            var s when s.StartsWith("UNIQUEIDENTIFIER") || s.StartsWith("UUID") => "uuid",
            var s when s.StartsWith("BINARY") || s.StartsWith("VARBINARY") || s.StartsWith("IMAGE") || s.StartsWith("BLOB") => "binary",
            _ => lowerFirst(upper)
        };

        static string lowerFirst(string s) => s.Length > 0 ? char.ToLowerInvariant(s[0]) + s[1..].ToLowerInvariant() : s;
    }

    private static void DetectJunctionTables(List<SqlTableDefinition> tables)
    {
        foreach (var table in tables)
        {
            if (table.ForeignKeys.Count == 2 && table.ForeignKeys.Select(fk => fk.ReferenceTable).Distinct().Count() == 2)
            {
                var fkCols = table.ForeignKeys.Select(fk => fk.ColumnName).ToHashSet();
                var pkCols = table.PrimaryKeys.ToHashSet();

                if (pkCols.SetEquals(fkCols) || (table.Columns.Count <= 3 && pkCols.IsSupersetOf(fkCols)))
                {
                    table.IsJunctionTable = true;
                }
            }
        }
    }
}
