namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A full-width wrapper (<c>mj-wrapper</c>) that groups whole sections under one background.</summary>
public sealed class EmailWrapperBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Wrapper;

    /// <summary>Gets the sections held by the wrapper.</summary>
    public List<EmailSection> Sections { get; set; } = new();

    /// <summary>Gets or sets the background colour (<c>background-color</c>).</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Gets or sets the background image URL (<c>background-url</c>).</summary>
    public string? BackgroundUrl { get; set; }

    /// <summary>Gets or sets the border shorthand (<c>border</c>).</summary>
    public string? Border { get; set; }

    /// <summary>Gets or sets the border radius (<c>border-radius</c>).</summary>
    public string? BorderRadius { get; set; }

    /// <summary>Gets or sets the text alignment (<c>text-align</c>).</summary>
    public string TextAlign { get; set; } = "center";

    /// <summary>Gets or sets whether the wrapper spans the full width (<c>full-width</c>).</summary>
    public bool FullWidth { get; set; }
}
