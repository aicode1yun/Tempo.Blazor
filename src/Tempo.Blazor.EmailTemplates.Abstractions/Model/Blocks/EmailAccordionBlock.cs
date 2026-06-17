namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>An accordion (<c>mj-accordion</c>).</summary>
public sealed class EmailAccordionBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Accordion;

    /// <summary>Gets the accordion items.</summary>
    public IList<EmailAccordionItem> Items { get; set; } = new List<EmailAccordionItem>();

    /// <summary>Gets or sets the border shorthand (<c>border</c>).</summary>
    public string Border { get; set; } = "2px solid black";

    /// <summary>Gets or sets the icon alignment (<c>icon-align</c>).</summary>
    public string IconAlign { get; set; } = "middle";

    /// <summary>Gets or sets the icon position, <c>left</c> or <c>right</c> (<c>icon-position</c>).</summary>
    public string IconPosition { get; set; } = "right";

    /// <summary>Gets or sets the icon height (<c>icon-height</c>).</summary>
    public string IconHeight { get; set; } = "32px";

    /// <summary>Gets or sets the icon width (<c>icon-width</c>).</summary>
    public string IconWidth { get; set; } = "32px";

    /// <summary>Gets or sets the URL of the collapsed-state icon (<c>icon-wrapped-url</c>).</summary>
    public string? IconWrappedUrl { get; set; }

    /// <summary>Gets or sets the URL of the expanded-state icon (<c>icon-unwrapped-url</c>).</summary>
    public string? IconUnwrappedUrl { get; set; }

    /// <summary>Gets or sets the font family (<c>font-family</c>).</summary>
    public string? FontFamily { get; set; }
}
