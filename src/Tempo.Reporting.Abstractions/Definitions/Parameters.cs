#pragma warning disable MA0016, MA0048

namespace Tempo.Reporting.Abstractions.Definitions;

/// <summary>Report parameter data type.</summary>
public enum ReportParameterType
{
    /// <summary>String parameter.</summary>
    String,

    /// <summary>Numeric parameter.</summary>
    Number,

    /// <summary>Date or date-time parameter.</summary>
    Date,

    /// <summary>Boolean parameter.</summary>
    Boolean,

    /// <summary>List parameter with available values.</summary>
    List,
}

/// <summary>Report parameter definition.</summary>
public sealed record ReportParameterDefinition
{
    /// <summary>Stable parameter name used by expressions and APIs.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional label for UI consumers.</summary>
    public string? Label { get; init; }

    /// <summary>Parameter data type.</summary>
    public ReportParameterType DataType { get; init; } = ReportParameterType.String;

    /// <summary>Default value expression.</summary>
    public string? DefaultExpression { get; init; }

    /// <summary>Available values for list-style parameters.</summary>
    public ReportParameterAvailableValues? AvailableValues { get; init; }

    /// <summary>Whether the parameter accepts multiple values.</summary>
    public bool AllowMultipleValues { get; init; }

    /// <summary>Whether the parameter is hidden from user-facing editors/viewers.</summary>
    public bool Hidden { get; init; }

    /// <summary>Whether a value is required.</summary>
    public bool Required { get; init; } = true;
}

/// <summary>Available values source kind.</summary>
public enum ReportParameterAvailableValuesKind
{
    /// <summary>Available values are declared statically.</summary>
    Static,

    /// <summary>Available values come from a data set.</summary>
    DataSet,
}

/// <summary>Available values for a report parameter.</summary>
public sealed record ReportParameterAvailableValues
{
    /// <summary>Available values source kind.</summary>
    public ReportParameterAvailableValuesKind Kind { get; init; } = ReportParameterAvailableValuesKind.Static;

    /// <summary>Static values.</summary>
    public List<ReportParameterAvailableValue> StaticValues { get; init; } = [];

    /// <summary>Data set name for dynamic values.</summary>
    public string? DataSetName { get; init; }

    /// <summary>Field or expression that produces the value.</summary>
    public string? ValueField { get; init; }

    /// <summary>Field or expression that produces the label.</summary>
    public string? LabelField { get; init; }

    /// <summary>Creates a static available values source.</summary>
    public static ReportParameterAvailableValues Static(IEnumerable<ReportParameterAvailableValue> values)
        => new()
        {
            Kind = ReportParameterAvailableValuesKind.Static,
            StaticValues = values.ToList(),
        };

    /// <summary>Creates a data set backed available values source.</summary>
    public static ReportParameterAvailableValues FromDataSet(string dataSetName, string valueField, string? labelField = null)
        => new()
        {
            Kind = ReportParameterAvailableValuesKind.DataSet,
            DataSetName = dataSetName,
            ValueField = valueField,
            LabelField = labelField,
        };
}

/// <summary>Static parameter value option.</summary>
public sealed record ReportParameterAvailableValue
{
    /// <summary>Creates an empty value option.</summary>
    public ReportParameterAvailableValue()
    {
    }

    /// <summary>Creates a value option.</summary>
    public ReportParameterAvailableValue(string value, string? label = null)
    {
        Value = value;
        Label = label;
    }

    /// <summary>Serialized parameter value.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>Optional display label.</summary>
    public string? Label { get; init; }
}

#pragma warning restore MA0016, MA0048
