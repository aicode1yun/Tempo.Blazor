using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Block content for an embedded external work item.</summary>
public sealed class WorkItemBlockContent : IWorkItemBlockContent
{
    /// <inheritdoc />
    public string ProviderKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public string ExternalId { get; set; } = string.Empty;

    /// <inheritdoc />
    public WorkItemDto? CachedSnapshot { get; set; }

    /// <inheritdoc />
    public WorkItemDisplayMode DisplayMode { get; set; } = WorkItemDisplayMode.Card;
}
