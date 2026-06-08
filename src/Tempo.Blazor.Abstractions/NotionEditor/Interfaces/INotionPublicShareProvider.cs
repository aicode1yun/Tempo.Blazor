using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>Optional provider for public, token-based Notion page sharing.</summary>
public interface INotionPublicShareProvider
{
    /// <summary>Creates or replaces a public share for a page.</summary>
    Task<PublicShareDto> CreateShareAsync(Guid pageId, PublicShareOptions options, CancellationToken cancellationToken = default);

    /// <summary>Disables the public share for a page.</summary>
    Task RevokeAsync(Guid pageId, CancellationToken cancellationToken = default);

    /// <summary>Gets the current share settings for a page, including disabled settings.</summary>
    Task<PublicShareDto?> GetShareAsync(Guid pageId, CancellationToken cancellationToken = default);

    /// <summary>Resolves an active, non-expired public share by token.</summary>
    Task<PublicShareDto?> ResolveByTokenAsync(string token, CancellationToken cancellationToken = default);
}
