namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>An image carousel (<c>mj-carousel</c>).</summary>
public sealed class EmailCarouselBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Carousel;

    /// <summary>Gets the carousel images.</summary>
    public IList<EmailCarouselImage> Images { get; set; } = new List<EmailCarouselImage>();

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
