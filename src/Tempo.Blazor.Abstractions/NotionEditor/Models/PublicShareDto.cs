namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Public share settings for a Notion page.</summary>
public sealed class PublicShareDto
{
    /// <summary>Shared page identifier.</summary>
    public Guid PageId { get; set; }

    /// <summary>Opaque public token used in the public URL.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Whether the public share is currently enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Whether anonymous viewers can add comments on the public page.</summary>
    public bool AllowComments { get; set; }

    /// <summary>Optional UTC expiry timestamp. Null means the link does not expire.</summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Options used when creating or replacing a public share.</summary>
public sealed class PublicShareOptions
{
    /// <summary>Whether anonymous viewers can add comments on the public page.</summary>
    public bool AllowComments { get; set; }

    /// <summary>Optional UTC expiry timestamp. Null means the link does not expire.</summary>
    public DateTime? ExpiresAt { get; set; }
}
