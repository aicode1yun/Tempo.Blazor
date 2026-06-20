namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Logical operation joining a condition with the previous condition.</summary>
public enum SigningConditionOperation
{
    /// <summary>Both conditions must match.</summary>
    And,

    /// <summary>At least one condition must match.</summary>
    Or
}
