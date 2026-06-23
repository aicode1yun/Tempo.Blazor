namespace Tempo.Blazor.Components.NotionEditor.Services;

internal static class NotionMediaUploadValidation
{
    public const long MaxFileSizeBytes = 100L * 1024 * 1024;
    public const int MinVisualWidth = 80;
    public const int MaxVisualWidth = 1200;

    public static string? Validate(string mediaType, string fileName, string? contentType, long sizeBytes)
    {
        if (sizeBytes > MaxFileSizeBytes)
        {
            return "TmNotionMediaUploadDialog_FileTooLarge";
        }

        return IsAllowedType(mediaType, fileName, contentType)
            ? null
            : "TmNotionMediaUploadDialog_InvalidFileType";
    }

    public static int ClampVisualWidth(int width) =>
        Math.Clamp(width, MinVisualWidth, MaxVisualWidth);

    public static string FormatMaxFileSize()
    {
        var mb = MaxFileSizeBytes / 1_048_576;
        return $"{mb} MB";
    }

    private static bool IsAllowedType(string mediaType, string fileName, string? contentType)
    {
        var type = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return mediaType switch
        {
            "image" => type.StartsWith("image/", StringComparison.Ordinal) ||
                       extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".avif" or ".svg",
            "video" => type.StartsWith("video/", StringComparison.Ordinal) ||
                       extension is ".mp4" or ".webm" or ".mov" or ".m4v",
            "audio" => type.StartsWith("audio/", StringComparison.Ordinal) ||
                       extension is ".mp3" or ".wav" or ".ogg" or ".m4a" or ".flac",
            "pdf" => type == "application/pdf" || extension == ".pdf",
            _ => true
        };
    }
}
