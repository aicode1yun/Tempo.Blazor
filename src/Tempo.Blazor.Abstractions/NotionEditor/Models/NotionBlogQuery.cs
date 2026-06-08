namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Paging and publication filters for querying Notion blog posts.</summary>
public sealed class NotionBlogQuery
{
    /// <summary>Includes draft posts when true.</summary>
    public bool IncludeDrafts { get; set; }

    /// <summary>Number of filtered posts to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Maximum number of posts to return.</summary>
    public int Take { get; set; } = 10;
}
