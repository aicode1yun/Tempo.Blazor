namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>A section (<c>mj-section</c>); the top-level horizontal band that holds columns.</summary>
public sealed class EmailSection
{
    /// <summary>Gets or sets the unique identifier of this section.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets the columns contained in this section.</summary>
    public IList<EmailColumn> Columns { get; set; } = new List<EmailColumn>();

    /// <summary>Gets or sets the background colour (<c>background-color</c>).</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Gets or sets the background image URL (<c>background-url</c>).</summary>
    public string? BackgroundUrl { get; set; }

    /// <summary>Gets or sets the background position (<c>background-position</c>).</summary>
    public string? BackgroundPosition { get; set; }

    /// <summary>Gets or sets the background repeat (<c>background-repeat</c>).</summary>
    public string? BackgroundRepeat { get; set; }

    /// <summary>Gets or sets the background size (<c>background-size</c>).</summary>
    public string? BackgroundSize { get; set; }

    /// <summary>Gets or sets the border shorthand (<c>border</c>).</summary>
    public string? Border { get; set; }

    /// <summary>Gets or sets the border radius (<c>border-radius</c>).</summary>
    public string? BorderRadius { get; set; }

    /// <summary>Gets or sets the layout direction (<c>direction</c>).</summary>
    public string Direction { get; set; } = "ltr";

    /// <summary>Gets or sets whether the section spans the full window width (<c>full-width</c>).</summary>
    public bool FullWidth { get; set; }

    /// <summary>Gets or sets the padding shorthand (<c>padding</c>).</summary>
    public string Padding { get; set; } = "20px 0";

    /// <summary>Gets or sets the text alignment (<c>text-align</c>).</summary>
    public string TextAlign { get; set; } = "center";

    /// <summary>Gets or sets the space-separated CSS class names (<c>css-class</c>).</summary>
    public string? CssClass { get; set; }

    /// <summary>Gets or sets the referenced named MJML classes (<c>mj-class</c>).</summary>
    public IList<string> MjClasses { get; set; } = new List<string>();

    /// <summary>Gets or sets unmodelled attributes preserved for round-trip fidelity.</summary>
    public IDictionary<string, string> ExtraAttributes { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
