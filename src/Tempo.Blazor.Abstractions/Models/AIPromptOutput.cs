namespace Tempo.Blazor.Models;

/// <summary>
/// Represents an output / response item displayed in the AI prompt component.
/// </summary>
public sealed record AIPromptOutput
{
    /// <summary>Unique identifier for the output.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The rendered content (text, markdown, or code).</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Format of the content.</summary>
    public AIPromptOutputFormat Format { get; init; } = AIPromptOutputFormat.Text;

    /// <summary>Whether the output is still being generated.</summary>
    public bool IsLoading { get; init; }

    /// <summary>Optional user rating: true = positive, false = negative, null = unrated.</summary>
    public bool? Rating { get; init; }

    /// <summary>Optional title/label for the output (e.g., the prompt that produced it).</summary>
    public string? Title { get; init; }

    public AIPromptOutput() { }

    public AIPromptOutput(string id, string content, AIPromptOutputFormat format = AIPromptOutputFormat.Text, bool isLoading = false, bool? rating = null, string? title = null)
    {
        Id = id;
        Content = content;
        Format = format;
        IsLoading = isLoading;
        Rating = rating;
        Title = title;
    }
}

/// <summary>
/// Format options for AI prompt output content.
/// </summary>
public enum AIPromptOutputFormat
{
    /// <summary>Plain text.</summary>
    Text,
    /// <summary>Markdown formatted text.</summary>
    Markdown,
    /// <summary>Code block with optional language hint.</summary>
    Code,
}
