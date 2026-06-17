namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A single icon within an <see cref="EmailSocialBlock"/> (<c>mj-social-element</c>).</summary>
public sealed class EmailSocialElement
{
    /// <summary>Gets or sets the network name mapping to a built-in icon (<c>name</c>).</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the link target (<c>href</c>).</summary>
    public string? Href { get; set; }

    /// <summary>Gets or sets a custom icon URL overriding <see cref="Name"/> (<c>src</c>).</summary>
    public string? Src { get; set; }

    /// <summary>Gets or sets the icon alternative text (<c>alt</c>).</summary>
    public string Alt { get; set; } = string.Empty;

    /// <summary>Gets or sets the link target window (<c>target</c>).</summary>
    public string Target { get; set; } = "_blank";

    /// <summary>Gets or sets the optional label text shown next to the icon.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets the background colour override (<c>background-color</c>).</summary>
    public string? BackgroundColor { get; set; }
}
