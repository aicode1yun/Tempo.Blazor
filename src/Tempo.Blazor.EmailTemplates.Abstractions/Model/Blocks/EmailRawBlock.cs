namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>
/// Verbatim HTML/MJML passthrough (<c>mj-raw</c>). <see cref="Content"/> is emitted without escaping.
/// </summary>
public sealed class EmailRawBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Raw;

    /// <summary>Gets or sets the raw content emitted verbatim into the MJML output.</summary>
    public string Content { get; set; } = string.Empty;
}
