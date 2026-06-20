using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>Optional provider for Notion page likes and emoji reactions.</summary>
public interface INotionReactionProvider
{
    /// <summary>Returns aggregated reactions for the requested page.</summary>
    Task<IReadOnlyList<PageReactionDto>> GetReactionsAsync(Guid pageId, CancellationToken cancellationToken = default);

    /// <summary>Toggles the current user's like reaction and returns the updated aggregate set.</summary>
    Task<IReadOnlyList<PageReactionDto>> ToggleLikeAsync(Guid pageId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Toggles the current user's emoji reaction and returns the updated aggregate set.</summary>
    Task<IReadOnlyList<PageReactionDto>> ToggleReactionAsync(Guid pageId, string reaction, string userId, CancellationToken cancellationToken = default);
}
