namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Field and area pair rendered as an overlay on a signing document page.</summary>
public sealed class SigningStepOverlayItem
{
    /// <summary>Signing field rendered on the page.</summary>
    public required SigningField Field { get; init; }

    /// <summary>Field area rendered on the page.</summary>
    public required SigningFieldArea Area { get; init; }
}
