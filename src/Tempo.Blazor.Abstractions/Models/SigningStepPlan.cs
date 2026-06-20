namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Linear signing step plan and document overlay items for a signing ceremony.</summary>
public sealed class SigningStepPlan
{
    /// <summary>Interactive steps shown to the signer.</summary>
    public IReadOnlyList<SigningStepItem> Steps { get; init; } = [];

    /// <summary>Fields rendered over document pages, including read-only computed fields.</summary>
    public IReadOnlyList<SigningStepOverlayItem> OverlayFields { get; init; } = [];
}
