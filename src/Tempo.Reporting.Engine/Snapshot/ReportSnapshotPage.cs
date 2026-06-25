using System.Text.Json.Serialization;

namespace Tempo.Reporting.Engine.Snapshot;

/// <summary>Single fixed-size report page containing absolute drawing commands.</summary>
public sealed class ReportSnapshotPage
{
    /// <summary>One-based page number.</summary>
    [JsonPropertyOrder(0)]
    public int PageNumber { get; init; }

    /// <summary>Page width in CSS pixels at 96 DPI.</summary>
    [JsonPropertyOrder(1)]
    public double Width { get; init; }

    /// <summary>Page height in CSS pixels at 96 DPI.</summary>
    [JsonPropertyOrder(2)]
    public double Height { get; init; }

    /// <summary>Absolute drawing commands ordered by paint sequence.</summary>
    [JsonPropertyOrder(3)]
    public List<ReportSnapshotCommand> Commands { get; init; } = [];
}
