namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

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
