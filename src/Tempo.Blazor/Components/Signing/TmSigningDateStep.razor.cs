using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Renders a date, month, or date-time signing step.</summary>
public partial class TmSigningDateStep
{
    private string? _validationMessage;

    /// <summary>Signing field represented by this step.</summary>
    [Parameter] public SigningField Field { get; set; } = new() { Type = SigningFieldType.Date };

    /// <summary>Current date value formatted for the selected input type.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Callback invoked when the date value changes.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Short text describing where the field appears in the document.</summary>
    [Parameter] public string? AppearsOn { get; set; }

    /// <summary>Whether the input is disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Whether to show the button that fills today's date.</summary>
    [Parameter] public bool ShowTodayButton { get; set; } = true;

    /// <summary>Additional CSS classes for the shell element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the shell element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string InputType => Field.Preferences.Format?.ToLowerInvariant() switch
    {
        "month" => "month",
        "datetime" or "datetime-local" => "datetime-local",
        _ => "date"
    };

    private string ShellClass => string.Join(" ", new[] { "tm-signing-date-step", Class }.Where(item => !string.IsNullOrWhiteSpace(item)));

    private async Task HandleValueChangedAsync(ChangeEventArgs args)
    {
        var value = args.Value?.ToString();
        _validationMessage = Field.Required && string.IsNullOrWhiteSpace(value)
            ? Loc["TmSigningStep_Required"]
            : null;
        await ValueChanged.InvokeAsync(value);
    }

    private Task SetTodayAsync()
    {
        var today = DateTime.Today;
        var value = InputType switch
        {
            "month" => today.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            "datetime-local" => today.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            _ => today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        _validationMessage = null;
        return ValueChanged.InvokeAsync(value);
    }

    private string? NormalizeDateToken(string? value)
    {
        if (string.Equals(value, "{{date}}", StringComparison.OrdinalIgnoreCase))
        {
            return InputType == "month"
                ? DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                : DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return value;
    }
}
