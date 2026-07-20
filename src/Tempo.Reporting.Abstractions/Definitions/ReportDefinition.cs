#pragma warning disable MA0016

using System.Text.Json.Serialization;

namespace Tempo.Reporting.Abstractions.Definitions;

/// <summary>Root report definition contract shared by designers, servers and embedded hosts.</summary>
public sealed record ReportDefinition
{
    /// <summary>Current report definition schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Schema version used by migration and validation.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Stable report identifier assigned by a host or server.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable report name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional report description for catalog views.</summary>
    public string? Description { get; init; }

    /// <summary>Physical page setup used by the layout engine.</summary>
    public ReportPageSetup PageSetup { get; init; } = new();

    /// <summary>
    /// Document-level default base writing direction, applied to text elements that leave their
    /// own <see cref="ReportTextDirection.Auto"/> unresolved. Defaults to
    /// <see cref="ReportTextDirection.Auto"/> so existing reports are unchanged; set to
    /// <see cref="ReportTextDirection.Rtl"/> to make a whole Arabic/Hebrew report right-to-left.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ReportTextDirection TextDirection { get; init; } = ReportTextDirection.Auto;

    /// <summary>Parameter definitions exposed before rendering.</summary>
    public List<ReportParameterDefinition> Parameters { get; init; } = [];

    /// <summary>Data set definitions consumed by processing.</summary>
    public List<ReportDataSetDefinition> DataSets { get; init; } = [];

    /// <summary>Reusable named styles.</summary>
    public List<ReportStyleDefinition> Styles { get; init; } = [];

    /// <summary>Band definitions that make up the report body.</summary>
    public ReportBandCollection Bands { get; init; } = new();
}

#pragma warning restore MA0016
