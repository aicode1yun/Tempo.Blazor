using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Font faces for the demo headless document export. Loads the host system's sans-serif faces
/// (Arial on Windows, DejaVu Sans on Linux) once at startup and registers every face under both
/// the "Arial" and "Aptos" family names — demo documents use the
/// <c>Aptos, Arial, sans-serif</c> theme, and the alias keeps headless face resolution (and with
/// it measurement↔drawing parity) deterministic. <see cref="HasFonts"/> is false on hosts
/// without any known system face; snapshot-less exports then fail closed with
/// <c>TempoDocumentLayoutException</c> instead of degrading to non-WYSIWYG output.
/// </summary>
public sealed class DemoDocumentExportFontCatalog
{
    private static readonly (string Path, int Weight, string Style)[] Candidates =
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

    /// <summary>Loads the system faces once.</summary>
    public DemoDocumentExportFontCatalog()
    {
        var fonts = new List<ReportPdfFontFace>();
        var seen = new HashSet<(int Weight, string Style)>();
        foreach (var (path, weight, style) in Candidates)
        {
            if (!File.Exists(path) || !seen.Add((weight, style)))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(path);
            fonts.Add(new ReportPdfFontFace("Arial", weight, style, bytes));
            fonts.Add(new ReportPdfFontFace("Aptos", weight, style, bytes));
        }

        Fonts = fonts;
    }

    /// <summary>Font faces available for headless layout and PDF embedding.</summary>
    public IReadOnlyList<ReportPdfFontFace> Fonts { get; }

    /// <summary>True when at least one system face was found.</summary>
    public bool HasFonts => Fonts.Count > 0;
}
