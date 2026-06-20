using DocumentFormat.OpenXml.Packaging;
using System.Text;

namespace Tempo.Blazor.DocumentFormats.Docx;

/// <summary>Maps document image data to DOCX image part metadata.</summary>
public static class DocxImageContentTypeMapper
{
    private static readonly DocxImagePartInfo PngInfo = new(ImagePartType.Png, "image/png", ".png");
    private static readonly DocxImagePartInfo JpegInfo = new(ImagePartType.Jpeg, "image/jpeg", ".jpg");
    private static readonly DocxImagePartInfo GifInfo = new(ImagePartType.Gif, "image/gif", ".gif");
    private static readonly DocxImagePartInfo BmpInfo = new(ImagePartType.Bmp, "image/bmp", ".bmp");
    private static readonly DocxImagePartInfo TiffInfo = new(ImagePartType.Tiff, "image/tiff", ".tiff");
    private static readonly DocxImagePartInfo SvgInfo = new(ImagePartType.Svg, "image/svg+xml", ".svg");

    /// <summary>PNG image part metadata.</summary>
    public static DocxImagePartInfo Png => PngInfo;

    /// <summary>Attempts to resolve DOCX image part metadata from content type, file name, or byte signature.</summary>
    public static bool TryResolve(string? contentType, string? fileName, ReadOnlySpan<byte> content, out DocxImagePartInfo info)
    {
        if (TryFromContentType(contentType, out info)
            || TryFromFileName(fileName, out info)
            || TryFromSignature(content, out info))
        {
            return true;
        }

        info = PngInfo;
        return false;
    }

    /// <summary>Attempts to parse an image data URL into content type and bytes.</summary>
    public static bool TryParseDataUrl(string? dataUrl, out DocxImageData data)
        => TryParseDataUrl(dataUrl, long.MaxValue, out data, out _);

    /// <summary>Attempts to parse an image data URL into content type and bytes while enforcing a decoded byte limit.</summary>
    public static bool TryParseDataUrl(string? dataUrl, long maxBytes, out DocxImageData data, out bool exceededLimit)
    {
        data = new DocxImageData();
        exceededLimit = false;
        if (string.IsNullOrWhiteSpace(dataUrl)
            || !dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var comma = dataUrl.IndexOf(',');
        if (comma < 0)
        {
            return false;
        }

        var header = dataUrl[5..comma];
        var headerParts = header.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var contentType = headerParts.FirstOrDefault(part => part.Contains('/', StringComparison.Ordinal));
        var isBase64 = headerParts.Any(part => part.Equals("base64", StringComparison.OrdinalIgnoreCase));
        if (!isBase64)
        {
            return false;
        }

        var maxContentBytes = Math.Max(1L, maxBytes);
        var base64Payload = dataUrl[(comma + 1)..];
        if (EstimateBase64DecodedLength(base64Payload) > maxContentBytes)
        {
            exceededLimit = true;
            return false;
        }

        try
        {
            data = new DocxImageData
            {
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                Content = Convert.FromBase64String(base64Payload)
            };
            if (data.Content.Length > maxContentBytes)
            {
                data = new DocxImageData();
                exceededLimit = true;
                return false;
            }

            return data.Content.Length > 0;
        }
        catch (FormatException)
        {
            data = new DocxImageData();
            return false;
        }
    }

    /// <summary>Detects a suspicious mismatch between a declared image content type and the actual byte signature.</summary>
    public static bool HasContentTypeSignatureMismatch(
        string? declaredContentType,
        ReadOnlySpan<byte> content,
        out string? detectedContentType)
    {
        detectedContentType = null;
        if (!TryFromContentType(declaredContentType, out var declared)
            || !TryFromSignature(content, out var detected))
        {
            return false;
        }

        detectedContentType = detected.ContentType;
        return !string.Equals(declared.ContentType, detected.ContentType, StringComparison.OrdinalIgnoreCase);
    }

    private static long EstimateBase64DecodedLength(string value)
    {
        var chars = 0L;
        var padding = 0;
        for (var i = value.Length - 1; i >= 0; i--)
        {
            var character = value[i];
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            if (character == '=' && padding < 2)
            {
                padding++;
                continue;
            }

            break;
        }

        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                chars++;
            }
        }

        if (chars == 0)
        {
            return 0;
        }

