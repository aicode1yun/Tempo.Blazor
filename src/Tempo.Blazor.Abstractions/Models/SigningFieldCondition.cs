namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Conditional rule controlling a signing field from another field value.</summary>
public class SigningFieldCondition
{
    /// <summary>Source field identifier evaluated by the condition.</summary>
    public string FieldUuid { get; set; } = string.Empty;

    /// <summary>Comparison action to evaluate.</summary>
    public SigningConditionAction Action { get; set; } = SigningConditionAction.NotEmpty;

    /// <summary>Optional comparison value or option UUID.</summary>
    public string? Value { get; set; }

    /// <summary>Logical operation joining this condition with the previous condition.</summary>
    public SigningConditionOperation Operation { get; set; } = SigningConditionOperation.And;
}
