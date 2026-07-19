using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jint;
using Jint.Native;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.DocumentFormats.HeadlessLayout;

/// <summary>
/// <see cref="ITempoDocumentLayoutService"/> hosted in Jint: evaluates the embedded headless
/// layout bundle and calls its <c>generateHeadlessLayoutSnapshotJson</c> seam — one JSON request
/// in (canvas model + Skia advance tables), one JSON result out. Text measurement runs entirely
/// inside JS from the precomputed tables; there are no .NET↔JS callbacks per glyph.
/// Engines are pooled (thread-safe, bounded by observed concurrency) because bundle evaluation
/// is the expensive step — repeated MCP/preview traffic never allocates an engine per call.
/// Fail-closed: no fonts, an unknown font family, an unmeasurable glyph, or a JS failure all
/// throw <see cref="TempoDocumentLayoutException"/> with diagnostics instead of degrading the
/// layout silently.
/// </summary>
public sealed class JintDocumentLayoutEngine : ITempoDocumentLayoutService, IDisposable
{
    private const string ModuleSpecifier = "tempo/headless-layout";

    // Canvas model wire format: camelCase properties, camelCase string enums, nulls omitted —
    // mirrors the editor's interop serializer (CanvasEngineJsonContext), so the bundle receives
    // the exact shape the browser engine mounts.
    private static readonly JsonSerializerOptions CanvasModelWireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private const double PointsToCssPixels = 96d / 72d;

    private readonly ConcurrentBag<PooledEngine> _pool = [];
    private readonly int _maxRetainedEngines;
    private readonly TempoFontAdvanceTableExtractor _fontTableExtractor;
    private int _createdEngineCount;
    private volatile bool _disposed;

    /// <summary>Creates a pooled Jint-hosted layout service.</summary>
    /// <param name="maxRetainedEngines">Upper bound of idle engines kept for reuse;
    /// defaults to the processor count.</param>
    /// <param name="fontTableExtractor">Advance-table extractor; defaults to the shared,
    /// process-wide cached instance.</param>
    public JintDocumentLayoutEngine(int? maxRetainedEngines = null, TempoFontAdvanceTableExtractor? fontTableExtractor = null)
    {
        _maxRetainedEngines = Math.Max(1, maxRetainedEngines ?? Environment.ProcessorCount);
        _fontTableExtractor = fontTableExtractor ?? TempoFontAdvanceTableExtractor.Shared;
    }

    /// <summary>Total Jint engines created so far — diagnostics for pooling behavior.</summary>
    public int CreatedEngineCount => Volatile.Read(ref _createdEngineCount);

    /// <inheritdoc />
    public string GenerateLayoutSnapshotJson(
        DocumentEditorDocument document,
        DocumentPdfPageSetupOptions? pageSetup = null,
        IReadOnlyList<ReportPdfFontFace>? fonts = null,
        DocumentReviewDisplayMode reviewDisplayMode = DocumentReviewDisplayMode.AllMarkup)
    {
        ArgumentNullException.ThrowIfNull(document);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (fonts is null || fonts.Count == 0)
        {
            throw new TempoDocumentLayoutException(
                "Headless layout requires the font faces the PDF renderer embeds (fonts was null or empty) — " +
                "without them text measurement cannot be WYSIWYG-accurate.");
        }

        var requestJson = BuildRequestJson(document, pageSetup, fonts, reviewDisplayMode);
        var resultJson = InvokeEngine(requestJson);
        return UnwrapSnapshot(resultJson);
    }

