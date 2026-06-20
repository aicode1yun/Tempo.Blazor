using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>
/// One rendered editor page exported as a flat bitmap (plan S1). The canvas engine paints the page's
/// printable layers into an opaque image so the document can be used as a signing template page.
/// </summary>
public sealed class DocumentPageImage
{
    /// <summary>Zero-based page index within the document.</summary>
    public int PageIndex { get; set; }

    /// <summary>Logical (CSS pixel) page width. The export resolution is carried by <see cref="Scale"/>.</summary>
    public double Width { get; set; }

    /// <summary>Logical (CSS pixel) page height.</summary>
    public double Height { get; set; }

    /// <summary>Resolution multiplier baked into the image pixels (1–3, default 2). The data URL backing
    /// store is <see cref="Width"/>×<see cref="Scale"/> by <see cref="Height"/>×<see cref="Scale"/> pixels.</summary>
    public double Scale { get; set; } = 1;

    /// <summary>Rendered page image as a data URL (PNG by default, JPEG when requested).</summary>
    public string DataUrl { get; set; } = string.Empty;
}

/// <summary>Options that control how editor pages are exported to images (plan S1, O1).</summary>
public sealed class DocumentPageImageExportOptions
{
    /// <summary>Resolution multiplier (clamped to 1–3 by the engine). Default 2 for retina/zoom fidelity.</summary>
    public double Scale { get; set; } = 2;

    /// <summary>Image format: <c>"png"</c> (default) or <c>"jpeg"</c> (opt-in for large documents).</summary>
    public string Format { get; set; } = "png";

    /// <summary>JPEG quality (0.1–1.0) used only when <see cref="Format"/> is <c>"jpeg"</c>.</summary>
    public double? Quality { get; set; }
}

/// <summary>Maps exported editor page images into the signing model consumed by the signing designer/runner.</summary>
public static class DocumentPageImageMappingExtensions
{
    /// <summary>
    /// Projects rendered page images into <see cref="SigningDocumentPage"/> instances that the signing
    /// designer and form runner already consume. The image data URL becomes the page background image and
    /// the logical page size becomes the page dimensions used for normalized 0..1 field placement.
    /// </summary>
    /// <param name="images">Exported page images, one per document page.</param>
    /// <param name="attachmentUuid">Stable attachment identifier the produced pages and fields share.</param>
    /// <param name="labelFactory">Optional factory producing a localized accessible page label from the
    /// 1-based page number. Abstractions carries no localizer, so the UI layer supplies localized text.</param>
    public static List<SigningDocumentPage> ToSigningDocumentPages(
        this IEnumerable<DocumentPageImage> images,
        string attachmentUuid,
        Func<int, string>? labelFactory = null)
    {
        ArgumentNullException.ThrowIfNull(images);

        return images
            .Select(image => new SigningDocumentPage
            {
                AttachmentUuid = attachmentUuid,
                PageIndex = image.PageIndex,
                ImageUrl = image.DataUrl,
                Width = image.Width,
                Height = image.Height,
                Label = labelFactory?.Invoke(image.PageIndex + 1)
            })
            .ToList();
    }
}
