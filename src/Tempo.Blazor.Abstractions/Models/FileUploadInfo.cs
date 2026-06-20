namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Represents a file being uploaded through <see cref="IFileManagerDataProvider.UploadAsync"/>.
/// </summary>
public class FileUploadInfo
{
    /// <summary>The original file name including extension.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>The file size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>The MIME content type of the file.</summary>
    public string? ContentType { get; set; }

    /// <summary>The readable stream containing the file data.</summary>
    public System.IO.Stream Stream { get; set; } = System.IO.Stream.Null;
}
