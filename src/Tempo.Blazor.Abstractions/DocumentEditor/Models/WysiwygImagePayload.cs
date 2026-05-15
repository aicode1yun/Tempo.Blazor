namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Payload for WYSIWYG image insert and upload commands.</summary>
public sealed class WysiwygImagePayload
{
    /// <summary>Image source kind.</summary>
    public DocumentImageSource Source { get; set; } = DocumentImageSource.Url;

    /// <summary>Direct image URL or uploaded image URL.</summary>
    public string? Url { get; set; }

    /// <summary>Provider-managed image asset id.</summary>
    public string? AssetId { get; set; }

    /// <summary>Alternative text.</summary>
    public string? AltText { get; set; }

    /// <summary>Optional caption.</summary>
    public string? Caption { get; set; }

    /// <summary>Original or generated file name.</summary>
    public string? FileName { get; set; }

    /// <summary>Image content type used for uploads.</summary>
    public string ContentType { get; set; } = "image/png";

    /// <summary>Image size in bytes used for upload validation.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Base64 encoded binary image payload without the data URL prefix.</summary>
    public string? Base64Data { get; set; }

    /// <summary>Selection snapshot at the time of the image insert command.</summary>
    public WysiwygSelectionSnapshot? Selection { get; set; }
}
