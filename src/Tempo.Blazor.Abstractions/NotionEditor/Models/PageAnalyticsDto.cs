namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Analytics summary for a Notion page.</summary>
public sealed class PageAnalyticsDto
{
    /// <summary>Page identifier the analytics summary belongs to.</summary>
    public Guid PageId { get; set; }

    /// <summary>Total number of page views.</summary>
    public int Views { get; set; }

    /// <summary>Total number of unique visitors when the provider can resolve it.</summary>
    public int UniqueVisitors { get; set; }

    /// <summary>Last known view timestamp.</summary>
    public DateTime? LastViewedAt { get; set; }

    /// <summary>Daily view series for analytics panels.</summary>
    public IReadOnlyList<PageAnalyticsPointDto> ViewsByDay { get; set; } = [];
}

/// <summary>Single dated analytics point for a Notion page.</summary>
public sealed class PageAnalyticsPointDto
{
    /// <summary>Calendar date of the analytics point.</summary>
    public DateOnly Date { get; set; }

    /// <summary>View count for the date.</summary>
    public int Views { get; set; }
}

/// <summary>Date range and result limit used for analytics top-page queries.</summary>
public sealed class NotionAnalyticsRange
{
    /// <summary>Inclusive start date. When null, providers use their earliest available analytics date.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Inclusive end date. When null, providers use their latest available analytics date.</summary>
    public DateOnly? To { get; set; }

    /// <summary>Maximum number of pages to return.</summary>
    public int Take { get; set; } = 5;
}

/// <summary>Request used to record a page analytics view.</summary>
public sealed class RecordPageViewRequest
{
    /// <summary>User identifier for unique visitor counting. Null records an anonymous visitor.</summary>
    public string? UserId { get; set; }
}
