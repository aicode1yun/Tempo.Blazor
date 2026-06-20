namespace Tempo.Blazor.Models;

/// <summary>
/// Represents a file attachment within a chat message.
/// </summary>
public sealed record ChatAttachment
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display file name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Download or preview URL.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>MIME content type.</summary>
    public string? ContentType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long? Size { get; init; }

    public ChatAttachment() { }

    public ChatAttachment(string id, string name, string url, string? contentType = null, long? size = null)
    {
        Id = id;
        Name = name;
        Url = url;
        ContentType = contentType;
        Size = size;
    }
}
