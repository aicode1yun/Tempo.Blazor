using Tempo.Blazor.Abstractions.Shared;

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

/// <summary>Conversion helpers between chat-specific attachments and shared Tempo attachments.</summary>
public static class ChatAttachmentBridge
{
    /// <summary>Entity type used when a chat message owns a shared attachment.</summary>
    public const string EntityType = "chat-message";

    /// <summary>Converts a chat attachment to a shared attachment linked to a chat message.</summary>
    public static TmAttachment ToTmAttachment(this ChatAttachment attachment, string messageId)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return new TmAttachment
        {
            Id = string.IsNullOrWhiteSpace(attachment.Id) ? Guid.NewGuid().ToString("N") : attachment.Id,
            AssetId = string.IsNullOrWhiteSpace(attachment.Id) ? null : attachment.Id,
            EntityRef = TmEntityRef.Create(EntityType, messageId),
            FileName = attachment.Name,
            Url = attachment.Url,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.Size ?? 0,
            Purpose = "chat-attachment",
            CanDownload = !string.IsNullOrWhiteSpace(attachment.Url)
        };
    }

    /// <summary>Converts a shared attachment to a chat-specific attachment.</summary>
    public static ChatAttachment ToChatAttachment(this TmAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return new ChatAttachment(
            attachment.Id,
            attachment.FileName,
            attachment.Url ?? string.Empty,
            attachment.ContentType,
            attachment.SizeBytes);
    }
}
