using System.Reflection;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>
/// Provides bulk page operations for Notion data providers, including a single-page fallback for providers that do not expose native bulk methods.
/// </summary>
public static class NotionDataProviderBulkExtensions
{
    /// <summary>
    /// Moves all selected pages under the specified parent page or to the root when <paramref name="newParentId" /> is <see langword="null" />.
    /// </summary>
    /// <param name="provider">The Notion data provider.</param>
    /// <param name="pageIds">The page identifiers to move.</param>
    /// <param name="newParentId">The target parent page identifier, or <see langword="null" /> for root pages.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    public static async Task MovePagesAsync(
        this INotionDataProvider provider,
        IReadOnlyList<string> pageIds,
        string? newParentId,
        CancellationToken cancellationToken = default)
    {
        var method = FindProviderMethod(provider, nameof(MovePagesAsync));
        if (method is not null)
        {
            var result = method.Invoke(provider, [pageIds, newParentId, cancellationToken]);
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                return;
            }
        }

        foreach (var pageId in pageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await provider.MovePageAsync(pageId, newParentId).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deletes all selected pages using the provider bulk operation when available, otherwise deletes each page individually.
    /// </summary>
    /// <param name="provider">The Notion data provider.</param>
    /// <param name="pageIds">The page identifiers to delete.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    public static async Task DeletePagesAsync(
        this INotionDataProvider provider,
        IReadOnlyList<string> pageIds,
        CancellationToken cancellationToken = default)
    {
        var method = FindProviderMethod(provider, nameof(DeletePagesAsync));
        if (method is not null)
        {
            var result = method.Invoke(provider, [pageIds, cancellationToken]);
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                return;
            }
        }

        foreach (var pageId in pageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await provider.DeletePageAsync(pageId).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Copies a page tree under the specified parent page, returning the copied root page.
    /// </summary>
    /// <param name="provider">The Notion data provider.</param>
    /// <param name="pageId">The root page identifier to copy.</param>
    /// <param name="newParentId">The target parent page identifier, or <see langword="null" /> for root pages.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The copied root page.</returns>
    public static async Task<INotionPage> CopyPageTreeAsync(
        this INotionDataProvider provider,
        string pageId,
        string? newParentId,
        CancellationToken cancellationToken = default)
    {
        var method = FindProviderMethod(provider, nameof(CopyPageTreeAsync));
        if (method is not null)
        {
            var result = method.Invoke(provider, [pageId, newParentId, cancellationToken]);
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                return ExtractCopiedPage(task) ?? await provider.GetPageAsync(pageId).ConfigureAwait(false);
            }
        }

        var duplicate = await provider.DuplicatePageAsync(pageId).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await provider.MovePageAsync(duplicate.Id.ToString("D"), newParentId).ConfigureAwait(false);

        return duplicate;
    }

    private static MethodInfo? FindProviderMethod(INotionDataProvider provider, string methodName)
        => provider.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method =>
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    return false;

                var parameters = method.GetParameters();
                return methodName == nameof(DeletePagesAsync)
                    ? parameters.Length == 2
                    : parameters.Length == 3;
            });

    private static INotionPage? ExtractCopiedPage(Task task)
    {
        var resultProperty = task.GetType().GetProperty("Result");
        var result = resultProperty?.GetValue(task);
        if (result is INotionPage page)
            return page;

        var copiedRootProperty = result?.GetType().GetProperty("CopiedRoot")
            ?? result?.GetType().GetProperty("RootPage");
        return copiedRootProperty?.GetValue(result) as INotionPage;
    }
}
