using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Renders a numeric signing step with min, max, step, and required validation.</summary>
public partial class TmSigningNumberStep
{
    private string? _validationMessage;

    /// <summary>Signing field represented by this step.</summary>
    [Parameter] public SigningField Field { get; set; } = new() { Type = SigningFieldType.Number };

    /// <summary>Current numeric value.</summary>
    [Parameter] public decimal? Value { get; set; }

    /// <summary>Callback invoked when the numeric value changes.</summary>
    [Parameter] public EventCallback<decimal?> ValueChanged { get; set; }

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

    private string? FormattedValue => Value?.ToString(CultureInfo.InvariantCulture);

    private string PlaceholderText => SigningLocalizationResolver.ResolveFieldPlaceholder(Field, Culture, FallbackCulture, Loc["TmSigningStep_NumberPlaceholder"]);

    private string ShellClass => string.Join(" ", new[] { "tm-signing-number-step", Class }.Where(item => !string.IsNullOrWhiteSpace(item)));

    private async Task HandleValueChangedAsync(ChangeEventArgs args)
    {
        var text = args.Value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            _validationMessage = Field.Required
                ? SigningLocalizationResolver.ResolveValidationMessage(Field.Validation, Culture, FallbackCulture, Loc["TmSigningStep_Required"])
                : null;
            await ValueChanged.InvokeAsync(null);
            return;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            _validationMessage = Loc["TmSigningStep_InvalidNumber"];
            await ValueChanged.InvokeAsync(null);
            return;
        }

        _validationMessage = Validate(number);
        await ValueChanged.InvokeAsync(number);
    }

    private string? Validate(decimal number)
    {
        if (TryParseDecimal(Field.Validation?.Min, out var min) && number < min)
        {
            return SigningLocalizationResolver.ResolveValidationMessage(Field.Validation, Culture, FallbackCulture, Loc["TmSigningStep_MinValue", min]);
        }

        if (TryParseDecimal(Field.Validation?.Max, out var max) && number > max)
        {
            return SigningLocalizationResolver.ResolveValidationMessage(Field.Validation, Culture, FallbackCulture, Loc["TmSigningStep_MaxValue", max]);
        }

        return null;
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }
}
