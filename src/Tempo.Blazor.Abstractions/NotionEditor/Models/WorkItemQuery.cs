namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Provider-agnostic query for external work items.</summary>
public sealed class WorkItemQuery
{
    /// <summary>Provider discriminator used for registry lookup.</summary>
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>Free-text search string.</summary>
    public string? FreeText { get; set; }

    /// <summary>Specific provider-native IDs to resolve.</summary>
    public IReadOnlyList<string> Ids { get; set; } = [];

    /// <summary>Opaque provider-native query string, for example a Jira JQL expression.</summary>
    public string? QueryString { get; set; }

    /// <summary>Opaque Jira-style query string kept separate for Jira-compatible providers.</summary>
    public string? Jql { get; set; }

    /// <summary>Number of matching items to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Maximum number of items to return.</summary>
    public int Take { get; set; } = 20;
}
