namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>Request for creating a draft Notion blog post.</summary>
public sealed class CreateNotionBlogPostRequest
{
    /// <summary>Owning space identifier.</summary>
    public string SpaceId { get; set; } = string.Empty;

    /// <summary>Initial post title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Author user identifier.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>Initial body blocks. When empty, provider may create a valid blank body.</summary>
    public IReadOnlyList<PageBlock> Blocks { get; set; } = [];
}
