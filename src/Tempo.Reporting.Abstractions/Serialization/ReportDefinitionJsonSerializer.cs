using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Reporting.Abstractions.Serialization;

/// <summary>Canonical JSON serializer for report definitions.</summary>
public static class ReportDefinitionJsonSerializer
{
    /// <summary>Canonical serializer options used by report definition JSON.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Serializes a report definition to compact deterministic JSON.</summary>
    public static string Serialize(ReportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        EnsureSupportedVersion(definition.SchemaVersion);
        return JsonSerializer.Serialize(definition, Options);
    }

    /// <summary>Deserializes a report definition after applying registered migrations.</summary>
    public static ReportDefinition Deserialize(
        string json,
        ReportDefinitionMigrationRegistry? migrationRegistry = null)
    {
        var currentJson = MigrateToCurrentJson(json, migrationRegistry);
        try
        {
            return JsonSerializer.Deserialize<ReportDefinition>(currentJson, Options)
                ?? throw new ReportDefinitionJsonException("Report definition JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new ReportDefinitionJsonException($"Report definition JSON could not be parsed: {ex.Message}", ex);
        }
    }

    /// <summary>Migrates JSON to the current schema version and emits canonical JSON.</summary>
    public static string MigrateToCurrentJson(
        string json,
        ReportDefinitionMigrationRegistry? migrationRegistry = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ReportDefinitionJsonException("Report definition JSON is empty.");
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject
                ?? throw new ReportDefinitionJsonException("Report definition JSON root must be an object.");
        }
        catch (JsonException ex)
        {
            throw new ReportDefinitionJsonException($"Report definition JSON could not be parsed: {ex.Message}", ex);
        }

        var migrated = (migrationRegistry ?? ReportDefinitionMigrationRegistry.Empty)
            .Migrate(root, ReportDefinition.CurrentSchemaVersion);
        var definition = JsonSerializer.Deserialize<ReportDefinition>(migrated.ToJsonString(Options), Options)
            ?? throw new ReportDefinitionJsonException("Report definition JSON deserialized to null.");

        EnsureSupportedVersion(definition.SchemaVersion);
        return Serialize(definition);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private static void EnsureSupportedVersion(int schemaVersion)
    {
        if (schemaVersion != ReportDefinition.CurrentSchemaVersion)
        {
            throw new ReportDefinitionJsonException(
                $"Unsupported report definition schema version {schemaVersion}. Expected {ReportDefinition.CurrentSchemaVersion}.");
        }
    }
}
