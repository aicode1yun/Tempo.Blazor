namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Describes an uploaded file that should be scanned by an <see cref="Interfaces.IFileScanHook"/>.</summary>
public sealed class FileScanRequest
{
    /// <summary>Original or display file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type, when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Provider-managed asset identifier assigned during upload, when available.</summary>
    public string? AssetId { get; set; }

    /// <summary>Entity that owns the uploaded asset, when known.</summary>
    public TmEntityRef? EntityRef { get; set; }

    /// <summary>Optional purpose, matching the upload's purpose (e.g. "document-manager").</summary>
    public string? Purpose { get; set; }

    /// <summary>Arbitrary metadata carried from the upload for the scanner's use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
