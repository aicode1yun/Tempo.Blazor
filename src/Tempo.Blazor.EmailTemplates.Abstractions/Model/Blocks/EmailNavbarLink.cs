namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A single link within an <see cref="EmailNavbarBlock"/> (<c>mj-navbar-link</c>).</summary>
public sealed class EmailNavbarLink
{
    /// <summary>Gets or sets the link text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the link target (<c>href</c>).</summary>
    public string? Href { get; set; }

    /// <summary>Gets or sets the link <c>rel</c> attribute.</summary>
    public string? Rel { get; set; }

    /// <summary>Gets or sets the link target window (<c>target</c>).</summary>
    public string Target { get; set; } = "_blank";

    /// <summary>Gets or sets the text colour (<c>color</c>).</summary>
    public string Color { get; set; } = "#000000";

    /// <summary>Gets or sets the font family (<c>font-family</c>).</summary>
    public string? FontFamily { get; set; }

    /// <summary>Gets or sets the font size (<c>font-size</c>).</summary>
    public string FontSize { get; set; } = "13px";

    /// <summary>Gets or sets the font weight (<c>font-weight</c>).</summary>
    public string FontWeight { get; set; } = "normal";

    /// <summary>Gets or sets the line height (<c>line-height</c>).</summary>
    public string LineHeight { get; set; } = "22px";

    /// <summary>Gets or sets the text decoration (<c>text-decoration</c>).</summary>
    public string TextDecoration { get; set; } = "none";

    /// <summary>Gets or sets the text transform (<c>text-transform</c>).</summary>
    public string TextTransform { get; set; } = "uppercase";

    /// <summary>Gets or sets the padding (<c>padding</c>).</summary>
    public string Padding { get; set; } = "15px 10px";
}
