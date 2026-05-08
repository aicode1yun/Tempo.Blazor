namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Attachment referenced by signing fields or completed documents.</summary>
public class SigningAttachment
{
    /// <summary>Stable attachment identifier.</summary>
    public string Uuid { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display filename.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Content type, such as application/pdf or image/png.</summary>
    public string? ContentType { get; set; }

    /// <summary>Download or preview URL.</summary>
    public string? Url { get; set; }

    /// <summary>File size in bytes.</summary>
    public long? Size { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset? CreatedAt { get; set; }
}
