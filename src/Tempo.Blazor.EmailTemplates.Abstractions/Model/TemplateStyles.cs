namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>
/// Global, document-wide styling and head settings (maps to the MJML <c>&lt;mj-head&gt;</c> globals
/// and the <c>mj-body</c> attributes).
/// </summary>
public sealed class TemplateStyles
{
    /// <summary>Gets or sets the body content width (<c>mj-body width</c>).</summary>
    public string ContentWidth { get; set; } = "600px";

    /// <summary>Gets or sets the default font family applied across the template.</summary>
    public string FontFamily { get; set; } = "Ubuntu, Helvetica, Arial, sans-serif";

    /// <summary>Gets or sets the body background colour (<c>mj-body background-color</c>).</summary>
    public string BackgroundColor { get; set; } = "#ffffff";

    /// <summary>Gets or sets the mobile breakpoint width (<c>mj-breakpoint width</c>).</summary>
    public string Breakpoint { get; set; } = "480px";

    /// <summary>Gets the imported web fonts (<c>mj-font</c>).</summary>
    public List<EmailFont> Fonts { get; set; } = new();

    /// <summary>Gets the custom style blocks (<c>mj-style</c>).</summary>
    public List<EmailStyle> Styles { get; set; } = new();

    /// <summary>Gets or sets the default-attribute cascade (<c>mj-attributes</c>: mj-all, per-tag, mj-class).</summary>
    public MjAttributes Attributes { get; set; } = new();

    /// <summary>Gets the custom HTML attribute selectors (<c>mj-html-attributes</c>, round-trip only).</summary>
    public List<MjHtmlSelector> HtmlAttributes { get; set; } = new();
}

/// <summary>An imported web font (<c>mj-font</c>).</summary>
public sealed class EmailFont
{
    /// <summary>Gets or sets the font family name (<c>name</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the stylesheet URL providing the font (<c>href</c>).</summary>
    public string Href { get; set; } = string.Empty;
}

/// <summary>A custom CSS style block (<c>mj-style</c>).</summary>
public sealed class EmailStyle
{
    /// <summary>Gets or sets the raw CSS text.</summary>
    public string Css { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the CSS should be inlined into element <c>style</c> attributes
    /// (<c>mj-style inline="inline"</c>) rather than emitted as an embedded stylesheet.
    /// </summary>
    public bool Inline { get; set; }
}
