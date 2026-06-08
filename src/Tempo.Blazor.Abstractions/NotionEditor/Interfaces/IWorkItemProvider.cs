using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>Provides external work items from a concrete tracker.</summary>
public interface IWorkItemProvider
{
    /// <summary>Stable provider key used by work item blocks and queries.</summary>
    string ProviderKey { get; }

    /// <summary>User-visible provider name.</summary>
    string DisplayName { get; }

    /// <summary>Gets a single work item by provider-native ID.</summary>
    Task<WorkItemDto?> GetByIdAsync(string externalId, CancellationToken cancellationToken);

    /// <summary>Searches work items using provider-agnostic and opaque query fields.</summary>
    Task<PagedResult<WorkItemDto>> SearchAsync(WorkItemQuery query, CancellationToken cancellationToken);
}
