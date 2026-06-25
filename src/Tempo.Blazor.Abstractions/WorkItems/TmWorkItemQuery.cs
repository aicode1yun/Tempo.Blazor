namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>Unified filter and paging options for querying <see cref="TmWorkItem"/>s.</summary>
public sealed class TmWorkItemQuery
{
    /// <summary>Optional source/provider key filter (registry lookup). Null queries the resolved provider as-is.</summary>
    public string? SourceKey { get; set; }

    /// <summary>Free-text search string.</summary>
    public string? FreeText { get; set; }

    /// <summary>Specific work item ids to resolve.</summary>
    public IReadOnlyList<string> Ids { get; set; } = [];

    /// <summary>Optional assigned user id filter.</summary>
    public string? AssigneeId { get; set; }

    /// <summary>Optional parent id filter (children of a given item).</summary>
    public string? ParentId { get; set; }

    /// <summary>Optional source page id filter (e.g. Notion page).</summary>
    public string? OriginPageId { get; set; }

    /// <summary>Optional status filter.</summary>
    public TmWorkItemStatus? Status { get; set; }

    /// <summary>Includes completed items when true.</summary>
    public bool IncludeCompleted { get; set; }

    /// <summary>Inclusive lower due-date bound.</summary>
    public DateTime? DueAfter { get; set; }

    /// <summary>Inclusive upper due-date bound.</summary>
    public DateTime? DueBefore { get; set; }

    /// <summary>Items overlapping this range (scheduling queries). Inclusive lower bound.</summary>
    public DateTime? RangeStart { get; set; }

    /// <summary>Items overlapping this range (scheduling queries). Inclusive upper bound.</summary>
    public DateTime? RangeEnd { get; set; }

    /// <summary>Opaque provider-native query string (e.g. Jira JQL).</summary>
    public string? QueryString { get; set; }

    /// <summary>Number of matching items to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Maximum number of items to return.</summary>
    public int Take { get; set; } = 50;
}
