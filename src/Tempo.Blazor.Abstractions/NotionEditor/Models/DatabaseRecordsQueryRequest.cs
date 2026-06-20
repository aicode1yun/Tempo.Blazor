namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Request body for querying Notion-style database records.</summary>
public sealed record DatabaseRecordsQueryRequest(
    NotionDatabaseFilter? Filter,
    IReadOnlyList<NotionDatabaseSort>? Sorts,
    NotionDatabaseGrouping? Grouping,
    int Page = 1,
    int PageSize = 50);
