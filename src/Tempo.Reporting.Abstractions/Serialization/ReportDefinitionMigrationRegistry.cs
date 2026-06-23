using System.Text.Json.Nodes;
using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Reporting.Abstractions.Serialization;

/// <summary>Registry that applies report definition JSON migrations in schema-version order.</summary>
public sealed class ReportDefinitionMigrationRegistry
{
    private readonly Dictionary<int, IReportDefinitionMigration> _migrations;

    /// <summary>Empty migration registry. Current-version JSON passes through unchanged.</summary>
    public static ReportDefinitionMigrationRegistry Empty { get; } = new([]);

    /// <summary>Creates a registry from adjacent migrations.</summary>
    public ReportDefinitionMigrationRegistry(IEnumerable<IReportDefinitionMigration> migrations)
    {
        _migrations = migrations.ToDictionary(m => m.SourceSchemaVersion);
    }

    /// <summary>Migrates a definition JSON object to the requested schema version.</summary>
    public JsonObject Migrate(JsonObject definition, int targetSchemaVersion = ReportDefinition.CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var current = ReadSchemaVersion(definition);
        if (current > targetSchemaVersion)
        {
            throw new ReportDefinitionJsonException(
                $"Unsupported report definition schema version {current}. Expected {targetSchemaVersion} or lower.");
        }

        var working = (JsonObject)definition.DeepClone();
        while (current < targetSchemaVersion)
        {
            if (!_migrations.TryGetValue(current, out var migration) ||
                migration.TargetSchemaVersion <= migration.SourceSchemaVersion)
            {
                throw new ReportDefinitionJsonException(
                    $"No report definition migration from schema version {current} to {current + 1} is registered.");
            }

            working = migration.Migrate(working);
            current = ReadSchemaVersion(working);
        }

        return working;
    }

    private static int ReadSchemaVersion(JsonObject definition)
    {
        if (!definition.TryGetPropertyValue("schemaVersion", out var value) ||
            value is null ||
            !value.GetValueKind().Equals(System.Text.Json.JsonValueKind.Number) ||
            !value.AsValue().TryGetValue<int>(out var schemaVersion))
        {
            throw new ReportDefinitionJsonException("Report definition JSON is missing numeric schemaVersion.");
        }

        return schemaVersion;
    }
}
