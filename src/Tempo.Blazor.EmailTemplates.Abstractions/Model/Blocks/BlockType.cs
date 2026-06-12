namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>
/// Identifies the kind of an <see cref="EmailBlockBase"/>. Each value maps to one MJML body component
/// and is used as the JSON polymorphic discriminator token.
/// </summary>
public enum BlockType
{
    /// <summary>A text block (<c>mj-text</c>).</summary>
    Text,

    /// <summary>A call-to-action button (<c>mj-button</c>).</summary>
    Button,

    /// <summary>An image (<c>mj-image</c>).</summary>
    Image,

    /// <summary>A horizontal rule (<c>mj-divider</c>).</summary>
    Divider,

    /// <summary>Vertical spacing (<c>mj-spacer</c>).</summary>
    Spacer,

    /// <summary>Verbatim HTML/MJML passthrough (<c>mj-raw</c>).</summary>
    Raw,

    /// <summary>A data table (<c>mj-table</c>).</summary>
    Table,

    /// <summary>A row of social-network icons (<c>mj-social</c>).</summary>
    Social,

    /// <summary>A hero banner (<c>mj-hero</c>).</summary>
    Hero,

    /// <summary>A navigation bar (<c>mj-navbar</c>).</summary>
    Navbar,

    /// <summary>An image carousel (<c>mj-carousel</c>).</summary>
    Carousel,

    /// <summary>An accordion (<c>mj-accordion</c>).</summary>
    Accordion,

    /// <summary>A full-width wrapper holding sections (<c>mj-wrapper</c>).</summary>
    Wrapper,

    /// <summary>A group of columns that stay side-by-side on mobile (<c>mj-group</c>).</summary>
    Group,
}
