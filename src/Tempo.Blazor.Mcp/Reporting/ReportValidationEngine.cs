using FluentValidation.Results;
using Microsoft.Extensions.Localization;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Resources;
using Tempo.Reporting.Abstractions.Serialization;
using Tempo.Reporting.Abstractions.Validation;

namespace Tempo.Blazor.Mcp.Reporting;

/// <summary>Report definition parsing and validation helpers used by MCP tools.</summary>
public static class ReportValidationEngine
{
    /// <summary>Parses and validates a report definition JSON payload.</summary>
    public static ReportValidationResult ValidateJson(string definitionJson)
    {
        if (!TryDeserialize(definitionJson, out var definition, out var parseError) || definition is null)
        {
            return new ReportValidationResult(false, [parseError ?? "Report definition JSON could not be parsed."], null);
        }

        return Validate(definition);
    }

    /// <summary>Validates a report definition instance.</summary>
    public static ReportValidationResult Validate(ReportDefinition definition)
    {
        var fluentResult = new ReportDefinitionValidator(new ReportingMcpValidationLocalizer()).Validate(definition);
        var errors = fluentResult.Errors.Select(Format).ToArray();
        return new ReportValidationResult(fluentResult.IsValid, errors, definition);
    }

    /// <summary>Attempts to deserialize a report definition.</summary>
    public static bool TryDeserialize(string definitionJson, out ReportDefinition? definition, out string? error)
    {
        definition = null;
        error = null;
        try
        {
            definition = ReportDefinitionJsonSerializer.Deserialize(definitionJson);
            return true;
        }
        catch (ReportDefinitionJsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string Format(ValidationFailure failure)
        => string.IsNullOrWhiteSpace(failure.ErrorCode)
            ? $"{failure.PropertyName}: {failure.ErrorMessage}"
            : $"{failure.PropertyName}: {failure.ErrorCode}: {failure.ErrorMessage}";

    private sealed class ReportingMcpValidationLocalizer : IStringLocalizer<ReportingValidationResources>
    {
        public LocalizedString this[string name]
            => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}

/// <summary>Validation result for report definitions.</summary>
public sealed record ReportValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    ReportDefinition? Definition);
