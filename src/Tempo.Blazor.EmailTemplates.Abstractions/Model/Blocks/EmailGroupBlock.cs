namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A group of columns (<c>mj-group</c>) that stay side-by-side on mobile instead of stacking.</summary>
public sealed class EmailGroupBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Group;

    /// <summary>Gets the columns held by the group.</summary>
    public IList<EmailColumn> Columns { get; set; } = new List<EmailColumn>();

    /// <summary>Gets or sets the background colour (<c>background-color</c>).</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Gets or sets the layout direction (<c>direction</c>).</summary>
    public string Direction { get; set; } = "ltr";

    /// <summary>Gets or sets the vertical alignment (<c>vertical-align</c>).</summary>
    public string VerticalAlign { get; set; } = "top";

    /// <summary>Gets or sets the explicit width (<c>width</c>).</summary>
    public string? Width { get; set; }
}
