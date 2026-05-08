using System.Collections;
using System.Globalization;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Builds the linear signing step plan from template fields and current values.</summary>
public static class SigningStepPlanner
{
    /// <summary>Plans signing steps and overlay fields for the provided field set.</summary>
    public static SigningStepPlan Plan(
        IReadOnlyList<SigningField> fields,
        IReadOnlyList<SigningDocumentPage>? pages = null,
        IReadOnlyDictionary<string, object?>? values = null,
        string? submitterUuid = null)
    {
        var pageOrder = BuildPageOrder(pages);
        var visibleFields = fields
            .Where(field => BelongsToSubmitter(field, submitterUuid))
            .Where(field => IsVisible(field, values))
            .Select(field => new PlannedField(field, GetPrimaryArea(field), GetSortKey(field, GetPrimaryArea(field), pageOrder)))
            .OrderBy(item => item.SortKey.Document)
            .ThenBy(item => item.SortKey.Page)
            .ThenBy(item => item.SortKey.Y)
            .ThenBy(item => item.SortKey.X)
            .ThenBy(item => item.Field.Uuid, StringComparer.Ordinal)
            .ToList();

        var overlayFields = visibleFields
            .SelectMany(item => item.Field.Areas.Count == 0
                ? []
                : item.Field.Areas.Select(area => new SigningStepOverlayItem
                {
                    Field = item.Field,
                    Area = area
                }))
            .ToArray();

        var steps = new List<SigningStepItem>();
        for (var index = 0; index < visibleFields.Count; index++)
        {
            var item = visibleFields[index];
            if (!IsInteractiveStepField(item.Field))
            {
                continue;
            }

            if (item.Field.Type == SigningFieldType.Checkbox)
            {
                var group = new List<PlannedField> { item };
                while (index + 1 < visibleFields.Count && visibleFields[index + 1].Field.Type == SigningFieldType.Checkbox)
                {
                    group.Add(visibleFields[++index]);
                }

                if (group.Count > 1)
                {
                    steps.Add(CreateStep(group[0], group.Select(field => field.Field).ToArray(), isCheckboxGroup: true));
                    continue;
                }
            }

            steps.Add(CreateStep(item, [item.Field], isCheckboxGroup: false));
        }

        return new SigningStepPlan
        {
            Steps = steps,
            OverlayFields = overlayFields
        };
    }

    private static SigningStepItem CreateStep(PlannedField item, IReadOnlyList<SigningField> fields, bool isCheckboxGroup)
    {
        return new SigningStepItem
        {
            Field = item.Field,
            Fields = fields,
            Area = item.Area,
            IsCheckboxGroup = isCheckboxGroup,
            AppearsOn = item.Area is null ? null : string.Create(CultureInfo.InvariantCulture, $"Page {item.Area.Page + 1}")
        };
    }

    private static bool BelongsToSubmitter(SigningField field, string? submitterUuid)
    {
        return string.IsNullOrWhiteSpace(submitterUuid)
            || string.IsNullOrWhiteSpace(field.SubmitterUuid)
            || string.Equals(field.SubmitterUuid, submitterUuid, StringComparison.Ordinal);
    }

    private static bool IsInteractiveStepField(SigningField field)
    {
        if (field.Type is SigningFieldType.Heading or SigningFieldType.Strikethrough)
        {
            return false;
        }

        if (field.ReadOnly && !string.IsNullOrWhiteSpace(field.Preferences.Formula))
        {
            return false;
        }

        return !field.ReadOnly;
    }

    private static SigningFieldArea? GetPrimaryArea(SigningField field)
    {
        return field.Areas
            .OrderBy(area => area.Page)
            .ThenBy(area => area.Y)
            .ThenBy(area => area.X)
            .FirstOrDefault();
    }

    private static SortKey GetSortKey(SigningField field, SigningFieldArea? area, IReadOnlyDictionary<string, int> pageOrder)
    {
        if (area is null)
        {
            return new SortKey(int.MaxValue, int.MaxValue, double.MaxValue, double.MaxValue);
        }

        var pageKey = $"{area.AttachmentUuid ?? string.Empty}:{area.Page}";
        var document = pageOrder.TryGetValue(pageKey, out var order) ? order : int.MaxValue;
        return new SortKey(document, area.Page, area.Y, area.X);
    }

    private static IReadOnlyDictionary<string, int> BuildPageOrder(IReadOnlyList<SigningDocumentPage>? pages)
    {
        if (pages is null || pages.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        return pages
            .Select((page, index) => new { Key = $"{page.AttachmentUuid}:{page.PageIndex}", Index = index })
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.Ordinal);
    }

    private static bool IsVisible(SigningField field, IReadOnlyDictionary<string, object?>? values)
    {
        if (field.Conditions.Count == 0)
        {
            return true;
        }

        bool? result = null;
        foreach (var condition in field.Conditions)
        {
            var matches = Matches(condition, values);
            result = result is null
                ? matches
                : condition.Operation == SigningConditionOperation.Or
                    ? result.Value || matches
                    : result.Value && matches;
        }

        return result.GetValueOrDefault();
    }

    private static bool Matches(SigningFieldCondition condition, IReadOnlyDictionary<string, object?>? values)
    {
        values ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        values.TryGetValue(condition.FieldUuid, out var value);

        return condition.Action switch
        {
            SigningConditionAction.Checked => value is bool boolValue && boolValue,
            SigningConditionAction.Unchecked => value is not bool boolValue || !boolValue,
            SigningConditionAction.Equal => string.Equals(FormatValue(value), condition.Value, StringComparison.Ordinal),
            SigningConditionAction.NotEqual => !string.Equals(FormatValue(value), condition.Value, StringComparison.Ordinal),
            SigningConditionAction.Contains => GetValues(value).Contains(condition.Value ?? string.Empty, StringComparer.Ordinal),
            SigningConditionAction.DoesNotContain => !GetValues(value).Contains(condition.Value ?? string.Empty, StringComparer.Ordinal),
            SigningConditionAction.Empty => string.IsNullOrWhiteSpace(FormatValue(value)),
            SigningConditionAction.NotEmpty => !string.IsNullOrWhiteSpace(FormatValue(value)),
            SigningConditionAction.GreaterThan => TryDecimal(value, out var number) && TryDecimal(condition.Value, out var compare) && number > compare,
            SigningConditionAction.LessThan => TryDecimal(value, out var number) && TryDecimal(condition.Value, out var compare) && number < compare,
            _ => true
        };
    }

    private static IEnumerable<string> GetValues(object? value)
    {
        if (value is null)
        {
            return [];
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? [] : [text];
        }

        if (value is IEnumerable enumerable)
        {
            return enumerable.Cast<object?>().Select(FormatValue).Where(text => !string.IsNullOrWhiteSpace(text));
        }

        return [FormatValue(value)];
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static bool TryDecimal(object? value, out decimal result)
    {
        return decimal.TryParse(FormatValue(value), NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private sealed record PlannedField(SigningField Field, SigningFieldArea? Area, SortKey SortKey);

    private sealed record SortKey(int Document, int Page, double Y, double X);
}
