namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Result of signing formula normalization or validation.</summary>
public sealed class SigningFormulaResult
{
    /// <summary>Normalized formula text. Field tokens use stable field UUIDs.</summary>
    public string Formula { get; init; } = string.Empty;

    /// <summary>Validation errors found while processing the formula.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Whether the formula has no validation errors.</summary>
    public bool IsValid => Errors.Count == 0;
}
