namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>An image carousel (<c>mj-carousel</c>).</summary>
public sealed class EmailCarouselBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Carousel;

    /// <summary>Gets the carousel images.</summary>
    public List<EmailCarouselImage> Images { get; set; } = new();

    /// <summary>Gets or sets the alignment (<c>align</c>).</summary>
    public string Align { get; set; } = "center";

    /// <summary>Gets or sets the image border radius (<c>border-radius</c>).</summary>
    public string BorderRadius { get; set; } = "6px";

    /// <summary>Gets or sets the navigation icon width (<c>icon-width</c>).</summary>
    public string IconWidth { get; set; } = "44px";

    /// <summary>Gets or sets the left navigation icon URL (<c>left-icon</c>).</summary>
    public string? LeftIcon { get; set; }

    /// <summary>Gets or sets the right navigation icon URL (<c>right-icon</c>).</summary>
    public string? RightIcon { get; set; }

    /// <summary>Gets or sets whether thumbnails are <c>visible</c> or <c>hidden</c> (<c>thumbnails</c>).</summary>
    public string Thumbnails { get; set; } = "visible";

    /// <summary>Gets or sets the thumbnail border radius (<c>tb-border-radius</c>).</summary>
    public string TbBorderRadius { get; set; } = "6px";
}

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
