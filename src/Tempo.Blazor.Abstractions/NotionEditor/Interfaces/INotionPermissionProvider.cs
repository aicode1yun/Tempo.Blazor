using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>Provides Confluence-style page restrictions for the Notion editor.</summary>
public interface INotionPermissionProvider
{
    /// <summary>Gets restrictions assigned directly to a page.</summary>
    Task<PageRestrictionDto> GetRestrictionsAsync(Guid pageId, CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces restrictions assigned directly to a page.</summary>
    Task SetRestrictionsAsync(PageRestrictionDto restrictions, CancellationToken cancellationToken = default);

    /// <summary>Gets the effective permission for a user, including inherited restrictions from parent pages.</summary>
    Task<PageEffectivePermissionDto> GetEffectivePermissionAsync(
        Guid pageId,
        string userId,
        IReadOnlyList<string>? groupIds = null,
        CancellationToken cancellationToken = default);
}
