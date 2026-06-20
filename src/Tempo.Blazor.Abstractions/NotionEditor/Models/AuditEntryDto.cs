namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Single immutable audit record for a Notion workspace action.</summary>
public sealed class AuditEntryDto
{
    /// <summary>Stable audit entry identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the audited action occurred.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>User identifier that performed the action.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Human-readable user name displayed in the audit log.</summary>
    public string UserDisplayName { get; set; } = string.Empty;

    /// <summary>Action key such as create, edit, delete, move, or restrict.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Audited target type, for example page or block.</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>Audited target identifier.</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Provider-specific details for the audited action.</summary>
    public IReadOnlyDictionary<string, string> Details { get; set; } = new Dictionary<string, string>();
}

/// <summary>Filter used when querying Notion audit entries.</summary>
public sealed class AuditLogFilter
{
    /// <summary>User id filter. Null or empty includes all users.</summary>
    public string? UserId { get; set; }

    /// <summary>Action key filter. Null or empty includes all actions.</summary>
    public string? Action { get; set; }

    /// <summary>Target type filter. Null or empty includes all target types.</summary>
    public string? TargetType { get; set; }

    /// <summary>Target id filter. Null or empty includes all target ids.</summary>
    public string? TargetId { get; set; }

    /// <summary>Inclusive start date filter.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Inclusive end date filter.</summary>
    public DateOnly? To { get; set; }
}

/// <summary>Paging settings for querying Notion audit entries.</summary>
public sealed class NotionAuditQuery
{
    /// <summary>Number of matching entries to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Maximum number of matching entries to return.</summary>
    public int Take { get; set; } = 20;
}
