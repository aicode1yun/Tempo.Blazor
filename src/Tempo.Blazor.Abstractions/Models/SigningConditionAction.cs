namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Comparison action used by a signing field condition.</summary>
public enum SigningConditionAction
{
    /// <summary>Source checkbox must be checked.</summary>
    Checked,

    /// <summary>Source checkbox must be unchecked.</summary>
    Unchecked,

    /// <summary>Source value must equal the configured value.</summary>
    Equal,

    /// <summary>Source value must not equal the configured value.</summary>
    NotEqual,

    /// <summary>Source multi-value field must contain the configured value.</summary>
    Contains,

    /// <summary>Source multi-value field must not contain the configured value.</summary>
    DoesNotContain,

    /// <summary>Source field must be empty.</summary>
    Empty,

    /// <summary>Source field must not be empty.</summary>
    NotEmpty,

    /// <summary>Source numeric value must be greater than the configured value.</summary>
    GreaterThan,

    /// <summary>Source numeric value must be less than the configured value.</summary>
    LessThan
}
