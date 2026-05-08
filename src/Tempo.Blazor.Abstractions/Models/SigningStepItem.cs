namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Single linear signing step produced from one field or a group of adjacent checkbox fields.</summary>
public sealed class SigningStepItem
{
    /// <summary>Primary field represented by this step.</summary>
    public required SigningField Field { get; init; }

    /// <summary>Fields represented by this step. Contains multiple fields for checkbox groups.</summary>
    public IReadOnlyList<SigningField> Fields { get; init; } = [];

    /// <summary>Primary document area represented by this step.</summary>
    public SigningFieldArea? Area { get; init; }

    /// <summary>Whether this step represents multiple adjacent checkbox fields.</summary>
    public bool IsCheckboxGroup { get; init; }

    /// <summary>Human-readable document location for the step.</summary>
    public string? AppearsOn { get; init; }
}
