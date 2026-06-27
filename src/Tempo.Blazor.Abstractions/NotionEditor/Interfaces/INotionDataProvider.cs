namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotionDataProvider
{
    Task<INotionPage> GetPageAsync(string pageId);
    Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId);
    Task<IEnumerable<INotionPage>> GetFavoritesAsync();
    Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count);
    Task<IEnumerable<INotionPage>> GetTrashAsync();
    Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default);
    Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default);
    Task<INotionPage> CreatePageAsync(string? parentId, string title);
    Task UpdatePageAsync(INotionPage page);
    Task DeletePageAsync(string pageId);
    Task RestorePageAsync(string pageId);
    Task PermanentlyDeletePageAsync(string pageId);
    Task ToggleFavoriteAsync(string pageId, bool isFavorite);
    Task MovePageAsync(string pageId, string? newParentId);
    Task<INotionPage> DuplicatePageAsync(string pageId);

    // ── App-scoped overloads (multi-app hosts) ───────────────────────────────────
    // Root listing (parentId == null), favorites/recent/trash/labels and root-page creation are
    // app-ambiguous when one API key/session serves several apps. These overloads carry an optional
    // scopeAppId (GUID string) so stateless callers (MCP tools) can target a specific app. Default
    // implementations ignore the scope and delegate to the unscoped members, so existing single-app
    // providers need no changes; multi-app providers override the scoped overloads.

    /// <summary>App-scoped variant of <see cref="GetChildPagesAsync(string?)"/> (scope used only for root).</summary>
    Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId, string? scopeAppId)
        => GetChildPagesAsync(parentId);

    /// <summary>App-scoped variant of <see cref="GetFavoritesAsync()"/>.</summary>
    Task<IEnumerable<INotionPage>> GetFavoritesAsync(string? scopeAppId)
        => GetFavoritesAsync();

    /// <summary>App-scoped variant of <see cref="GetRecentPagesAsync(int)"/>.</summary>
    Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count, string? scopeAppId)
        => GetRecentPagesAsync(count);

    /// <summary>App-scoped variant of <see cref="GetTrashAsync()"/>.</summary>
    Task<IEnumerable<INotionPage>> GetTrashAsync(string? scopeAppId)
        => GetTrashAsync();

    /// <summary>App-scoped variant of <see cref="GetPagesByLabelAsync(string, CancellationToken)"/>.</summary>
    Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, string? scopeAppId, CancellationToken cancellationToken = default)
        => GetPagesByLabelAsync(label, cancellationToken);

    /// <summary>App-scoped variant of <see cref="GetAllLabelsAsync(CancellationToken)"/>.</summary>
    Task<IReadOnlyList<string>> GetAllLabelsAsync(string? scopeAppId, CancellationToken cancellationToken = default)
        => GetAllLabelsAsync(cancellationToken);

    /// <summary>App-scoped variant of <see cref="CreatePageAsync(string?, string)"/> (scope used only for root pages).</summary>
    Task<INotionPage> CreatePageAsync(string? parentId, string title, string? scopeAppId)
        => CreatePageAsync(parentId, title);
}