        return ((chars + 3L) / 4L * 3L) - padding;
    }

    private static bool TryFromContentType(string? contentType, out DocxImagePartInfo info)
    {
        var normalized = NormalizeContentType(contentType);
        info = normalized switch
        {
            "image/png" or "image/x-png" => PngInfo,
            "image/jpeg" or "image/jpg" or "image/pjpeg" => JpegInfo,
            "image/gif" => GifInfo,
            "image/bmp" or "image/x-bmp" or "image/x-ms-bmp" => BmpInfo,
            "image/tiff" or "image/tif" => TiffInfo,
            "image/svg+xml" => SvgInfo,
            _ => PngInfo
        };

        return normalized is
            "image/png" or "image/x-png" or
            "image/jpeg" or "image/jpg" or "image/pjpeg" or
            "image/gif" or
            "image/bmp" or "image/x-bmp" or "image/x-ms-bmp" or
            "image/tiff" or "image/tif" or
            "image/svg+xml";
    }

    private static bool TryFromFileName(string? fileName, out DocxImagePartInfo info)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            info = PngInfo;
            return false;
        }

        info = extension.ToLowerInvariant() switch
        {
            ".png" => PngInfo,
            ".jpg" or ".jpeg" or ".jpe" => JpegInfo,
            ".gif" => GifInfo,
            ".bmp" or ".dib" => BmpInfo,
            ".tif" or ".tiff" => TiffInfo,
            ".svg" => SvgInfo,
            _ => PngInfo
        };

        return !ReferenceEquals(info, PngInfo) || string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryFromSignature(ReadOnlySpan<byte> content, out DocxImagePartInfo info)
    {
        if (content.Length >= 8
            && content[0] == 0x89
            && content[1] == 0x50
            && content[2] == 0x4E
            && content[3] == 0x47
            && content[4] == 0x0D
            && content[5] == 0x0A
            && content[6] == 0x1A
            && content[7] == 0x0A)
        {
            info = PngInfo;
            return true;
        }

        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            info = JpegInfo;
            return true;
        }

        if (content.Length >= 6
            && content[0] == 0x47
            && content[1] == 0x49
            && content[2] == 0x46
            && content[3] == 0x38
            && (content[4] == 0x37 || content[4] == 0x39)
            && content[5] == 0x61)
        {
            info = GifInfo;
            return true;
        }

        if (content.Length >= 2 && content[0] == 0x42 && content[1] == 0x4D)
        {
            info = BmpInfo;
            return true;
        }

        if (content.Length >= 4
            && ((content[0] == 0x49 && content[1] == 0x49 && content[2] == 0x2A && content[3] == 0x00)
                || (content[0] == 0x4D && content[1] == 0x4D && content[2] == 0x00 && content[3] == 0x2A)))
        {
            info = TiffInfo;
            return true;
        }

        if (LooksLikeSvg(content))
        {
            info = SvgInfo;
            return true;
        }

        info = PngInfo;
        return false;
    }

    private static bool LooksLikeSvg(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty)
        {
            return false;
        }

        var length = Math.Min(content.Length, 512);
        var text = Encoding.UTF8.GetString(content[..length]).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return text.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
            || (text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
                && text.Contains("<svg", StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var semicolon = contentType.IndexOf(';');
        return (semicolon >= 0 ? contentType[..semicolon] : contentType)
            .Trim()
            .ToLowerInvariant();
    }
}

/// <summary>Resolved DOCX image part metadata.</summary>
public sealed class DocxImagePartInfo
{
    /// <summary>Creates resolved image part metadata.</summary>
    public DocxImagePartInfo(PartTypeInfo imagePartType, string contentType, string extension)
    {
        ImagePartType = imagePartType;
        ContentType = contentType;
        Extension = extension;
    }

    /// <summary>Open XML SDK image part type.</summary>
    public PartTypeInfo ImagePartType { get; }

    /// <summary>Normalized image content type.</summary>
    public string ContentType { get; }

    /// <summary>Preferred file extension including the leading dot.</summary>
    public string Extension { get; }
}

/// <summary>Decoded image data from a data URL.</summary>
public sealed class DocxImageData
{
    /// <summary>Image content type from the data URL header.</summary>
    public string ContentType { get; init; } = "application/octet-stream";

    /// <summary>Decoded image bytes.</summary>
    public byte[] Content { get; init; } = [];
}
