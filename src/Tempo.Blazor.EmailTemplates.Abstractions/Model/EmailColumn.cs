using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>A column (<c>mj-column</c>) inside a section; holds content blocks.</summary>
public sealed class EmailColumn : IBlockContainer
{
    /// <summary>Gets or sets the unique identifier of this column.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the column width (<c>width</c>), e.g. <c>"50%"</c> or <c>"200px"</c>.
    /// <see langword="null"/> means MJML splits the section width equally between columns.
    /// </summary>
    public string? Width { get; set; }

    /// <summary>Gets or sets the vertical alignment of content (<c>vertical-align</c>).</summary>
    public string VerticalAlign { get; set; } = "top";

    /// <summary>Gets or sets the background colour (<c>background-color</c>).</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Gets or sets the border shorthand (<c>border</c>).</summary>
    public string? Border { get; set; }

    /// <summary>Gets or sets the border radius (<c>border-radius</c>).</summary>
    public string? BorderRadius { get; set; }

    /// <summary>Gets or sets the padding shorthand (<c>padding</c>).</summary>
    public string? Padding { get; set; }

    /// <summary>Gets or sets the space-separated CSS class names (<c>css-class</c>).</summary>
    public string? CssClass { get; set; }

    /// <summary>Gets or sets the referenced named MJML classes (<c>mj-class</c>).</summary>
    public IList<string> MjClasses { get; set; } = new List<string>();

    /// <summary>Gets or sets unmodelled attributes preserved for round-trip fidelity.</summary>
    public IDictionary<string, string> ExtraAttributes { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the content blocks held by this column.</summary>
    public IList<EmailBlockBase> Blocks { get; set; } = new List<EmailBlockBase>();
}
