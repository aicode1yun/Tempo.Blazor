using Microsoft.AspNetCore.Components;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.UI;

/// <summary>Read-only blog post detail that reuses the Notion block renderer.</summary>
public partial class TmNotionBlogPost : ComponentBase
{
    /// <summary>Blog post to render.</summary>
    [Parameter, EditorRequired] public NotionBlogPostDto Post { get; set; } = default!;

    /// <summary>True while the post is being published.</summary>
    [Parameter] public bool IsPublishing { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Raised when the publish button is clicked. Arg is the post id.</summary>
    [Parameter] public EventCallback<string> OnPublish { get; set; }

    private IReadOnlyList<IPageBlock> PostBlocks => Post.Blocks.Cast<IPageBlock>().ToArray();

    private string StatusText => Post.PublishedAt is null
        ? Loc["Notion_Blog_Draft"]
        : Loc["Notion_Blog_Published"];

    private string StatusClass => Post.PublishedAt is null
        ? "tm-notion-blog-post__status tm-notion-blog-post__status--draft"
        : "tm-notion-blog-post__status tm-notion-blog-post__status--published";
}
