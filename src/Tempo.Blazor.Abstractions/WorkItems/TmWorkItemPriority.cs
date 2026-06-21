namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// Unified priority level shared by all task-bearing components.
/// Providers may additionally carry a provider-native label in
/// <see cref="TmWorkItem.PriorityLabel"/>.
/// </summary>
public enum TmWorkItemPriority
{
    /// <summary>Highest urgency.</summary>
    Highest,

    /// <summary>High urgency.</summary>
    High,

    /// <summary>Default urgency.</summary>
    Medium,

    /// <summary>Low urgency.</summary>
    Low,

    /// <summary>Lowest urgency.</summary>
    Lowest
}
