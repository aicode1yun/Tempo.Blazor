using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotionMediaLibraryProvider
{
    /// <summary>
    /// Searches media items stored in the application's database.
    /// </summary>
    /// <param name="query">Free-text search query (empty = return all).</param>
    /// <param name="mediaType">
    /// Optional filter: "image" | "pdf" | "file" | null (all types).
    /// </param>
    /// <param name="skip">Pagination offset.</param>
    /// <param name="take">Page size (default 24).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<INotionMediaLibraryItem>> SearchAsync(
        string  query,
        string? mediaType = null,
        int     skip      = 0,
        int     take      = 24,
        CancellationToken ct = default);
}
