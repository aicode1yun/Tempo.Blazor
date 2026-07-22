#pragma warning disable MA0048

using System.Text.Json.Serialization;

namespace Tempo.Reporting.Abstractions.Definitions.Rdl;

/// <summary>Severity of an <see cref="RdlImportDiagnostic"/>.</summary>
public enum RdlDiagnosticSeverity
{
    /// <summary>A non-fatal condition: an RDL construct was skipped or approximated during import. The
    /// resulting <see cref="ReportDefinition"/> is still usable.</summary>
    Warning,

    /// <summary>A fatal condition: the RDL was malformed or could not be interpreted. The resulting
    /// definition is a placeholder and must not be persisted.</summary>
    Error,
}

/// <summary>A single diagnostic produced while importing an RDL document.</summary>
/// <param name="Severity">Whether the condition is fatal (<see cref="RdlDiagnosticSeverity.Error"/>) or
/// a lossy approximation (<see cref="RdlDiagnosticSeverity.Warning"/>).</param>
/// <param name="ElementPath">A slash-delimited path to the RDL element the diagnostic refers to (e.g.
/// <c>Report/Body/ReportItems/Tablix[Sales]</c>), or <c>Report</c> for document-level conditions.</param>
/// <param name="Message">A human-readable, culture-invariant description of the condition.</param>
public sealed record RdlImportDiagnostic(RdlDiagnosticSeverity Severity, string ElementPath, string Message);

/// <summary>Outcome of importing an RDL document into a <see cref="ReportDefinition"/>.</summary>
/// <param name="Definition">The mapped report definition. When <see cref="HasErrors"/> is <see langword="true"/>
/// this is a minimal placeholder and must not be persisted.</param>
/// <param name="Diagnostics">Every condition encountered during import. Nothing is dropped silently: any RDL
/// element or attribute that was not mapped produces at least a warning here.</param>
public sealed record RdlImportResult(ReportDefinition Definition, IReadOnlyList<RdlImportDiagnostic> Diagnostics)
{
    /// <summary>True when at least one <see cref="RdlDiagnosticSeverity.Error"/> diagnostic was produced.</summary>
    [JsonIgnore]
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == RdlDiagnosticSeverity.Error);

    /// <summary>The subset of <see cref="Diagnostics"/> with <see cref="RdlDiagnosticSeverity.Error"/> severity.</summary>
    [JsonIgnore]
    public IReadOnlyList<RdlImportDiagnostic> Errors =>
        [.. Diagnostics.Where(diagnostic => diagnostic.Severity == RdlDiagnosticSeverity.Error)];

    /// <summary>The subset of <see cref="Diagnostics"/> with <see cref="RdlDiagnosticSeverity.Warning"/> severity.</summary>
    [JsonIgnore]
    public IReadOnlyList<RdlImportDiagnostic> Warnings =>
        [.. Diagnostics.Where(diagnostic => diagnostic.Severity == RdlDiagnosticSeverity.Warning)];
}

#pragma warning restore MA0048
