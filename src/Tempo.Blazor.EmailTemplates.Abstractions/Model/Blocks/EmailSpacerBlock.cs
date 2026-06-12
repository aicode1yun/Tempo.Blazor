namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>Vertical spacing (<c>mj-spacer</c>).</summary>
public sealed class EmailSpacerBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Spacer;

    /// <summary>Gets or sets the spacer height (<c>height</c>).</summary>
    public string Height { get; set; } = "20px";
}
