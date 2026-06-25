using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Reporting.Engine.Snapshot;

/// <summary>Stable JSON serializer for report snapshots.</summary>
public static class ReportSnapshotJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    /// <summary>Serializes a report snapshot with deterministic property naming and enum values.</summary>
    public static string Serialize(ReportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        EnsureSupportedVersion(snapshot.SchemaVersion);
        return JsonSerializer.Serialize(snapshot, SerializerOptions);
    }

    /// <summary>Deserializes and validates a report snapshot.</summary>
    public static ReportSnapshot Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ReportSnapshotJsonException("Report snapshot JSON is empty.");
        }

        var snapshot = JsonSerializer.Deserialize<ReportSnapshot>(json, SerializerOptions)
            ?? throw new ReportSnapshotJsonException("Report snapshot JSON did not produce a snapshot.");
        EnsureSupportedVersion(snapshot.SchemaVersion);
        return snapshot;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter<ReportSnapshotCommandType>(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static void EnsureSupportedVersion(int schemaVersion)
    {
        if (schemaVersion != ReportSnapshot.CurrentSchemaVersion)
        {
            throw new ReportSnapshotJsonException($"Unsupported report snapshot schema version {schemaVersion}. Expected {ReportSnapshot.CurrentSchemaVersion}.");
        }
    }
}
