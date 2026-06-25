namespace Tempo.Blazor.Reporting.Models;

/// <summary>Resolved parameter option displayed by the viewer parameter panel.</summary>
public sealed record ReportViewerParameterOption
{
    /// <summary>Creates an empty option.</summary>
    public ReportViewerParameterOption()
    {
    }

    /// <summary>Creates an option.</summary>
    public ReportViewerParameterOption(string value, string label)
    {
        Value = value;
        Label = label;
    }

    /// <summary>Serialized parameter value.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>Display label.</summary>
    public string Label { get; init; } = string.Empty;
}
