namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>
/// Models the MJML <c>&lt;mj-attributes&gt;</c> head section: global defaults (<c>mj-all</c>),
/// per-component defaults and named classes (<c>mj-class</c>). The importer keeps this cascade intact
/// rather than flattening it onto blocks, preserving both fidelity and editability of global styles.
/// </summary>
public sealed class MjAttributes
{
    /// <summary>Gets the attributes applied to every component (<c>mj-all</c>).</summary>
    public Dictionary<string, string> All { get; set; } = new();

    /// <summary>Gets the per-component default attributes, keyed by MJML tag name (e.g. <c>mj-text</c>).</summary>
    public Dictionary<string, Dictionary<string, string>> PerTag { get; set; } = new();

    /// <summary>Gets the named class definitions (<c>mj-class</c>), keyed by class name.</summary>
    public Dictionary<string, Dictionary<string, string>> Classes { get; set; } = new();

    /// <summary>
    /// Resolves the head-level value of an attribute for a component, following MJML precedence:
    /// referenced classes (later class wins) &gt; per-tag default &gt; <c>mj-all</c>. Returns
    /// <see langword="null"/> when no head rule supplies the attribute (the block's own value or the
    /// MJML built-in default then applies). The block's own inline value is not considered here.
    /// </summary>
    public string? Resolve(string tag, IReadOnlyList<string> mjClasses, string attribute)
    {
        for (int i = mjClasses.Count - 1; i >= 0; i--)
            if (Classes.TryGetValue(mjClasses[i], out var classAttrs)
                && classAttrs.TryGetValue(attribute, out var fromClass))
                return fromClass;

        if (PerTag.TryGetValue(tag, out var tagAttrs) && tagAttrs.TryGetValue(attribute, out var fromTag))
            return fromTag;

        return All.TryGetValue(attribute, out var fromAll) ? fromAll : null;
    }
}

/// <summary>
/// A single <c>mj-html-attributes</c> selector: a CSS path and the custom HTML attributes to apply.
/// Carried for lossless round-trip; the default renderer (Mjml.Net) does not emit it (see E0.9).
/// </summary>
public sealed class MjHtmlSelector
{
    /// <summary>Gets or sets the CSS selector path (<c>mj-selector path</c>).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets the custom HTML attributes to apply to matching elements.</summary>
    public Dictionary<string, string> Attributes { get; set; } = new();
}
