namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A navigation bar (<c>mj-navbar</c>).</summary>
public sealed class EmailNavbarBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Navbar;

    /// <summary>Gets the navigation links.</summary>
    public IList<EmailNavbarLink> Links { get; set; } = new List<EmailNavbarLink>();

    /// <summary>Gets or sets the alignment (<c>align</c>).</summary>
    public string Align { get; set; } = "center";

    /// <summary>Gets or sets the base URL prepended to relative link hrefs (<c>base-url</c>).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Gets or sets whether a hamburger menu is shown on mobile (<c>hamburger</c>).</summary>
    public string? Hamburger { get; set; }
}
