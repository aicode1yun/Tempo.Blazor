using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Provider-agnostic placeholder step for verification, KBA, and payment flows.</summary>
public partial class TmSigningExternalStep
{
    private readonly Dictionary<string, string?> _answers = [];

    /// <summary>Signing field represented by this step.</summary>
    [Parameter] public SigningField Field { get; set; } = new() { Type = SigningFieldType.Verification };

    /// <summary>Whether the external provider operation is loading.</summary>
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>Error message shown for the external provider operation.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>External URL for provider-hosted verification or checkout.</summary>
    [Parameter] public string? ExternalUrl { get; set; }

    /// <summary>KBA questions to answer inline.</summary>
    [Parameter] public IReadOnlyList<string> Questions { get; set; } = [];

    /// <summary>Callback invoked when KBA answers change.</summary>
    [Parameter] public EventCallback<IReadOnlyDictionary<string, string?>> AnswersChanged { get; set; }

    /// <summary>Callback invoked when verification or KBA should start.</summary>
    [Parameter] public EventCallback<SigningField> OnStart { get; set; }

    /// <summary>Callback invoked when payment checkout should start.</summary>
    [Parameter] public EventCallback<SigningField> OnCheckout { get; set; }

    /// <summary>Short text describing where the field appears in the document.</summary>
    [Parameter] public string? AppearsOn { get; set; }

    /// <summary>Whether the controls are disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Additional CSS classes for the shell element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the shell element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string ShellClass => string.Join(" ", new[] { "tm-signing-external-step", Class }.Where(item => !string.IsNullOrWhiteSpace(item)));

    private string PaymentAmount
    {
        get
        {
            var currency = string.IsNullOrWhiteSpace(Field.Preferences.Currency) ? "USD" : Field.Preferences.Currency;
            var price = Field.Preferences.Price ?? 0m;
            return string.Create(CultureInfo.InvariantCulture, $"{currency} {price:0.00}");
        }
    }

    private Task StartAsync() => OnStart.InvokeAsync(Field);

    private Task CheckoutAsync() => OnCheckout.InvokeAsync(Field);

    private string? GetAnswer(string question)
    {
        return _answers.TryGetValue(question, out var answer) ? answer : null;
    }

    private async Task HandleAnswerChangedAsync(string question, ChangeEventArgs args)
    {
        _answers[question] = args.Value?.ToString();
        await AnswersChanged.InvokeAsync(_answers);
    }
}
