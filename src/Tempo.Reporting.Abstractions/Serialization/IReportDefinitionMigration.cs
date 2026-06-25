using System.Text.Json.Nodes;

namespace Tempo.Reporting.Abstractions.Serialization;

/// <summary>Migrates a report definition JSON object between adjacent schema versions.</summary>
public interface IReportDefinitionMigration
{
    /// <summary>Source schema version.</summary>
    int SourceSchemaVersion { get; }

    /// <summary>Target schema version.</summary>
    int TargetSchemaVersion { get; }

    /// <summary>Migrates the supplied JSON object.</summary>
    JsonObject Migrate(JsonObject definition);
}
