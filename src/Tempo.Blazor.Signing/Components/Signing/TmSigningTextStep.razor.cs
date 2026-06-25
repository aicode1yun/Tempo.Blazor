using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Renders a text-like signing step with optional multiline, cells, and regex validation.</summary>
public partial class TmSigningTextStep
{
    private readonly string _validationId = $"tm-signing-text-validation-{Guid.NewGuid():N}";
    private string? _validationMessage;

    /// <summary>Signing field represented by this step.</summary>
    [Parameter] public SigningField Field { get; set; } = new();

    /// <summary>Current text value.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Callback invoked when the text value changes.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Document area used to infer cells maxlength.</summary>
    [Parameter] public SigningFieldArea? Area { get; set; }

    /// <summary>Short text describing where the field appears in the document.</summary>
    [Parameter] public string? AppearsOn { get; set; }

    /// <summary>Whether the input is disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Culture used to resolve localized field text.</summary>
    [Parameter] public string? Culture { get; set; }

    /// <summary>Fallback culture used when localized field text is missing.</summary>
    [Parameter] public string? FallbackCulture { get; set; }

    /// <summary>Additional CSS classes for the shell element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the shell element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsMultiline => string.Equals(Field.Preferences.Format, "multiline", StringComparison.OrdinalIgnoreCase)
        || TryGetBoolSetting("multiline");

    private int? MaxLength => Field.Type == SigningFieldType.Cells
        ? GetCellCount()
        : TryGetIntSetting("maxLength");

    private string? CurrentValidationMessage => _validationMessage;

    private string? ValidationDescriptionId => CurrentValidationMessage is null ? null : _validationId;

    private string PlaceholderText => SigningLocalizationResolver.ResolveFieldPlaceholder(Field, Culture, FallbackCulture, Loc["TmSigningStep_TextPlaceholder"]);

    private string ShellClass => string.Join(" ", new[] { "tm-signing-text-step", Class }.Where(item => !string.IsNullOrWhiteSpace(item)));

    private async Task HandleValueChangedAsync(ChangeEventArgs args)
    {
        var value = args.Value?.ToString();
        _validationMessage = Validate(value);
        await ValueChanged.InvokeAsync(value);
    }

    private string? Validate(string? value)
    {
        if (Field.Required && string.IsNullOrWhiteSpace(value))
        {
            return SigningLocalizationResolver.ResolveValidationMessage(Field.Validation, Culture, FallbackCulture, Loc["TmSigningStep_Required"]);
        }

        if (!string.IsNullOrWhiteSpace(value)
            && !string.IsNullOrWhiteSpace(Field.Validation?.Pattern)
            && !Regex.IsMatch(value, Field.Validation.Pattern))
        {
            return SigningLocalizationResolver.ResolveValidationMessage(Field.Validation, Culture, FallbackCulture, Loc["TmSigningStep_InvalidPattern"]);
        }

        return null;
    }

    private int? GetCellCount()
    {
        if (Area is { CellWidth: > 0, Width: > 0 } area)
        {
            var cellWidth = area.CellWidth.GetValueOrDefault();
            return Math.Max(1, (int)Math.Floor(area.Width / cellWidth));
        }

        return TryGetIntSetting("cells");
    }

    private bool TryGetBoolSetting(string key)
    {
        return Field.Preferences.AdditionalSettings.TryGetValue(key, out var value)
            && value is bool boolValue
            && boolValue;
    }

    private int? TryGetIntSetting(string key)
    {
        if (!Field.Preferences.AdditionalSettings.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            string text when int.TryParse(text, out var result) => result,
            _ => null
        };
    }
}
