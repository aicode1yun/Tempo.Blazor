using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Event data for mapping a choice option to a document area.</summary>
public sealed class TmSigningFieldOptionAreaMappingEventArgs
{
    /// <summary>Field that owns the option.</summary>
    public required SigningField Field { get; init; }

    /// <summary>Option requested for area mapping.</summary>
    public required SigningFieldOption Option { get; init; }
}
