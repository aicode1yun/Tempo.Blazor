using Tempo.Blazor.Models;

namespace Tempo.Blazor.Interfaces;

/// <summary>
/// Server-side data provider for TmFilterableDropdown.
/// Implement this interface to provide paginated, searchable items from any data source.
/// </summary>
/// <typeparam name="TItem">The item type returned by the provider.</typeparam>
/// <example>
/// <code>
/// public class UsersDropdownProvider : IDropdownDataProvider&lt;UserDto&gt;
/// {
///     public async Task&lt;DropdownDataResult&lt;UserDto&gt;&gt; GetItemsAsync(
///         DropdownSearchRequest request, CancellationToken ct = default)
///     {
///         var result = await _api.SearchUsersAsync(request.SearchText, request.Page, request.PageSize, ct);
///         return DropdownDataResult&lt;UserDto&gt;.WithItems(result.Items, result.TotalCount);
///     }
/// }
/// </code>
/// </example>
public interface IDropdownDataProvider<TItem>
{
    /// <summary>
    /// Retrieves items matching the search criteria.
    /// </summary>
    /// <param name="request">Search parameters including text, pagination, and excluded IDs.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<DropdownDataResult<TItem>> GetItemsAsync(DropdownSearchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a new item from the user's typed text (inline "create new" support). Returns the created
    /// item, or <c>null</c> when the provider does not support creation. The default is a no-op returning
    /// <c>null</c>, so existing providers keep compiling and behaving unchanged.
    /// </summary>
    /// <param name="text">The user-entered text to create the item from.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TItem?> CreateAsync(string text, CancellationToken ct = default)
        => Task.FromResult<TItem?>(default);

    /// <summary>
    /// Returns recently used items to surface at the top of the dropdown, or an empty list when the
    /// provider has none. The default returns an empty list, so existing providers keep compiling and
    /// behaving unchanged.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<TItem>> GetRecentAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TItem>>(Array.Empty<TItem>());
}
