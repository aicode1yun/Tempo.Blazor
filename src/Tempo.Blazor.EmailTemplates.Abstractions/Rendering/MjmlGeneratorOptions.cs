namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>Options controlling MJML generation.</summary>
public sealed class MjmlGeneratorOptions
{
    /// <summary>
    /// Gets or sets whether to emit <c>mj-html-attributes</c>. Off by default because the render
    /// engine (Mjml.Net) does not support it; turn on only when generating MJML for export so the
    /// markup round-trips losslessly.
    /// </summary>
    public bool EmitHtmlAttributes { get; set; }

    /// <summary>The default options (render-safe).</summary>
    public static MjmlGeneratorOptions Default { get; } = new();

    /// <summary>Options for producing MJML intended for export/round-trip (includes everything).</summary>
    public static MjmlGeneratorOptions ForExport { get; } = new() { EmitHtmlAttributes = true };
}
