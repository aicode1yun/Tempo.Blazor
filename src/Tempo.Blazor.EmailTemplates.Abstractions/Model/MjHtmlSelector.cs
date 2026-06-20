namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>
/// A single <c>mj-html-attributes</c> selector: a CSS path and the custom HTML attributes to apply.
/// Carried for lossless round-trip; the default renderer (Mjml.Net) does not emit it (see E0.9).
/// </summary>
public sealed class MjHtmlSelector
{
    /// <summary>Gets or sets the CSS selector path (<c>mj-selector path</c>).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets the custom HTML attributes to apply to matching elements.</summary>
    public IDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
