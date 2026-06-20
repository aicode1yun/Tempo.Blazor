namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Request for publishing a draft Notion blog post.</summary>
public sealed class PublishNotionBlogPostRequest
{
    /// <summary>Publish timestamp. Provider uses current UTC time when null.</summary>
    public DateTime? PublishedAt { get; set; }
}
