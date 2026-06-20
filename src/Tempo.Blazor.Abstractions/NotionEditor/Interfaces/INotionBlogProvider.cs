namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

/// <summary>Provides chronological Notion blog posts for a space.</summary>
public interface INotionBlogProvider
{
    /// <summary>Gets a paged chronological list of posts in a space.</summary>
    Task<PagedResult<NotionBlogPostDto>> GetPostsAsync(string spaceId, NotionBlogQuery paging, CancellationToken cancellationToken = default);

    /// <summary>Gets a single blog post by identifier.</summary>
    Task<NotionBlogPostDto?> GetPostAsync(string postId, CancellationToken cancellationToken = default);

    /// <summary>Creates a draft post.</summary>
    Task<NotionBlogPostDto> CreatePostAsync(CreateNotionBlogPostRequest request, CancellationToken cancellationToken = default);

    /// <summary>Publishes a draft post or updates its publication timestamp.</summary>
    Task<NotionBlogPostDto> PublishAsync(string postId, PublishNotionBlogPostRequest request, CancellationToken cancellationToken = default);
}
