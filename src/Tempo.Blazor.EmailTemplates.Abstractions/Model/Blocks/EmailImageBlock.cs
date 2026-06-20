namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>An image (<c>mj-image</c>). <see cref="Src"/> is required; <see cref="Alt"/> matters for a11y.</summary>
public sealed class EmailImageBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Image;

    /// <summary>Gets or sets the image source URL (<c>src</c>).</summary>
    public string Src { get; set; } = string.Empty;

    /// <summary>Gets or sets the alternative text (<c>alt</c>). Required for accessibility.</summary>
    public string Alt { get; set; } = string.Empty;

    /// <summary>Gets or sets the link target (<c>href</c>).</summary>
    public string? Href { get; set; }

    /// <summary>Gets or sets the link <c>rel</c> attribute.</summary>
    public string? Rel { get; set; }

    /// <summary>Gets or sets the link target window (<c>target</c>).</summary>
    public string Target { get; set; } = "_blank";

    /// <summary>Gets or sets the image title (<c>title</c>).</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the horizontal alignment (<c>align</c>).</summary>
    public string Align { get; set; } = "center";

    /// <summary>Gets or sets the explicit width (<c>width</c>).</summary>
    public string? Width { get; set; }

    /// <summary>Gets or sets the explicit height (<c>height</c>).</summary>
    public string? Height { get; set; }

    /// <summary>Gets or sets the border shorthand (<c>border</c>).</summary>
    public string Border { get; set; } = "0";

    /// <summary>Gets or sets the border radius (<c>border-radius</c>).</summary>
    public string? BorderRadius { get; set; }

    /// <summary>Gets or sets whether the image becomes fluid on mobile (<c>fluid-on-mobile</c>).</summary>
    public string? FluidOnMobile { get; set; }

    /// <summary>Initializes a new instance of the <see cref="EmailImageBlock"/> class.</summary>
    public EmailImageBlock() => Padding = "10px 25px";
}
