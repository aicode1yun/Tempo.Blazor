#pragma warning disable MA0016, MA0048

namespace Tempo.Reporting.Abstractions.Definitions;

/// <summary>Supported field data type in a report data set schema.</summary>
public enum ReportDataFieldType
{
    /// <summary>String field.</summary>
    String,

    /// <summary>Numeric field.</summary>
    Number,

    /// <summary>Date or date-time field.</summary>
    Date,

    /// <summary>Boolean field.</summary>
    Boolean,

    /// <summary>Opaque object field.</summary>
    Object,
}

/// <summary>Reference to a named data source registered by the host or server.</summary>
public sealed record ReportDataSourceReference
{
    /// <summary>Named data source identifier.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>Data set definition consumed by report processing.</summary>
public sealed record ReportDataSetDefinition
{
    /// <summary>Data set name used in expressions.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Named data source reference.</summary>
    public ReportDataSourceReference? Source { get; init; }

    /// <summary>Provider-specific query text.</summary>
    public string? Query { get; init; }

    /// <summary>Declared fields.</summary>
    public List<ReportDataSetField> Fields { get; init; } = [];

    /// <summary>Parameter bindings passed to the provider.</summary>
    public List<ReportDataSetParameterBinding> Parameters { get; init; } = [];
}

/// <summary>Declared data set field.</summary>
public sealed record ReportDataSetField
{
    /// <summary>Creates an empty field.</summary>
    public ReportDataSetField()
    {
    }

    /// <summary>Creates a field.</summary>
    public ReportDataSetField(string name, ReportDataFieldType dataType)
    {
        Name = name;
        DataType = dataType;
    }

    /// <summary>Field name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Field data type.</summary>
    public ReportDataFieldType DataType { get; init; } = ReportDataFieldType.String;
}

/// <summary>Data set query parameter binding.</summary>
public sealed record ReportDataSetParameterBinding
{
    /// <summary>Creates an empty binding.</summary>
    public ReportDataSetParameterBinding()
    {
    }

    /// <summary>Creates a parameter binding.</summary>
    public ReportDataSetParameterBinding(string name, string expression)
    {
        Name = name;
        Expression = expression;
    }

    /// <summary>Provider parameter name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Report expression that produces the parameter value.</summary>
    public string Expression { get; init; } = string.Empty;
}

#pragma warning restore MA0016, MA0048
