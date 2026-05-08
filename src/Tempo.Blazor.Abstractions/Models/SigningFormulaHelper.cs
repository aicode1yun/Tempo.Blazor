using System.Text.RegularExpressions;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Helpers for signing formula field tokens and dependency validation.</summary>
public static class SigningFormulaHelper
{
    private static readonly Regex TokenRegex = new(@"\{\{\s*(?<token>[^{}]+?)\s*\}\}", RegexOptions.Compiled);

    /// <summary>Replaces stable field UUID tokens in a formula with user-facing field labels.</summary>
    public static string Humanize(string? formula, IReadOnlyList<SigningField> fields)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return string.Empty;
        }

        return TokenRegex.Replace(formula, match =>
        {
            var token = GetTokenValue(match);
            var field = ResolveField(token, fields);
            return field is null
                ? match.Value
                : "{{" + GetFieldLabel(field) + "}}";
        });
    }

    /// <summary>Replaces field label tokens in a formula with stable field UUID tokens.</summary>
    public static SigningFormulaResult Normalize(string? formula, IReadOnlyList<SigningField> fields)
    {
        var source = formula ?? string.Empty;
        var errors = new List<string>();

        var normalized = TokenRegex.Replace(source, match =>
        {
            var token = GetTokenValue(match);
            var field = ResolveField(token, fields);
            if (field is null)
            {
                errors.Add($"Unknown formula field '{token}'.");
                return match.Value;
            }

            return "{{" + field.Uuid + "}}";
        });

        return new SigningFormulaResult
        {
            Formula = normalized,
            Errors = errors
        };
    }

    /// <summary>Normalizes a formula and validates that it does not introduce a field dependency cycle.</summary>
    public static SigningFormulaResult Validate(string? formula, IReadOnlyList<SigningField> fields, string? currentFieldUuid)
    {
        var normalized = Normalize(formula, fields);
        var errors = normalized.Errors.ToList();

        if (!string.IsNullOrWhiteSpace(currentFieldUuid)
            && CreatesCycle(normalized.Formula, fields, currentFieldUuid))
        {
            errors.Add("This formula would create a dependency cycle.");
        }

        return new SigningFormulaResult
        {
            Formula = normalized.Formula,
            Errors = errors
        };
    }

    /// <summary>Extracts formula token contents without surrounding braces.</summary>
    public static IReadOnlyList<string> ExtractTokens(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return [];
        }

        return TokenRegex
            .Matches(formula)
            .Select(match => GetTokenValue(match))
            .ToArray();
    }

    /// <summary>Gets a user-facing label for a signing field.</summary>
    public static string GetFieldLabel(SigningField field)
    {
        if (!string.IsNullOrWhiteSpace(field.Name))
        {
            return field.Name;
        }

        if (!string.IsNullOrWhiteSpace(field.Title))
        {
            return field.Title;
        }

        return field.Uuid;
    }

    private static bool CreatesCycle(string formula, IReadOnlyList<SigningField> fields, string currentFieldUuid)
    {
        foreach (var token in ExtractTokens(formula))
        {
            if (DependsOnCurrentField(token, fields, currentFieldUuid, []))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DependsOnCurrentField(
        string sourceFieldUuid,
        IReadOnlyList<SigningField> fields,
        string currentFieldUuid,
        HashSet<string> visited)
    {
        if (string.Equals(sourceFieldUuid, currentFieldUuid, StringComparison.Ordinal))
        {
            return true;
        }

        if (!visited.Add(sourceFieldUuid))
        {
            return false;
        }

        var sourceField = fields.FirstOrDefault(field => string.Equals(field.Uuid, sourceFieldUuid, StringComparison.Ordinal));
        if (sourceField?.Preferences.Formula is null)
        {
            return false;
        }

        var normalized = Normalize(sourceField.Preferences.Formula, fields);
        foreach (var token in ExtractTokens(normalized.Formula))
        {
            if (DependsOnCurrentField(token, fields, currentFieldUuid, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static SigningField? ResolveField(string token, IReadOnlyList<SigningField> fields)
    {
        return fields.FirstOrDefault(field => string.Equals(field.Uuid, token, StringComparison.Ordinal))
            ?? fields.FirstOrDefault(field => string.Equals(GetFieldLabel(field), token, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetTokenValue(Match match)
    {
        return match.Groups["token"].Value.Trim();
    }
}
