namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A horizontal rule (<c>mj-divider</c>).</summary>
public sealed class EmailDividerBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Divider;

    /// <summary>Gets or sets the line colour (<c>border-color</c>).</summary>
    public string BorderColor { get; set; } = "#000000";

    /// <summary>Gets or sets the line style (<c>border-style</c>).</summary>
    public string BorderStyle { get; set; } = "solid";

    /// <summary>Gets or sets the line thickness (<c>border-width</c>).</summary>
    public string BorderWidth { get; set; } = "4px";

    /// <summary>Gets or sets the line width (<c>width</c>).</summary>
    public string Width { get; set; } = "100%";

    /// <summary>Gets or sets the alignment (<c>align</c>).</summary>
    public string Align { get; set; } = "center";

    /// <summary>Initializes a new instance of the <see cref="EmailDividerBlock"/> class.</summary>
    public EmailDividerBlock() => Padding = "10px 25px";
}
