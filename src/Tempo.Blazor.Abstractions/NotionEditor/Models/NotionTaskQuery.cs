namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Filter and paging options for querying aggregated Notion tasks.</summary>
public sealed class NotionTaskQuery
{
    /// <summary>Optional assigned user id filter.</summary>
    public string? AssigneeId { get; set; }

    /// <summary>Includes completed tasks when true.</summary>
    public bool IncludeCompleted { get; set; }

    /// <summary>Inclusive upper due-date bound.</summary>
    public DateTime? DueBefore { get; set; }

    /// <summary>Inclusive lower due-date bound.</summary>
    public DateTime? DueAfter { get; set; }

    /// <summary>Optional source page id filter.</summary>
    public string? PageId { get; set; }

    /// <summary>Number of filtered records to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Maximum number of records to return.</summary>
    public int Take { get; set; } = 50;
}