    /// <summary>
    /// Builds the exact JSON request the JS layout seam (<c>generateHeadlessLayoutSnapshotJson</c>)
    /// receives — the serialized canvas model, the font advance tables and the review display
    /// mode. Exposed for diagnostics and cross-runtime parity tooling (the same payload replayed
    /// through Node must produce the same snapshot the Jint host produces).
    /// </summary>
    public string BuildRequestJson(
        DocumentEditorDocument document,
        DocumentPdfPageSetupOptions? pageSetup,
        IReadOnlyList<ReportPdfFontFace> fonts,
        DocumentReviewDisplayMode reviewDisplayMode = DocumentReviewDisplayMode.AllMarkup)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fonts);
        var canvasModel = CanvasDocumentModelConverter.ToCanvasModel(document);
        if (pageSetup is not null)
        {
            var overridden = ToCanvasPageSettings(pageSetup, document.PageSettings);
            canvasModel.PageSettings = overridden;
            foreach (var section in canvasModel.Sections)
            {
                // A PDF page setup override is document-wide — section-level page settings would
                // silently win over it inside the paginator.
                section.PageSettings = overridden;
            }
        }

        var modelJson = JsonSerializer.Serialize(canvasModel, CanvasModelWireOptions);
        var fontTablesJson = _fontTableExtractor.BuildAdvanceTablesJson(fonts);

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("model");
            writer.WriteRawValue(modelJson);
            writer.WritePropertyName("fontTables");
            writer.WriteRawValue(fontTablesJson);
            writer.WriteString("reviewDisplayMode", JsonNamingPolicy.CamelCase.ConvertName(reviewDisplayMode.ToString()));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static CanvasPageSettings ToCanvasPageSettings(DocumentPdfPageSetupOptions pageSetup, DocumentPageSettings documentSettings)
    {
        // Mirrors CanvasDocumentModelConverter.ToCanvasPageSettings: points → CSS pixels,
        // landscape swaps the longer edge to width.
        var size = pageSetup.PageSize ?? DocumentPageSize.A4;
        var margins = pageSetup.Margins ?? DocumentPageMargins.Default;
        var landscape = pageSetup.Orientation == DocumentPdfPageOrientation.Landscape;
        var widthPoints = landscape ? Math.Max(size.Width, size.Height) : size.Width;
        var heightPoints = landscape ? Math.Min(size.Width, size.Height) : size.Height;
        return new CanvasPageSettings
        {
            Width = widthPoints * PointsToCssPixels,
            Height = heightPoints * PointsToCssPixels,
            MarginTop = margins.Top * PointsToCssPixels,
            MarginRight = margins.Right * PointsToCssPixels,
            MarginBottom = margins.Bottom * PointsToCssPixels,
            MarginLeft = margins.Left * PointsToCssPixels,
            HeaderDistanceFromTop = documentSettings.HeaderDistanceFromTop * PointsToCssPixels,
            FooterDistanceFromBottom = documentSettings.FooterDistanceFromBottom * PointsToCssPixels,
            SizeName = size.Name,
            Landscape = landscape,
        };
    }

    private string InvokeEngine(string requestJson)
    {
        var engine = RentEngine();
        try
        {
            var result = engine.Invoke(requestJson);
            ReturnEngine(engine);
            return result;
        }
        catch (Jint.Runtime.JavaScriptException jsError)
        {
            // The engine itself stays healthy after a script exception — keep it pooled.
            ReturnEngine(engine);
            throw new TempoDocumentLayoutException(
                $"Headless layout engine failed: {jsError.Message}", innerException: jsError);
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }

    private static string UnwrapSnapshot(string resultJson)
    {
        using var document = JsonDocument.Parse(resultJson);
        var root = document.RootElement;
        var diagnostics = root.GetProperty("diagnostics");

        var unknownFamilies = diagnostics.GetProperty("unknownFamilies").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => item.Length > 0)
            .ToList();
        var missingGlyphs = diagnostics.GetProperty("missingGlyphs").EnumerateArray()
            .Select(item => new TempoDocumentLayoutMissingGlyph(
                item.GetProperty("family").GetString() ?? string.Empty,
                item.GetProperty("codePoint").GetInt32()))
            .ToList();

        if (unknownFamilies.Count > 0 || missingGlyphs.Count > 0)
        {
            var families = string.Join(", ", unknownFamilies);
            var glyphs = string.Join(", ", missingGlyphs.Select(glyph => $"{glyph.Family} U+{glyph.CodePoint:X4}"));
            throw new TempoDocumentLayoutException(
                "Headless layout fell back to synthetic text metrics — the provided fonts cannot measure the document " +
                $"(unknown families: [{families}]; missing glyphs: [{glyphs}]). Provide the missing font faces.",
                unknownFamilies,
                missingGlyphs);
        }

        return root.GetProperty("snapshot").GetRawText();
    }

    private PooledEngine RentEngine()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pool.TryTake(out var pooled))
        {
            return pooled;
        }

        Interlocked.Increment(ref _createdEngineCount);
        return new PooledEngine();
    }

    private void ReturnEngine(PooledEngine engine)
    {
        if (_disposed || _pool.Count >= _maxRetainedEngines)
        {
            engine.Dispose();
            return;
        }

        _pool.Add(engine);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        while (_pool.TryTake(out var pooled))
        {
            pooled.Dispose();
        }
    }

    /// <summary>One Jint engine with the bundle evaluated and the layout seam resolved. Not
    /// thread-safe — the pool guarantees single-threaded use.</summary>
    private sealed class PooledEngine : IDisposable
    {
        private readonly Engine _engine;
        private readonly JsValue _generateSnapshotJson;

        public PooledEngine()
        {
            _engine = new Engine();
            _engine.Modules.Add(ModuleSpecifier, TempoDocumentHeadlessLayoutBundle.ReadJavaScript());
            var moduleNamespace = _engine.Modules.Import(ModuleSpecifier);
            _generateSnapshotJson = moduleNamespace.Get("generateHeadlessLayoutSnapshotJson");
            if (_generateSnapshotJson.IsUndefined())
            {
                throw new TempoDocumentLayoutException(
                    "The embedded headless layout bundle does not export generateHeadlessLayoutSnapshotJson — " +
                    "rebuild it via `npm run build:document-editor`.");
            }
        }

        public string Invoke(string requestJson)
            => _engine.Invoke(_generateSnapshotJson, requestJson).AsString();

        public void Dispose() => _engine.Dispose();
    }
}
