using System.Text;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>
/// Accumulates element attributes, escaping values and omitting defaults, then renders them as the
/// attribute portion of an opening tag.
/// </summary>
internal sealed class MjmlAttributeBuffer
{
    private readonly List<(string Name, string Value)> _items = new();

    /// <summary>Adds an optional attribute; skipped when the value is null or empty.</summary>
    public MjmlAttributeBuffer Optional(string name, string? value)
    {
        if (!string.IsNullOrEmpty(value)) _items.Add((name, MjmlEscape.Attribute(value)));
        return this;
    }

    /// <summary>Adds an attribute only when it differs from its MJML default value.</summary>
    public MjmlAttributeBuffer Defaulted(string name, string value, string defaultValue)
    {
        if (!string.Equals(value, defaultValue, StringComparison.Ordinal))
            _items.Add((name, MjmlEscape.Attribute(value)));
        return this;
    }

    /// <summary>Adds a verbatim attribute (value still escaped).</summary>
    public MjmlAttributeBuffer Raw(string name, string value)
    {
        _items.Add((name, MjmlEscape.Attribute(value)));
        return this;
    }

    /// <summary>Adds a boolean flag attribute (e.g. <c>full-width="full-width"</c>) when set.</summary>
    public MjmlAttributeBuffer Flag(string name, bool value)
    {
        if (value) _items.Add((name, name));
        return this;
    }

    /// <summary>Adds <c>css-class</c>, <c>mj-class</c> and any preserved extra attributes.</summary>
    public MjmlAttributeBuffer Common(string? cssClass, IList<string> mjClasses, IDictionary<string, string> extra)
    {
        Optional("css-class", cssClass);
        if (mjClasses.Count > 0) Raw("mj-class", string.Join(' ', mjClasses));
        foreach (var kv in extra) Raw(kv.Key, kv.Value);
        return this;
    }

    /// <summary>Adds the padding shorthand/sides and container background shared by blocks.</summary>
    public MjmlAttributeBuffer BlockCommon(EmailBlockBase block, string? defaultPadding)
    {
        Defaulted("padding", block.Padding ?? string.Empty, defaultPadding ?? string.Empty);
        Optional("padding-top", block.PaddingTop);
        Optional("padding-right", block.PaddingRight);
        Optional("padding-bottom", block.PaddingBottom);
        Optional("padding-left", block.PaddingLeft);
        Optional("container-background-color", block.ContainerBackgroundColor);
        Common(block.CssClass, block.MjClasses, block.ExtraAttributes);
        return this;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var (name, value) in _items)
            sb.Append(' ').Append(name).Append("=\"").Append(value).Append('"');
        return sb.ToString();
    }
}
