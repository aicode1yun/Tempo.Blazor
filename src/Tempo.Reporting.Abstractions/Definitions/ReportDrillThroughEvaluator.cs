using System.Globalization;
using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Reporting.Abstractions.Definitions;

/// <summary>Resolved drill-through target and mapped parameter values ready for viewer navigation.</summary>
public sealed record ReportDrillThroughResolution
{
    /// <summary>Folder-qualified path of the target report, when the action targeted a path.</summary>
    public string? TargetReportPath { get; init; }

    /// <summary>Identifier of the target report, when the action targeted an identifier.</summary>
    public string? TargetReportId { get; init; }

    /// <summary>Mapped parameter values keyed by target parameter name.</summary>
    public IReadOnlyDictionary<string, string?> Parameters { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>Whether a usable target report reference was resolved.</summary>
    public bool HasTarget
        => !string.IsNullOrWhiteSpace(TargetReportPath) || !string.IsNullOrWhiteSpace(TargetReportId);
}

/// <summary>Evaluates <see cref="ReportDrillThroughAction"/> parameter mappings against a clicked context.</summary>
public static class ReportDrillThroughEvaluator
{
    /// <summary>
    /// Resolves the drill-through target and evaluates each parameter mapping against the clicked data
    /// point <paramref name="context"/> and the current report's <paramref name="currentParameters"/>.
    /// </summary>
    public static ReportDrillThroughResolution Resolve(
        ReportDrillThroughAction action,
        IReadOnlyDictionary<string, string?>? context = null,
        IReadOnlyDictionary<string, ReportParameterValue>? currentParameters = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var mapping in action.ParameterMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.ParameterName))
            {
                continue;
            }

            parameters[mapping.ParameterName] = mapping.SourceKind switch
            {
                ReportDrillThroughSourceKind.Static => mapping.Source,
                ReportDrillThroughSourceKind.Field => ResolveField(context, mapping.Source),
                ReportDrillThroughSourceKind.Parameter => ResolveParameter(currentParameters, mapping.Source),
                _ => null,
            };
        }

        return new ReportDrillThroughResolution
        {
            TargetReportPath = action.TargetReportPath,
            TargetReportId = action.TargetReportId,
            Parameters = parameters,
        };
    }

    private static string? ResolveField(IReadOnlyDictionary<string, string?>? context, string field)
        => context is not null && context.TryGetValue(field, out var value) ? value : null;

    private static string? ResolveParameter(
        IReadOnlyDictionary<string, ReportParameterValue>? currentParameters,
        string parameterName)
        => currentParameters is not null && currentParameters.TryGetValue(parameterName, out var value)
            ? Convert.ToString(value.ScalarValue, CultureInfo.InvariantCulture)
            : null;
}
