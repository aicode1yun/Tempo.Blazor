using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>
/// Render configuration for the document MCP tools (document_render_preview /
/// document_render_pdf): the font catalog the headless layout measures and embeds with
/// (fail-closed — missing fonts produce an error, never silent synthetic metrics), an optional
/// image resolver for provider-backed assets, and preview limits.
/// </summary>
public sealed class TempoDocumentMcpRenderOptions
{
    /// <summary>Explicit font faces available to the headless layout and PDF embedding.</summary>
    public List<ReportPdfFontFace> Fonts { get; } = [];

    /// <summary>
    /// Alias family → source family. Every face of the source family is also registered under
    /// the alias, so documents referencing the alias (e.g. "Aptos") measure with the source face.
    /// </summary>
    public Dictionary<string, string> FontAliases { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether well-known system faces (Windows Arial, Linux DejaVu Sans) are loaded as a
    /// fallback and registered under "Arial" plus <see cref="SystemFallbackAliases"/> — the same
    /// pattern the demo export font catalog uses. Default true.
    /// </summary>
    public bool IncludeSystemFontFallback { get; set; } = true;

    /// <summary>Extra family names the system-fallback faces are also registered under.</summary>
    public List<string> SystemFallbackAliases { get; } = ["Aptos"];

    /// <summary>
    /// Optional server-side image resolution for asset-backed/URL image sources (data URIs are
    /// embeddable as-is). Null leaves unresolved images as placeholder rectangles.
    /// </summary>
    public TempoDocumentImageSourceResolver? ImageResolver { get; set; }

    /// <summary>Hard cap on pages returned by document_render_preview in one call.</summary>
    public int MaxPreviewPages { get; set; } = 10;
}

/// <summary>Resolved font catalog for the document MCP render tools.</summary>
public interface ITempoDocumentMcpFontCatalog
{
    /// <summary>Font faces (explicit + aliases + optional system fallback), resolved once.</summary>
    IReadOnlyList<ReportPdfFontFace> Fonts { get; }
}

/// <summary>Default catalog: materializes options into a flat face list once.</summary>
public sealed class TempoDocumentMcpFontCatalog : ITempoDocumentMcpFontCatalog
{
    private static readonly (string Path, int Weight, string Style)[] SystemCandidates =
    [
        (@"C:\Windows\Fonts\arial.ttf", 400, "normal"),
        (@"C:\Windows\Fonts\arialbd.ttf", 700, "normal"),
        (@"C:\Windows\Fonts\ariali.ttf", 400, "italic"),
        (@"C:\Windows\Fonts\arialbi.ttf", 700, "italic"),
        ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 400, "normal"),
        ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 700, "normal"),
        ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf", 400, "italic"),
        ("/usr/share/fonts/truetype/dejavu/DejaVuSans-BoldOblique.ttf", 700, "italic"),
    ];

    /// <summary>Resolves the catalog from options (explicit faces → aliases → system fallback).</summary>
    public TempoDocumentMcpFontCatalog(TempoDocumentMcpRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var fonts = new List<ReportPdfFontFace>(options.Fonts);

        foreach (var (alias, sourceFamily) in options.FontAliases)
        {
            fonts.AddRange(options.Fonts
                .Where(face => string.Equals(face.Family, sourceFamily, StringComparison.OrdinalIgnoreCase))
                .Select(face => new ReportPdfFontFace(alias, face.Weight, face.Style, face.Bytes)));
        }

        if (options.IncludeSystemFontFallback)
        {
            var seen = new HashSet<(int Weight, string Style)>();
            foreach (var (path, weight, style) in SystemCandidates)
            {
                if (!File.Exists(path) || !seen.Add((weight, style)))
                {
                    continue;
                }

                var bytes = File.ReadAllBytes(path);
                fonts.Add(new ReportPdfFontFace("Arial", weight, style, bytes));
                foreach (var alias in options.SystemFallbackAliases)
                {
                    fonts.Add(new ReportPdfFontFace(alias, weight, style, bytes));
                }
            }
        }

        Fonts = fonts;
    }

    /// <inheritdoc />
    public IReadOnlyList<ReportPdfFontFace> Fonts { get; }
}

/// <summary>DI registration for the document MCP render tools.</summary>
public static class TempoDocumentEditorMcpRenderingExtensions
{
    /// <summary>
    /// Registers everything document_render_preview / document_render_pdf need: the headless
    /// document pipeline (<see cref="ITempoDocumentService"/> via AddTempoDocumentServices), the
    /// configured <see cref="TempoDocumentMcpRenderOptions"/> and the resolved
    /// <see cref="ITempoDocumentMcpFontCatalog"/>. Idempotent.
    /// </summary>
    public static IServiceCollection AddTempoDocumentEditorMcpRendering(
        this IServiceCollection services,
        Action<TempoDocumentMcpRenderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoDocumentServices();

        var options = new TempoDocumentMcpRenderOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddSingleton<ITempoDocumentMcpFontCatalog>(provider =>
            new TempoDocumentMcpFontCatalog(provider.GetRequiredService<TempoDocumentMcpRenderOptions>()));
        return services;
    }
}
