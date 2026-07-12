namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Configuration for the periodic notification digest.</summary>
public sealed class TmNotificationDigestOptions
{
    /// <summary>Whether the digest background service is enabled. Default <c>true</c>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the digest runs. Default 24 hours.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Minimum number of items required before a digest is sent to a recipient. Default 1.</summary>
    public int MinItems { get; set; } = 1;

    /// <summary>When true, the digest includes only unread notifications. Default <c>true</c>.</summary>
    public bool UnreadOnly { get; set; } = true;
}
