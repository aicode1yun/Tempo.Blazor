using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Reporting.Engine.Pdf;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Blazor.DocumentFormats.Pdf;

/// <summary>
/// Production PDF renderer for TmDocumentEditor exports. Input is the canonical document plus the
/// canvas layout snapshot (schema v1) captured by the editor's <c>getLayoutSnapshotJson</c> interop —
/// the exact print primitives the canvas painted, so the PDF inherits the editor's line and page
/// breaking (WYSIWYG parity by construction). Output is a vector PDF with a real, searchable text
/// layer; fonts are resolved by family and embedded with subsetting by the underlying Skia PDF
/// backend (<see cref="ReportPdfRenderer"/>).
/// </summary>
public sealed class TempoDocumentPdfRenderer
{
    private readonly ReportPdfRenderer _pdfRenderer = new();

    /// <summary>Renderer options: fonts available for deterministic embedding and the default family.</summary>
    public TempoDocumentPdfRendererOptions Options { get; }

    /// <summary>Creates a renderer with optional font configuration.</summary>
    public TempoDocumentPdfRenderer(TempoDocumentPdfRendererOptions? options = null)
    {
        Options = options ?? new TempoDocumentPdfRendererOptions();
    }

    /// <summary>
    /// Renders the export request to PDF bytes. The request must carry
    /// <see cref="DocumentPdfExportRequest.LayoutSnapshotJson"/>; rendering without the editor's
    /// layout snapshot would silently lose WYSIWYG parity, so it is an explicit contract violation.
    /// </summary>
    public byte[] Render(DocumentPdfExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.LayoutSnapshotJson))
        {
            throw new ArgumentException(
                "DocumentPdfExportRequest.LayoutSnapshotJson is required: the production renderer reuses the canvas layout snapshot for WYSIWYG parity.",
                nameof(request));
        }

        var snapshot = TranslateLayoutSnapshot(request.LayoutSnapshotJson);
        return _pdfRenderer.Render(snapshot, new ReportPdfRendererOptions
        {
            Fonts = Options.Fonts,
            DefaultFontFamily = Options.DefaultFontFamily,
        });
    }

    /// <summary>
    /// Translates a canvas layout snapshot (schema v1) into the report snapshot consumed by the
    /// Skia PDF backend. Positions stay in CSS pixels; the backend converts at 0.75 pt/px, so
    /// geometry parity with the editor layout is exact (well under the 1 pt tolerance).
    /// </summary>
    public static ReportSnapshot TranslateLayoutSnapshot(string layoutSnapshotJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutSnapshotJson);

        using var document = JsonDocument.Parse(layoutSnapshotJson);
        var root = document.RootElement;
        var snapshot = new ReportSnapshot
        {
            SnapshotId = "tm-document-editor-export",
        };

        if (!root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
        {
            return snapshot;
        }

        var pageNumber = 1;
        foreach (var page in pages.EnumerateArray())
        {
            var snapshotPage = new ReportSnapshotPage
            {
                PageNumber = pageNumber++,
                Width = GetDouble(page, "width"),
                Height = GetDouble(page, "height"),
            };

            if (page.TryGetProperty("commands", out var commands) && commands.ValueKind == JsonValueKind.Array)
            {
                foreach (var command in commands.EnumerateArray())
                {
                    var translated = TranslateCommand(command);
                    if (translated is not null)
                    {
                        snapshotPage.Commands.Add(translated);
                    }
                }
            }

            snapshot.Pages.Add(snapshotPage);
        }

        return snapshot;
    }

    private static ReportSnapshotCommand? TranslateCommand(JsonElement command)
    {
        var type = GetString(command, "type");
        var id = GetString(command, "id");
        var x = GetDouble(command, "x");
        var y = GetDouble(command, "y");
        var width = GetDouble(command, "width");
        var height = GetDouble(command, "height");

        switch (type)
        {
            case "text":
            {
                var text = GetString(command, "text");
                if (text.Length == 0)
                {
                    return null;
                }

                var fontSize = GetDouble(command, "fontSize");
                var baseline = command.TryGetProperty("baseline", out var baselineElement) && baselineElement.ValueKind == JsonValueKind.Number
                    ? baselineElement.GetDouble()
                    : y + height * 0.78;
                return ReportSnapshotCommand.TextRun(
                    id,
                    text,
                    x,
                    baseline,
                    width,
                    height,
                    FirstFontFamily(GetString(command, "fontFamily")),
                    fontSize > 0 ? fontSize : 16,
                    GetString(command, "fill", "#111827"),
                    GetString(command, "fontWeight", "400"),
                    GetString(command, "fontStyle", "normal"),
                    GetDouble(command, "letterSpacing"),
                    GetBool(command, "underline"),
                    GetBool(command, "strikeThrough"),
                    NullIfEmpty(GetString(command, "highlight")),
                    GetDouble(command, "rotation"));
            }

            case "rect":
                return ReportSnapshotCommand.Rectangle(
                    id,
                    x,
                    y,
                    width,
                    height,
                    GetString(command, "fill"),
                    NullIfEmpty(GetString(command, "stroke")),
                    GetDouble(command, "strokeWidth"));

            case "line":
                return ReportSnapshotCommand.Line(
                    id,
                    x,
                    y,
                    width,
                    height,
                    GetString(command, "stroke", "#111827"),
                    Math.Max(0.25, GetDouble(command, "strokeWidth")));

            case "image":
            {
                var source = GetString(command, "source");
                return source.Length == 0
                    ? null
                    : ReportSnapshotCommand.Image(id, x, y, width, height, source);
            }

            case "path":
            {
                var pathData = GetString(command, "pathData");
                return pathData.Length == 0
                    ? null
                    : ReportSnapshotCommand.Path(
                        id,
                        pathData,
                        x,
                        y,
                        width,
                        height,
                        NullIfEmpty(GetString(command, "fill")),
                        NullIfEmpty(GetString(command, "stroke")),
                        GetDouble(command, "strokeWidth"));
            }

            default:
                return null;
        }
    }

    private static string FirstFontFamily(string cssFamilyList)
    {
        if (string.IsNullOrWhiteSpace(cssFamilyList))
        {
            return string.Empty;
        }

        var first = cssFamilyList.Split(',')[0].Trim().Trim('"', '\'');
        return first;
    }

    private static double GetDouble(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0;

    private static string GetString(JsonElement element, string name, string fallback = "")
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static bool GetBool(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True;

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>Options for <see cref="TempoDocumentPdfRenderer"/>.</summary>
public sealed record TempoDocumentPdfRendererOptions
{
    /// <summary>Fonts available for deterministic embedded rendering (family + weight + style + bytes).</summary>
    public IReadOnlyList<ReportPdfFontFace> Fonts { get; init; } = [];

    /// <summary>Fallback font family used when a text command does not carry one.</summary>
    public string DefaultFontFamily { get; init; } = "Arial";
}
