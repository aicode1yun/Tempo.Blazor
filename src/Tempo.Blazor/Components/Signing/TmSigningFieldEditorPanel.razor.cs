using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Edits signing field settings, options, validation, preferences, conditions, and formulas.</summary>
public partial class TmSigningFieldEditorPanel
{
    private SigningField? _field;
    private SigningField? _lastField;
    private bool _showConditions;
    private bool _showFormula;

    /// <summary>Selected signing field edited by the panel.</summary>
    [Parameter] public SigningField? Field { get; set; }

    /// <summary>Callback invoked whenever the edited field changes.</summary>
    [Parameter] public EventCallback<SigningField> FieldChanged { get; set; }

    /// <summary>All fields in the current signing template, used by condition and formula builders.</summary>
    [Parameter] public IReadOnlyList<SigningField> Fields { get; set; } = [];

    /// <summary>Submitter roles available for assigning the edited field.</summary>
    [Parameter] public IReadOnlyList<SigningSubmitterRole> SubmitterRoles { get; set; } = [];

    /// <summary>Whether the panel should prevent edits.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Callback invoked when a radio or multiple option should be mapped to a document area.</summary>
    [Parameter] public EventCallback<TmSigningFieldOptionAreaMappingEventArgs> OptionAreaMappingRequested { get; set; }

