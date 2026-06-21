namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// A person (or virtual resource) assigned to a <see cref="TmWorkItem"/>.
/// Unifies the previous <c>TmWorkItemAssignee</c> and the mention-user concept so the
/// same person can be referenced across Gantt, Notion tasks and the scheduler.
/// </summary>
public sealed class TmWorkItemAssignee
{
    /// <summary>Stable user identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional avatar URL.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Optional e-mail address.</summary>
    public string? Email { get; set; }

    /// <summary>Hourly billing rate for cost tracking. Null = not specified.</summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>When true, this is a generic placeholder resource (not a real user account).</summary>
    public bool IsVirtual { get; set; }

    /// <summary>Optional CSS color used to tint the person in timelines/avatars.</summary>
    public string? Color { get; set; }
}
