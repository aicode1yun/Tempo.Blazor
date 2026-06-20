namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A single item within an <see cref="EmailAccordionBlock"/> (<c>mj-accordion-element</c>).</summary>
public sealed class EmailAccordionItem
{
    /// <summary>Gets or sets the item title text.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the item content (inline HTML).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the item background colour (<c>background-color</c>).</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Gets or sets the title text colour (<c>color</c>).</summary>
    public string? TitleColor { get; set; }
}
