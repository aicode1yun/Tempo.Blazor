namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Shared immutable activity or audit entry linked to a Tempo entity.</summary>
public sealed class TmActivityEntry
{
    /// <summary>Stable activity entry identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Entity this activity entry belongs to.</summary>
    public TmEntityRef EntityRef { get; set; } = new();

    /// <summary>User that performed the action, when known.</summary>
    public TmUserRef? Actor { get; set; }

    /// <summary>Action key, for example <c>create</c>, <c>edit</c>, or <c>restore-version</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the activity occurred.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional human-readable summary.</summary>
    public string? Summary { get; set; }

    /// <summary>Optional human-readable state before the change.</summary>
    public string? Before { get; set; }

    /// <summary>Optional human-readable state after the change.</summary>
    public string? After { get; set; }

    /// <summary>Optional diff payload or summary.</summary>
    public string? Diff { get; set; }

    /// <summary>Optional correlation identifier used to group related activity entries.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Returns true when required identity fields are populated.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(Id)
        && EntityRef.IsValid
        && !string.IsNullOrWhiteSpace(Action);
}
