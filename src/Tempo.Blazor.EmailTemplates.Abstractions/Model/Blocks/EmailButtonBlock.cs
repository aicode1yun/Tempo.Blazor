namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A call-to-action button (<c>mj-button</c>). <see cref="Text"/> is the label.</summary>
public sealed class EmailButtonBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Button;

    /// <summary>Gets or sets the button label text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the link target (<c>href</c>).</summary>
    public string? Href { get; set; }

    /// <summary>Gets or sets the link <c>rel</c> attribute.</summary>
    public string? Rel { get; set; }

    /// <summary>Gets or sets the link target window (<c>target</c>).</summary>
    public string Target { get; set; } = "_blank";

    /// <summary>Gets or sets the background colour (<c>background-color</c>).</summary>
    public string BackgroundColor { get; set; } = "#414141";

    /// <summary>Gets or sets the text colour (<c>color</c>).</summary>
    public string Color { get; set; } = "#ffffff";

    /// <summary>Gets or sets the font family (<c>font-family</c>).</summary>
    public string? FontFamily { get; set; }

    /// <summary>Gets or sets the font size (<c>font-size</c>).</summary>
    public string FontSize { get; set; } = "13px";

    /// <summary>Gets or sets the font style (<c>font-style</c>).</summary>
    public string? FontStyle { get; set; }

    /// <summary>Gets or sets the font weight (<c>font-weight</c>).</summary>
    public string FontWeight { get; set; } = "normal";

    /// <summary>Gets or sets the line height (<c>line-height</c>).</summary>
    public string LineHeight { get; set; } = "120%";

    /// <summary>Gets or sets the letter spacing (<c>letter-spacing</c>).</summary>
    public string? LetterSpacing { get; set; }

    /// <summary>Gets or sets the content text alignment (<c>text-align</c>).</summary>
    public string TextAlign { get; set; } = "center";

    /// <summary>Gets or sets the text decoration (<c>text-decoration</c>).</summary>
    public string TextDecoration { get; set; } = "none";

    /// <summary>Gets or sets the text transform (<c>text-transform</c>).</summary>
    public string? TextTransform { get; set; }

    /// <summary>Gets or sets the block alignment within the column (<c>align</c>).</summary>
    public string Align { get; set; } = "center";

    /// <summary>Gets or sets the vertical alignment (<c>vertical-align</c>).</summary>
    public string VerticalAlign { get; set; } = "middle";

    /// <summary>Gets or sets the border shorthand (<c>border</c>).</summary>
    public string Border { get; set; } = "none";

    /// <summary>Gets or sets the border radius (<c>border-radius</c>).</summary>
    public string BorderRadius { get; set; } = "3px";

    /// <summary>Gets or sets the inner padding (<c>inner-padding</c>).</summary>
    public string InnerPadding { get; set; } = "10px 25px";

    /// <summary>Gets or sets the explicit width (<c>width</c>).</summary>
    public string? Width { get; set; }

    /// <summary>Gets or sets the explicit height (<c>height</c>).</summary>
    public string? Height { get; set; }

    /// <summary>Initializes a new instance of the <see cref="EmailButtonBlock"/> class.</summary>
    public EmailButtonBlock() => Padding = "10px 25px";
}
