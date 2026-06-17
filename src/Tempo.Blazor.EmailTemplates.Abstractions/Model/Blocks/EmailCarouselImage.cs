namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A single image within an <see cref="EmailCarouselBlock"/> (<c>mj-carousel-image</c>).</summary>
public sealed class EmailCarouselImage
{
    /// <summary>Gets or sets the image source URL (<c>src</c>).</summary>
    public string Src { get; set; } = string.Empty;

    /// <summary>Gets or sets the alternative text (<c>alt</c>).</summary>
    public string Alt { get; set; } = string.Empty;

    /// <summary>Gets or sets the link target (<c>href</c>).</summary>
    public string? Href { get; set; }

    /// <summary>Gets or sets the link <c>rel</c> attribute.</summary>
    public string? Rel { get; set; }

    /// <summary>Gets or sets the link target window (<c>target</c>).</summary>
    public string Target { get; set; } = "_blank";

    /// <summary>Gets or sets the image title (<c>title</c>).</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets a distinct thumbnail source (<c>thumbnails-src</c>).</summary>
    public string? ThumbnailsSrc { get; set; }
}
