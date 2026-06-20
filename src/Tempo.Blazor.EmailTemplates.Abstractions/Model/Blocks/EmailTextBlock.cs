namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A text block (<c>mj-text</c>). <see cref="Content"/> may contain inline HTML.</summary>
public sealed class EmailTextBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Text;

    /// <summary>Gets or sets the inline HTML content of the block.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the text colour (<c>color</c>).</summary>
    public string Color { get; set; } = "#000000";

    /// <summary>Gets or sets the font family (<c>font-family</c>).</summary>
    public string FontFamily { get; set; } = "Ubuntu, Helvetica, Arial, sans-serif";

    /// <summary>Gets or sets the font size (<c>font-size</c>).</summary>
    public string FontSize { get; set; } = "13px";

    /// <summary>Gets or sets the font style, e.g. <c>italic</c> (<c>font-style</c>).</summary>
    public string? FontStyle { get; set; }

    /// <summary>Gets or sets the font weight (<c>font-weight</c>).</summary>
    public string? FontWeight { get; set; }

    /// <summary>Gets or sets the line height (<c>line-height</c>).</summary>
    public string LineHeight { get; set; } = "1";

    /// <summary>Gets or sets the letter spacing (<c>letter-spacing</c>).</summary>
    public string? LetterSpacing { get; set; }

    /// <summary>Gets or sets the explicit height (<c>height</c>).</summary>
    public string? Height { get; set; }

    /// <summary>Gets or sets the text decoration (<c>text-decoration</c>).</summary>
    public string? TextDecoration { get; set; }

    /// <summary>Gets or sets the text transform (<c>text-transform</c>).</summary>
    public string? TextTransform { get; set; }

    /// <summary>Gets or sets the horizontal alignment (<c>align</c>).</summary>
    public string Align { get; set; } = "left";

    /// <summary>Initializes a new instance of the <see cref="EmailTextBlock"/> class.</summary>
    public EmailTextBlock() => Padding = "10px 25px";
}
