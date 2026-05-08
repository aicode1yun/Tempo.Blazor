using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Builds conditional visibility rules for signing fields.</summary>
public partial class TmConditionBuilder
{
    private static readonly SigningConditionOperation[] Operations = Enum.GetValues<SigningConditionOperation>();

    private readonly List<SigningFieldCondition> _conditions = [];
    private IReadOnlyList<SigningFieldCondition>? _lastConditions;

    /// <summary>All fields in the current signing template.</summary>
    [Parameter] public IReadOnlyList<SigningField> Fields { get; set; } = [];

    /// <summary>Field identifier whose conditions are currently edited.</summary>
    [Parameter] public string? CurrentFieldUuid { get; set; }

    /// <summary>Condition rules currently assigned to the edited field.</summary>
    [Parameter] public IReadOnlyList<SigningFieldCondition> Conditions { get; set; } = [];

    /// <summary>Callback invoked when the condition rules change.</summary>
    [Parameter] public EventCallback<IReadOnlyList<SigningFieldCondition>> ConditionsChanged { get; set; }

    /// <summary>Whether the builder controls are disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private IEnumerable<SigningField> ConditionSourceFields => Fields.Where(IsConditionSourceField);

    private bool HasValidationErrors => _conditions.Any(condition => !IsBlank(condition)
        && (IsValueMissing(condition) || CreatesCycle(condition)));

