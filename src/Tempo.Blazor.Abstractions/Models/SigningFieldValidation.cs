namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Validation settings for a signing field.</summary>
public class SigningFieldValidation
{
    /// <summary>Regular expression pattern for text-like fields.</summary>
    public string? Pattern { get; set; }

    /// <summary>Custom validation message displayed when validation fails.</summary>
    public string? Message { get; set; }

    /// <summary>Minimum numeric/date value or minimum length depending on field type.</summary>
    public string? Min { get; set; }

    /// <summary>Maximum numeric/date value or maximum length depending on field type.</summary>
    public string? Max { get; set; }

    /// <summary>Numeric input step.</summary>
    public string? Step { get; set; }
}
