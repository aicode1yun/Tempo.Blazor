namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A row of social-network icons (<c>mj-social</c>).</summary>
public sealed class EmailSocialBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Social;

    /// <summary>Gets the social elements.</summary>
    public IList<EmailSocialElement> Elements { get; set; } = new List<EmailSocialElement>();

    /// <summary>Gets or sets the layout mode, <c>horizontal</c> or <c>vertical</c> (<c>mode</c>).</summary>
    public string Mode { get; set; } = "horizontal";

    /// <summary>Gets or sets the alignment (<c>align</c>).</summary>
    public string Align { get; set; } = "center";

    /// <summary>Gets or sets the icon size (<c>icon-size</c>).</summary>
    public string IconSize { get; set; } = "20px";

    /// <summary>Gets or sets the border radius of icons (<c>border-radius</c>).</summary>
    public string BorderRadius { get; set; } = "3px";

    /// <summary>Gets or sets the label colour (<c>color</c>).</summary>
    public string Color { get; set; } = "#333333";

    /// <summary>Gets or sets the font size (<c>font-size</c>).</summary>
    public string FontSize { get; set; } = "13px";

    /// <summary>Gets or sets the font family (<c>font-family</c>).</summary>
    public string? FontFamily { get; set; }

    /// <summary>Gets or sets the line height (<c>line-height</c>).</summary>
    public string LineHeight { get; set; } = "22px";

    /// <summary>Gets or sets the text padding (<c>text-padding</c>).</summary>
    public string TextPadding { get; set; } = "4px 4px 4px 0";

    /// <summary>Gets or sets the text decoration (<c>text-decoration</c>).</summary>
    public string TextDecoration { get; set; } = "none";

    /// <summary>Initializes a new instance of the <see cref="EmailSocialBlock"/> class.</summary>
    public EmailSocialBlock() => Padding = "10px 25px";
}
