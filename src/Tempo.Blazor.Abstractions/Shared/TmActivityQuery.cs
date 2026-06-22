namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Query options for resolving activity entries from an <see cref="ITmActivityProvider"/>.</summary>
public sealed class TmActivityQuery
{
    /// <summary>Optional exact entity reference filter.</summary>
    public TmEntityRef? EntityRef { get; set; }

    /// <summary>Optional entity type filter.</summary>
    public string? EntityType { get; set; }

    /// <summary>Optional entity id filter.</summary>
    public string? EntityId { get; set; }

    /// <summary>Optional exact actor id filter.</summary>
    public string? ActorId { get; set; }

    /// <summary>Optional free-text filter matched against actor id, display name, and summary.</summary>
    public string? SearchText { get; set; }

    /// <summary>Optional action key filter.</summary>
    public string? Action { get; set; }

    /// <summary>Optional correlation id filter.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Inclusive lower timestamp bound.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Inclusive upper timestamp bound.</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>Number of matching entries to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Maximum number of matching entries to return.</summary>
    public int Take { get; set; } = 20;
}
