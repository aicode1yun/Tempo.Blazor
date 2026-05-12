namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Rendered document page used by signing designers and signing forms.</summary>
public class SigningDocumentPage
{
    /// <summary>Document attachment identifier this page belongs to.</summary>
    public string AttachmentUuid { get; set; } = string.Empty;

    /// <summary>Zero-based page index within the attachment.</summary>
    public int PageIndex { get; set; }

    /// <summary>Page image URL, usually a server-rendered PNG or JPEG preview.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Original page width in pixels.</summary>
    public double Width { get; set; }

    /// <summary>Original page height in pixels.</summary>
    public double Height { get; set; }

    /// <summary>Accessible page label.</summary>
    public string? Label { get; set; }

    /// <summary>Localized accessible page labels.</summary>
    public SigningLocalizedText Labels { get; set; } = new();
}
