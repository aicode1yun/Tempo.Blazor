namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>Chronological blog post stored inside a Notion space.</summary>
public sealed class NotionBlogPostDto
{
    /// <summary>Stable blog post identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Owning space identifier.</summary>
    public string SpaceId { get; set; } = string.Empty;

    /// <summary>Post title displayed in blog list and detail.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Publish timestamp. Null means the post is still a draft.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Author user identifier.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>Read-only Notion blocks rendered as the blog post body.</summary>
    public IReadOnlyList<PageBlock> Blocks { get; set; } = [];
}
