namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Basic notification preferences for assignment, mention, and deadline workflows.</summary>
public sealed class TmNotificationPreferences
{
    /// <summary>Whether assignment notifications should also be delivered by email.</summary>
    public bool EmailOnAssign { get; set; }

    /// <summary>Whether mention notifications should also be delivered by email.</summary>
    public bool EmailOnMention { get; set; }

    /// <summary>Whether assignment notifications should be delivered as push notifications.</summary>
    public bool PushOnAssign { get; set; }

    /// <summary>Whether mention notifications should be delivered as push notifications.</summary>
    public bool PushOnMention { get; set; }

    /// <summary>Whether deadline notifications should be delivered as push notifications.</summary>
    public bool PushOnDeadline { get; set; }
}
