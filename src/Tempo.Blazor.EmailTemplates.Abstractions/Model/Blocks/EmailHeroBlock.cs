namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A hero banner (<c>mj-hero</c>) that holds content blocks over a background.</summary>
public sealed class EmailHeroBlock : EmailBlockBase, IBlockContainer
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Hero;

    /// <summary>Gets the blocks rendered inside the hero.</summary>
    public IList<EmailBlockBase> Blocks { get; set; } = new List<EmailBlockBase>();

    /// <summary>Gets or sets the sizing mode, <c>fluid-height</c> or <c>fixed-height</c> (<c>mode</c>).</summary>
    public string Mode { get; set; } = "fluid-height";

    /// <summary>Gets or sets the hero height, used in fixed-height mode (<c>height</c>).</summary>
    public string Height { get; set; } = "0px";

    /// <summary>Gets or sets the background colour (<c>background-color</c>).</summary>
    public string BackgroundColor { get; set; } = "#ffffff";

    /// <summary>Gets or sets the background image URL (<c>background-url</c>).</summary>
    public string? BackgroundUrl { get; set; }

    /// <summary>Gets or sets the background width (<c>background-width</c>).</summary>
    public string? BackgroundWidth { get; set; }

    /// <summary>Gets or sets the background height (<c>background-height</c>).</summary>
    public string? BackgroundHeight { get; set; }

    /// <summary>Gets or sets the background position (<c>background-position</c>).</summary>
    public string BackgroundPosition { get; set; } = "center center";

    /// <summary>Gets or sets the vertical alignment of content (<c>vertical-align</c>).</summary>
    public string VerticalAlign { get; set; } = "top";

    /// <summary>Initializes a new instance of the <see cref="EmailHeroBlock"/> class.</summary>
    public EmailHeroBlock() => Padding = "0px";
}
