using System.Text.Json.Serialization;

namespace Tempo.Reporting.Engine.Snapshot;

/// <summary>Immutable page snapshot consumed by reporting canvas and export renderers.</summary>
public sealed class ReportSnapshot
{
    /// <summary>Current snapshot schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Version of the serialized snapshot schema.</summary>
    [JsonPropertyOrder(0)]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Stable snapshot identifier.</summary>
    [JsonPropertyOrder(1)]
    public string SnapshotId { get; init; } = string.Empty;

    /// <summary>Snapshot pages in print order.</summary>
    [JsonPropertyOrder(2)]
    public List<ReportSnapshotPage> Pages { get; init; } = [];
}