    /// <summary>Callback invoked when the current field should be copied to all pages.</summary>
    [Parameter] public EventCallback<SigningField> CopyToAllPagesRequested { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool HasField => _field is not null;

    private bool IsChoiceField => _field?.Type is SigningFieldType.Select or SigningFieldType.Radio or SigningFieldType.Multiple;

    private bool IsTextLikeField => _field?.Type is SigningFieldType.Text or SigningFieldType.Cells;

    private bool IsNumberField => _field?.Type is SigningFieldType.Number or SigningFieldType.Payment;

    private bool IsDateField => _field?.Type is SigningFieldType.Date or SigningFieldType.DateNow;

    private bool IsSignatureField => _field?.Type is SigningFieldType.Signature or SigningFieldType.Initials;

    private bool IsStampField => _field?.Type is SigningFieldType.Stamp;

    private bool CanEditFormula => _field?.Type is SigningFieldType.Number or SigningFieldType.Payment;

    private string RootClass
    {
        get
        {
            var classes = new List<string> { "tm-signing-field-editor-panel" };
            AddClass(classes, ReadOnly, "tm-signing-field-editor-panel--readonly");
            AddClass(classes, !HasField, "tm-signing-field-editor-panel--empty");

            if (!string.IsNullOrWhiteSpace(Class))
            {
                classes.Add(Class);
            }

            return string.Join(" ", classes);
        }
    }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_lastField, Field))
        {
            _field = Field is null ? null : Clone(Field);
            _lastField = Field;
            _showConditions = false;
            _showFormula = false;
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

    private Task UpdateFieldAsync(Action<SigningField> update)
    {
        if (ReadOnly || _field is null)
        {
            return Task.CompletedTask;
        }

        update(_field);
        return FieldChanged.InvokeAsync(Clone(_field));
    }

    private Task UpdatePreferencesAsync(Action<SigningFieldPreferences> update)
    {
        return UpdateFieldAsync(field =>
        {
            field.Preferences ??= new SigningFieldPreferences();
            update(field.Preferences);
        });
    }

    private Task UpdateValidationAsync(Action<SigningFieldValidation> update)
    {
        return UpdateFieldAsync(field =>
        {
            field.Validation ??= new SigningFieldValidation();
            update(field.Validation);
        });
    }

    private Task HandleTypeChangedAsync(ChangeEventArgs args)
    {
        return Enum.TryParse<SigningFieldType>(args.Value?.ToString(), out var type)
            ? UpdateFieldAsync(field => field.Type = type)
            : Task.CompletedTask;
    }

    private Task HandleTextPropertyChangedAsync(ChangeEventArgs args, Action<SigningField, string?> update)
    {
        return UpdateFieldAsync(field => update(field, NormalizeOptional(args.Value?.ToString())));
    }

    private Task HandleBoolPropertyChangedAsync(ChangeEventArgs args, Action<SigningField, bool> update)
    {
        return UpdateFieldAsync(field => update(field, ToBool(args.Value)));
    }

    private Task HandleSubmitterChangedAsync(ChangeEventArgs args)
    {
        return UpdateFieldAsync(field => field.SubmitterUuid = NormalizeOptional(args.Value?.ToString()));
    }

    private Task AddOptionAsync()
    {
        return UpdateFieldAsync(field =>
        {
            field.Options.Add(new SigningFieldOption
            {
                Value = Loc["TmSigningFieldEditorPanel_NewOption", field.Options.Count + 1]
            });
        });
    }

    private Task UpdateOptionValueAsync(string optionUuid, ChangeEventArgs args)
    {
        return UpdateFieldAsync(field =>
        {
            var option = field.Options.FirstOrDefault(item => string.Equals(item.Uuid, optionUuid, StringComparison.Ordinal));
            if (option is not null)
            {
                option.Value = args.Value?.ToString() ?? string.Empty;
            }
        });
    }

    private Task RemoveOptionAsync(string optionUuid)
    {
        return UpdateFieldAsync(field =>
        {
            field.Options.RemoveAll(option => string.Equals(option.Uuid, optionUuid, StringComparison.Ordinal));
            if (string.Equals(field.DefaultValue?.ToString(), optionUuid, StringComparison.Ordinal))
            {
                field.DefaultValue = null;
            }
        });
    }

    private Task MoveOptionAsync(string optionUuid, int delta)
    {
        return UpdateFieldAsync(field =>
        {
            var index = field.Options.FindIndex(option => string.Equals(option.Uuid, optionUuid, StringComparison.Ordinal));
            var target = index + delta;
            if (index < 0 || target < 0 || target >= field.Options.Count)
            {
                return;
            }

            (field.Options[index], field.Options[target]) = (field.Options[target], field.Options[index]);
        });
    }

    private Task HandleDefaultValueChangedAsync(ChangeEventArgs args)
    {
        return UpdateFieldAsync(field => field.DefaultValue = NormalizeOptional(args.Value?.ToString()));
    }

    private Task RequestOptionAreaMappingAsync(SigningFieldOption option)
    {
        return _field is null
            ? Task.CompletedTask
            : OptionAreaMappingRequested.InvokeAsync(new TmSigningFieldOptionAreaMappingEventArgs
            {
                Field = Clone(_field),
                Option = Clone(option)
            });
    }

    private Task HandleValidationModeChangedAsync(ChangeEventArgs args)
    {
        var mode = args.Value?.ToString();
        if (string.Equals(mode, "None", StringComparison.Ordinal))
        {
            return UpdateFieldAsync(field => field.Validation = null);
        }

        return UpdateValidationAsync(_ => { });
    }

    private Task HandleValidationValueChangedAsync(ChangeEventArgs args, Action<SigningFieldValidation, string?> update)
    {
        return UpdateValidationAsync(validation => update(validation, NormalizeOptional(args.Value?.ToString())));
    }

    private Task HandleFontSizeChangedAsync(ChangeEventArgs args)
    {
        return UpdatePreferencesAsync(preferences =>
        {
            preferences.FontSize = double.TryParse(args.Value?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        });
    }

    private Task HandlePreferenceTextChangedAsync(ChangeEventArgs args, Action<SigningFieldPreferences, string?> update)
    {
        return UpdatePreferencesAsync(preferences => update(preferences, NormalizeOptional(args.Value?.ToString())));
    }

    private Task HandlePreferenceBoolChangedAsync(ChangeEventArgs args, Action<SigningFieldPreferences, bool?> update)
    {
        return UpdatePreferencesAsync(preferences => update(preferences, ToBool(args.Value)));
    }

    private Task HandleConditionsChangedAsync(IReadOnlyList<SigningFieldCondition> conditions)
    {
        return UpdateFieldAsync(field =>
        {
            field.Conditions = conditions.Select(Clone).ToList();
        });
    }

    private Task HandleFormulaSavedAsync(string formula)
    {
        return UpdateFieldAsync(field =>
        {
            field.Preferences.Formula = formula;
            field.ReadOnly = true;
        });
    }

    private Task CopyToAllPagesAsync()
    {
        return _field is null || ReadOnly
            ? Task.CompletedTask
            : CopyToAllPagesRequested.InvokeAsync(Clone(_field));
    }

    private void ToggleConditions()
    {
        if (!ReadOnly)
        {
            _showConditions = !_showConditions;
        }
    }

    private void ToggleFormula()
    {
        if (!ReadOnly)
        {
            _showFormula = !_showFormula;
        }
    }

    private string GetPanelTitle()
    {
        return string.IsNullOrWhiteSpace(_field?.Name)
            ? Loc["TmSigningFieldEditorPanel_Title"]
            : _field!.Name!;
    }

    private string GetValidationMode()
    {
        return string.IsNullOrWhiteSpace(_field?.Validation?.Pattern) ? "None" : "Regex";
    }

    private static bool ToBool(object? value)
    {
        return value is bool boolValue && boolValue;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
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
            SigningFieldType.Payment => Loc["TmSigning_Field_Payment"],
            _ => type.ToString()
        };
    }

    private static SigningField Clone(SigningField field)
    {
        return new SigningField
        {
            Uuid = field.Uuid,
            SubmitterUuid = field.SubmitterUuid,
            Name = field.Name,
            Title = field.Title,
            Description = field.Description,
            Type = field.Type,
            Required = field.Required,
            ReadOnly = field.ReadOnly,
            Prefillable = field.Prefillable,
            DefaultValue = field.DefaultValue,
            Preferences = Clone(field.Preferences),
            Validation = field.Validation is null ? null : Clone(field.Validation),
            Conditions = field.Conditions.Select(Clone).ToList(),
            Options = field.Options.Select(Clone).ToList(),
            Areas = field.Areas.Select(Clone).ToList()
        };
    }

    private static SigningFieldPreferences Clone(SigningFieldPreferences preferences)
    {
        return new SigningFieldPreferences
        {
            Color = preferences.Color,
            Align = preferences.Align,
            Format = preferences.Format,
            FontFamily = preferences.FontFamily,
            FontSize = preferences.FontSize,
            WithSignatureId = preferences.WithSignatureId,
            WithLogo = preferences.WithLogo,
            ReasonFieldUuid = preferences.ReasonFieldUuid,
            Formula = preferences.Formula,
            Currency = preferences.Currency,
            Price = preferences.Price,
            PriceId = preferences.PriceId,
            PaymentLinkId = preferences.PaymentLinkId,
            AdditionalSettings = new Dictionary<string, object?>(preferences.AdditionalSettings)
        };
    }

    private static SigningFieldValidation Clone(SigningFieldValidation validation)
    {
        return new SigningFieldValidation
        {
            Pattern = validation.Pattern,
            Message = validation.Message,
            Min = validation.Min,
            Max = validation.Max,
            Step = validation.Step
        };
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

    private static SigningFieldOption Clone(SigningFieldOption option)
    {
        return new SigningFieldOption
        {
            Uuid = option.Uuid,
            Value = option.Value
        };
    }

    private static SigningFieldArea Clone(SigningFieldArea area)
    {
        return new SigningFieldArea
        {
            Uuid = area.Uuid,
            AttachmentUuid = area.AttachmentUuid,
            Page = area.Page,
            X = area.X,
            Y = area.Y,
            Width = area.Width,
            Height = area.Height,
            OptionUuid = area.OptionUuid
        };
    }
}
