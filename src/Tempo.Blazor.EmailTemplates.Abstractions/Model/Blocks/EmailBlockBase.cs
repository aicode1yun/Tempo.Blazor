using System.Text.Json.Serialization;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>
/// Base type for every content block placed inside a column (or a block container such as a hero).
/// Carries the identity and the cross-cutting attributes shared by all MJML body components
/// (see <c>docs/email-templates/MJML_ATTRIBUTE_PARITY.md</c>).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(EmailTextBlock), "text")]
[JsonDerivedType(typeof(EmailButtonBlock), "button")]
[JsonDerivedType(typeof(EmailImageBlock), "image")]
[JsonDerivedType(typeof(EmailDividerBlock), "divider")]
[JsonDerivedType(typeof(EmailSpacerBlock), "spacer")]
[JsonDerivedType(typeof(EmailRawBlock), "raw")]
[JsonDerivedType(typeof(EmailTableBlock), "table")]
[JsonDerivedType(typeof(EmailSocialBlock), "social")]
[JsonDerivedType(typeof(EmailHeroBlock), "hero")]
[JsonDerivedType(typeof(EmailNavbarBlock), "navbar")]
[JsonDerivedType(typeof(EmailCarouselBlock), "carousel")]
[JsonDerivedType(typeof(EmailAccordionBlock), "accordion")]
[JsonDerivedType(typeof(EmailWrapperBlock), "wrapper")]
[JsonDerivedType(typeof(EmailGroupBlock), "group")]
public abstract class EmailBlockBase
{
    /// <summary>Gets or sets the unique identifier of this block.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets the discriminator identifying this block's concrete type.</summary>
    [JsonIgnore]
    public abstract BlockType Type { get; }

    /// <summary>Gets or sets the space-separated CSS class names (<c>css-class</c>).</summary>
    public string? CssClass { get; set; }

    /// <summary>Gets or sets the referenced named MJML classes (<c>mj-class</c>).</summary>
    public IList<string> MjClasses { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets attributes that are not modelled explicitly. Populated on import and re-emitted
    /// on export so unknown/future MJML attributes survive a round-trip without loss.
    /// </summary>
    public IDictionary<string, string> ExtraAttributes { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets an optional Scriban boolean expression. When set, the generator wraps the block
    /// in <c>{{ if &lt;expr&gt; }}…{{ end }}</c> so it only renders when the expression is truthy.
    /// This is a Tempo extension, not an MJML attribute.
    /// </summary>
    public string? VisibleWhen { get; set; }

    /// <summary>Gets or sets the padding shorthand (<c>padding</c>).</summary>
    public string? Padding { get; set; }

    /// <summary>Gets or sets the top padding (<c>padding-top</c>).</summary>
    public string? PaddingTop { get; set; }

    /// <summary>Gets or sets the right padding (<c>padding-right</c>).</summary>
    public string? PaddingRight { get; set; }

    /// <summary>Gets or sets the bottom padding (<c>padding-bottom</c>).</summary>
    public string? PaddingBottom { get; set; }

    /// <summary>Gets or sets the left padding (<c>padding-left</c>).</summary>
    public string? PaddingLeft { get; set; }

    /// <summary>Gets or sets the container background colour (<c>container-background-color</c>).</summary>
    public string? ContainerBackgroundColor { get; set; }
}