    private string RootClass
    {
        get
        {
            var classes = new List<string> { "tm-condition-builder" };
            AddClass(classes, Disabled, "tm-condition-builder--disabled");
            AddClass(classes, HasValidationErrors, "tm-condition-builder--invalid");

            if (!string.IsNullOrWhiteSpace(Class))
            {
                classes.Add(Class);
            }

            return string.Join(" ", classes);
        }
    }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_lastConditions, Conditions))
        {
            _conditions.Clear();
            _conditions.AddRange((Conditions ?? []).Select(CloneAndNormalize));

            if (_conditions.Count == 0)
            {
                _conditions.Add(CreateDefaultCondition());
            }

            _lastConditions = Conditions;
        }
    }

    private static void AddClass(List<string> classes, bool condition, string cssClass)
    {
        if (condition)
        {
            classes.Add(cssClass);
        }
    }

    private static string BoolText(bool value) => value ? "true" : "false";

    private async Task HandleFieldChangedAsync(int index, ChangeEventArgs args)
    {
        if (!TryGetCondition(index, out var condition))
        {
            return;
        }

        condition.FieldUuid = args.Value?.ToString() ?? string.Empty;
        condition.Action = GetActionsFor(GetField(condition.FieldUuid)?.Type).First();
        condition.Value = null;
        await NotifyChangedAsync();
    }

    private async Task HandleActionChangedAsync(int index, ChangeEventArgs args)
    {
        if (!TryGetCondition(index, out var condition)
            || !Enum.TryParse<SigningConditionAction>(args.Value?.ToString(), out var action))
        {
            return;
        }

        condition.Action = action;
        if (!ActionRequiresValue(action))
        {
            condition.Value = null;
        }

        await NotifyChangedAsync();
    }

    private async Task HandleValueChangedAsync(int index, ChangeEventArgs args)
    {
        if (!TryGetCondition(index, out var condition))
        {
            return;
        }

        condition.Value = string.IsNullOrWhiteSpace(args.Value?.ToString())
            ? null
            : args.Value?.ToString();

        await NotifyChangedAsync();
    }

    private async Task HandleOperationChangedAsync(int index, ChangeEventArgs args)
    {
        if (!TryGetCondition(index, out var condition)
            || !Enum.TryParse<SigningConditionOperation>(args.Value?.ToString(), out var operation))
        {
            return;
        }

        condition.Operation = operation;
        await NotifyChangedAsync();
    }

    private async Task AddConditionAsync()
    {
        _conditions.Add(CreateDefaultCondition());
        await Task.CompletedTask;
    }

    private async Task RemoveConditionAsync(int index)
    {
        if (index < 0 || index >= _conditions.Count)
        {
            return;
        }

        _conditions.RemoveAt(index);

        if (_conditions.Count == 0)
        {
            _conditions.Add(CreateDefaultCondition());
        }

        await NotifyChangedAsync();
    }

    private bool TryGetCondition(int index, out SigningFieldCondition condition)
    {
        if (index < 0 || index >= _conditions.Count)
        {
            condition = default!;
            return false;
        }

        condition = _conditions[index];
        return true;
    }

    private Task NotifyChangedAsync()
    {
        var materialized = _conditions
            .Where(condition => !IsBlank(condition))
            .Select(Clone)
            .ToArray();

        return ConditionsChanged.InvokeAsync(materialized);
    }

    private SigningFieldCondition CloneAndNormalize(SigningFieldCondition condition)
    {
        var clone = Clone(condition);
        var actions = GetActionsFor(GetField(clone.FieldUuid)?.Type);

        if (!actions.Contains(clone.Action))
        {
            clone.Action = actions.First();
            clone.Value = null;
        }

        if (!ActionRequiresValue(clone.Action))
        {
            clone.Value = null;
        }

        return clone;
    }

    private static SigningFieldCondition Clone(SigningFieldCondition condition)
    {
        return new SigningFieldCondition
        {
            FieldUuid = condition.FieldUuid,
            Action = condition.Action,
            Value = condition.Value,
            Operation = condition.Operation
        };
    }

    private static SigningFieldCondition CreateDefaultCondition()
    {
        return new SigningFieldCondition
        {
            FieldUuid = string.Empty,
            Action = SigningConditionAction.NotEmpty,
            Operation = SigningConditionOperation.And
        };
    }

    private bool IsConditionSourceField(SigningField field)
    {
        return !string.Equals(field.Uuid, CurrentFieldUuid, StringComparison.Ordinal)
            && field.Type is not SigningFieldType.Heading
            && field.Type is not SigningFieldType.Strikethrough;
    }

    private SigningField? GetField(string? fieldUuid)
    {
        return string.IsNullOrWhiteSpace(fieldUuid)
            ? null
            : Fields.FirstOrDefault(field => string.Equals(field.Uuid, fieldUuid, StringComparison.Ordinal));
    }

    private string GetFieldLabel(SigningField field)
    {
        if (!string.IsNullOrWhiteSpace(field.Name))
        {
            return field.Name;
        }

        if (!string.IsNullOrWhiteSpace(field.Title))
        {
            return field.Title;
        }

        return GetFieldTypeLabel(field.Type);
    }

    private string GetFieldTypeLabel(SigningFieldType type)
    {
        return type switch
        {
            SigningFieldType.Heading => Loc["TmSigning_Field_Heading"],
            SigningFieldType.Strikethrough => Loc["TmSigning_Field_Strikethrough"],
            SigningFieldType.Text => Loc["TmSigning_Field_Text"],
            SigningFieldType.Signature => Loc["TmSigning_Field_Signature"],
            SigningFieldType.Initials => Loc["TmSigning_Field_Initials"],
            SigningFieldType.Date or SigningFieldType.DateNow => Loc["TmSigning_Field_Date"],
            SigningFieldType.Number => Loc["TmSigning_Field_Number"],
            SigningFieldType.Image => Loc["TmSigning_Field_Image"],
            SigningFieldType.File => Loc["TmSigning_Field_File"],
            SigningFieldType.Select => Loc["TmSigning_Field_Select"],
            SigningFieldType.Checkbox => Loc["TmSigning_Field_Checkbox"],
            SigningFieldType.Multiple => Loc["TmSigning_Field_Multiple"],
            SigningFieldType.Radio => Loc["TmSigning_Field_Radio"],
            SigningFieldType.Cells => Loc["TmSigning_Field_Cells"],
            SigningFieldType.Stamp => Loc["TmSigning_Field_Stamp"],
            SigningFieldType.Phone => Loc["TmSigning_Field_Phone"],
            SigningFieldType.Verification => Loc["TmSigning_Field_Verification"],
            SigningFieldType.Kba => Loc["TmSigning_Field_Kba"],
            _ => Loc["TmSigning_Field_Text"]
        };
    }

    private static IReadOnlyList<SigningConditionAction> GetActionsFor(SigningFieldType? type)
    {
        return type switch
        {
            SigningFieldType.Checkbox =>
            [
                SigningConditionAction.Checked,
                SigningConditionAction.Unchecked
            ],
            SigningFieldType.Radio or SigningFieldType.Select =>
            [
                SigningConditionAction.Equal,
                SigningConditionAction.NotEqual
            ],
            SigningFieldType.Multiple =>
            [
                SigningConditionAction.Contains,
                SigningConditionAction.DoesNotContain
            ],
            SigningFieldType.Number =>
            [
                SigningConditionAction.Empty,
                SigningConditionAction.NotEmpty,
                SigningConditionAction.Equal,
                SigningConditionAction.NotEqual,
                SigningConditionAction.GreaterThan,
                SigningConditionAction.LessThan
            ],
            _ =>
            [
                SigningConditionAction.Empty,
                SigningConditionAction.NotEmpty
            ]
        };
    }

    private string GetActionLabel(SigningConditionAction action)
    {
        return action switch
        {
            SigningConditionAction.Checked => Loc["TmConditionBuilder_Checked"],
            SigningConditionAction.Unchecked => Loc["TmConditionBuilder_Unchecked"],
            SigningConditionAction.Equal => Loc["TmConditionBuilder_Equal"],
            SigningConditionAction.NotEqual => Loc["TmConditionBuilder_NotEqual"],
            SigningConditionAction.Contains => Loc["TmConditionBuilder_Contains"],
            SigningConditionAction.DoesNotContain => Loc["TmConditionBuilder_DoesNotContain"],
            SigningConditionAction.Empty => Loc["TmConditionBuilder_Empty"],
            SigningConditionAction.NotEmpty => Loc["TmConditionBuilder_NotEmpty"],
            SigningConditionAction.GreaterThan => Loc["TmConditionBuilder_GreaterThan"],
            SigningConditionAction.LessThan => Loc["TmConditionBuilder_LessThan"],
            _ => action.ToString()
        };
    }

    private string GetOperationLabel(SigningConditionOperation operation)
    {
        return operation switch
        {
            SigningConditionOperation.And => Loc["TmConditionBuilder_And"],
            SigningConditionOperation.Or => Loc["TmConditionBuilder_Or"],
            _ => operation.ToString()
        };
    }

    private static bool IsChoiceField(SigningField? field)
    {
        return field?.Type is SigningFieldType.Radio or SigningFieldType.Select or SigningFieldType.Multiple;
    }

    private static bool ActionRequiresValue(SigningConditionAction action)
    {
        return action is SigningConditionAction.Equal
            or SigningConditionAction.NotEqual
            or SigningConditionAction.Contains
            or SigningConditionAction.DoesNotContain
            or SigningConditionAction.GreaterThan
            or SigningConditionAction.LessThan;
    }

    private static bool IsBlank(SigningFieldCondition condition)
    {
        return string.IsNullOrWhiteSpace(condition.FieldUuid);
    }

    private static bool IsValueMissing(SigningFieldCondition condition)
    {
        return ActionRequiresValue(condition.Action)
            && string.IsNullOrWhiteSpace(condition.Value);
    }

    private IEnumerable<string> GetValidationMessages(SigningFieldCondition condition)
    {
        if (IsBlank(condition))
        {
            yield break;
        }

        if (IsValueMissing(condition))
        {
            yield return Loc["TmConditionBuilder_MissingValue"];
        }

        if (CreatesCycle(condition))
        {
            yield return Loc["TmConditionBuilder_CycleDetected"];
        }
    }

    private bool CreatesCycle(SigningFieldCondition condition)
    {
        if (string.IsNullOrWhiteSpace(CurrentFieldUuid) || string.IsNullOrWhiteSpace(condition.FieldUuid))
        {
            return false;
        }

        return DependsOnCurrentField(condition.FieldUuid, []);
    }

    private bool DependsOnCurrentField(string sourceFieldUuid, HashSet<string> visited)
    {
        if (string.Equals(sourceFieldUuid, CurrentFieldUuid, StringComparison.Ordinal))
        {
            return true;
        }

        if (!visited.Add(sourceFieldUuid))
        {
            return false;
        }

        var sourceField = GetField(sourceFieldUuid);
        if (sourceField is null)
        {
            return false;
        }

        foreach (var nestedCondition in sourceField.Conditions)
        {
            if (DependsOnCurrentField(nestedCondition.FieldUuid, visited))
            {
                return true;
            }
        }

        return false;
    }
}
