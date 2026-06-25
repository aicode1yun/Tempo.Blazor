#pragma warning disable CA1822, MA0048

using System.Buffers.Text;
using System.Globalization;
using System.Net;
using System.Text;
using SkiaSharp;
using Svg.Skia;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Reporting.Engine.Pdf;

/// <summary>Single TrueType/OpenType font face available to the PDF renderer.</summary>
public sealed record ReportPdfFontFace
{
    /// <summary>Creates a font face from raw font bytes.</summary>
    public ReportPdfFontFace(string family, int weight, string style, byte[] bytes)
    {
        Family = family;
        Weight = weight;
        Style = style;
        Bytes = bytes;
    }

    /// <summary>Font family used by snapshot text commands.</summary>
    public string Family { get; }

    /// <summary>CSS-like numeric font weight.</summary>
    public int Weight { get; }

    /// <summary>CSS-like font style, usually normal or italic.</summary>
    public string Style { get; }

    /// <summary>Raw TTF/OTF bytes. Skia subsets and embeds this face in the PDF.</summary>
    public byte[] Bytes { get; }
}

/// <summary>Options for snapshot to PDF rendering.</summary>
public sealed record ReportPdfRendererOptions
{
    /// <summary>PDF points per snapshot CSS pixel. Defaults to 72 / 96.</summary>
    public double PdfPointsPerCssPixel { get; init; } = 0.75;

    /// <summary>Fonts available for deterministic embedded rendering.</summary>
    public IReadOnlyList<ReportPdfFontFace> Fonts { get; init; } = [];

    /// <summary>Fallback family used when a text run does not specify one.</summary>
    public string DefaultFontFamily { get; init; } = "Inter";
}

/// <summary>Renders report snapshots to PDF and Skia raster images.</summary>
public sealed class ReportPdfRenderer
{
    /// <summary>Renders a report snapshot to a PDF byte array.</summary>
    public byte[] Render(ReportSnapshot snapshot, ReportPdfRendererOptions? options = null)
    {
        using var stream = new MemoryStream();
        Render(snapshot, stream, options);
        return stream.ToArray();
    }

    /// <summary>Renders a report snapshot to a PDF stream.</summary>
    public void Render(ReportSnapshot snapshot, Stream destination, ReportPdfRendererOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(destination);

        options ??= new ReportPdfRendererOptions();
        using var catalog = new ReportPdfFontCatalog(options);
        using var document = SKDocument.CreatePdf(destination);
        foreach (var page in snapshot.Pages)
        {
            var width = ToPdfPoints(page.Width, options);
            var height = ToPdfPoints(page.Height, options);
            var canvas = document.BeginPage(width, height);
            canvas.Scale((float)options.PdfPointsPerCssPixel);
            DrawPage(canvas, page, catalog, options);
            document.EndPage();
        }

        document.Close();
    }

    /// <summary>Renders one snapshot page to a PNG byte array using the same Skia drawing path.</summary>
    public byte[] RenderPagePng(ReportSnapshotPage page, ReportPdfRendererOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        options ??= new ReportPdfRendererOptions();
        using var catalog = new ReportPdfFontCatalog(options);
        var info = new SKImageInfo(
            Math.Max(1, (int)Math.Ceiling(page.Width)),
            Math.Max(1, (int)Math.Ceiling(page.Height)),
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);
        DrawPage(surface.Canvas, page, catalog, options);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static float ToPdfPoints(double value, ReportPdfRendererOptions options)
        => (float)(value * options.PdfPointsPerCssPixel);

    private static void DrawPage(
        SKCanvas canvas,
        ReportSnapshotPage page,
        ReportPdfFontCatalog catalog,
        ReportPdfRendererOptions options)
    {
        var clipDepth = 0;
        foreach (var command in page.Commands)
        {
            switch (command.Type)
            {
                case ReportSnapshotCommandType.Rectangle:
                    DrawRectangle(canvas, command);
                    break;
                case ReportSnapshotCommandType.Line:
                    DrawLine(canvas, command);
                    break;
                case ReportSnapshotCommandType.Path:
                    DrawPath(canvas, command);
                    break;
                case ReportSnapshotCommandType.Image:
                    DrawImage(canvas, command);
                    break;
                case ReportSnapshotCommandType.TextRun:
                    DrawTextRun(canvas, command, catalog, options);
                    break;
                case ReportSnapshotCommandType.ClipPush:
                    canvas.Save();
                    clipDepth++;
                    canvas.ClipRect(Rect(command));
                    break;
                case ReportSnapshotCommandType.ClipPop:
                    if (clipDepth > 0)
                    {
                        canvas.Restore();
                        clipDepth--;
                    }

                    break;
            }
        }

        while (clipDepth > 0)
        {
            canvas.Restore();
            clipDepth--;
        }
    }

    private static void DrawRectangle(SKCanvas canvas, ReportSnapshotCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.Fill) && TryParseColor(command.Fill, out var fill))
        {
            using var fillPaint = new SKPaint
            {
                Color = fill,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawRect(Rect(command), fillPaint);
        }

        if (!string.IsNullOrWhiteSpace(command.Stroke) &&
            command.StrokeWidth > 0 &&
            TryParseColor(command.Stroke, out var stroke))
        {
            using var strokePaint = StrokePaint(stroke, command.StrokeWidth);
            canvas.DrawRect(Rect(command), strokePaint);
        }
    }

    private static void DrawLine(SKCanvas canvas, ReportSnapshotCommand command)
    {
        if (!TryParseColor(command.Stroke ?? command.Fill ?? "#111827", out var stroke))
        {
            return;
        }

        using var paint = StrokePaint(stroke, Math.Max(0.5, command.StrokeWidth));
        canvas.DrawLine(
            (float)command.X,
            (float)command.Y,
            (float)(command.X + command.Width),
            (float)(command.Y + command.Height),
            paint);
    }

    private static void DrawPath(SKCanvas canvas, ReportSnapshotCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.PathData))
        {
            return;
        }

        using var path = SKPath.ParseSvgPathData(command.PathData);
        if (path is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(command.Fill) && TryParseColor(command.Fill, out var fill))
        {
            using var fillPaint = new SKPaint
            {
                Color = fill,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawPath(path, fillPaint);
        }

        if (!string.IsNullOrWhiteSpace(command.Stroke) &&
            command.StrokeWidth > 0 &&
            TryParseColor(command.Stroke, out var stroke))
        {
            using var strokePaint = StrokePaint(stroke, command.StrokeWidth);
            canvas.DrawPath(path, strokePaint);
        }
    }

    private static void DrawImage(SKCanvas canvas, ReportSnapshotCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Source) ||
            !TryDecodeDataUri(command.Source, out var contentType, out var bytes))
        {
            return;
        }

        if (contentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
        {
            DrawSvg(canvas, command, bytes);
            return;
        }

        using var data = SKData.CreateCopy(bytes);
        using var image = SKImage.FromEncodedData(data);
        if (image is null)
        {
            return;
        }

        canvas.DrawImage(image, Rect(command));
    }

    private static void DrawSvg(SKCanvas canvas, ReportSnapshotCommand command, byte[] bytes)
    {
        var svgText = Encoding.UTF8.GetString(bytes);
        using var svg = new SKSvg();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svgText));
        var picture = svg.Load(stream);
        if (picture is null)
        {
            return;
        }

        var bounds = picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        canvas.Save();
        canvas.Translate((float)command.X, (float)command.Y);
        canvas.Scale((float)(command.Width / bounds.Width), (float)(command.Height / bounds.Height));
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    private static void DrawTextRun(
        SKCanvas canvas,
        ReportSnapshotCommand command,
        ReportPdfFontCatalog catalog,
        ReportPdfRendererOptions options)
    {
        var text = command.Text ?? string.Empty;
        var fontSize = Math.Max(1, command.FontSize ?? 12);
        var typeface = catalog.Resolve(
            command.FontFamily ?? options.DefaultFontFamily,
            command.FontWeight,
            command.FontStyle);
        using var paint = new SKPaint
        {
            Color = TryParseColor(command.Fill ?? "#111827", out var fill) ? fill : SKColors.Black,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        using var font = new SKFont(typeface, (float)fontSize)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true,
        };
        var naturalWidth = font.MeasureText(text, paint) + Math.Max(0, text.EnumerateRunes().Count() - 1) * command.LetterSpacing;
        var scaleX = naturalWidth > 0 && command.Width > 0 ? command.Width / naturalWidth : 1;
        var baseline = command.Baseline ?? command.Y + command.Height * 0.8;

        canvas.Save();
        canvas.Translate((float)command.X, (float)baseline);
        if (Math.Abs(command.Rotation) > 0.0001)
        {
            canvas.RotateDegrees((float)command.Rotation);
        }

        canvas.Scale((float)scaleX, 1);
        DrawTextWithLetterSpacing(canvas, text, command.LetterSpacing, font, paint);
        canvas.Restore();
    }

    private static void DrawTextWithLetterSpacing(SKCanvas canvas, string text, double letterSpacing, SKFont font, SKPaint paint)
    {
        if (Math.Abs(letterSpacing) < 0.0001)
        {
            canvas.DrawText(text, 0, 0, SKTextAlign.Left, font, paint);
            return;
        }

        var cursor = 0f;
        var runes = text.EnumerateRunes().ToArray();
        for (var index = 0; index < runes.Length; index++)
        {
            var glyph = runes[index].ToString();
            canvas.DrawText(glyph, cursor, 0, SKTextAlign.Left, font, paint);
            cursor += font.MeasureText(glyph, paint);
            if (index < runes.Length - 1)
            {
                cursor += (float)letterSpacing;
            }
        }
    }

    private static SKRect Rect(ReportSnapshotCommand command)
        => new(
            (float)command.X,
            (float)command.Y,
            (float)(command.X + command.Width),
            (float)(command.Y + command.Height));

    private static SKPaint StrokePaint(SKColor color, double strokeWidth)
        => new()
        {
            Color = color,
            IsAntialias = true,
            StrokeWidth = (float)strokeWidth,
            Style = SKPaintStyle.Stroke,
        };

    private static bool TryParseColor(string value, out SKColor color)
    {
        var text = value.Trim();
        if (text.StartsWith('#'))
        {
            return SKColor.TryParse(text, out color);
        }

        if (string.Equals(text, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = SKColors.Transparent;
            return true;
        }

        if (NamedColors.TryGetValue(text, out color))
        {
            return true;
        }

        return TryParseRgb(text, out color);
    }

    private static bool TryParseRgb(string value, out SKColor color)
    {
        color = SKColors.Transparent;
        if (!value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var start = value.IndexOf('(', StringComparison.Ordinal);
        var end = value.LastIndexOf(')');
        if (start < 0 || end <= start)
        {
            return false;
        }

        var parts = value[(start + 1)..end].Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length is not (3 or 4))
        {
            return false;
        }

        if (!byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var red) ||
            !byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var green) ||
            !byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var blue))
        {
            return false;
        }

        var alpha = (byte)255;
        if (parts.Length == 4 && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var alphaValue))
        {
            alpha = (byte)Math.Clamp((int)Math.Round(alphaValue * 255), 0, 255);
        }

        color = new SKColor(red, green, blue, alpha);
        return true;
    }

    private static bool TryDecodeDataUri(string source, out string contentType, out byte[] bytes)
    {
        contentType = string.Empty;
        bytes = [];
        if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var comma = source.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0)
        {
            return false;
        }

        var metadata = source[5..comma];
        contentType = metadata.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "application/octet-stream";
        var payload = source[(comma + 1)..];
        if (metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.TryFromBase64String(payload, bytes = new byte[Base64.GetMaxDecodedFromUtf8Length(payload.Length)], out var written) &&
                ResizeDecodedBytes(ref bytes, written);
        }

        bytes = Encoding.UTF8.GetBytes(WebUtility.UrlDecode(payload));
        return true;
    }

    private static bool ResizeDecodedBytes(ref byte[] bytes, int length)
    {
        Array.Resize(ref bytes, length);
        return true;
    }

    private static int ParseWeight(string? value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight))
        {
            return weight;
        }

        return string.Equals(value, "bold", StringComparison.OrdinalIgnoreCase) ? 700 : 400;
    }

    private static readonly IReadOnlyDictionary<string, SKColor> NamedColors = new Dictionary<string, SKColor>(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = SKColors.Black,
        ["white"] = SKColors.White,
        ["red"] = SKColors.Red,
        ["green"] = SKColors.Green,
        ["blue"] = SKColors.Blue,
    };

    private sealed class ReportPdfFontCatalog : IDisposable
    {
        private readonly ReportPdfRendererOptions _options;
        private readonly Dictionary<FontKey, (SKData Data, SKTypeface Typeface)> _embedded = new();
        private readonly Dictionary<FontKey, SKTypeface> _system = new();

        public ReportPdfFontCatalog(ReportPdfRendererOptions options)
        {
            _options = options;
            foreach (var font in options.Fonts)
            {
                var data = SKData.CreateCopy(font.Bytes);
                var typeface = SKTypeface.FromData(data);
                if (typeface is not null)
                {
                    _embedded[FontKey.Create(font.Family, font.Weight, font.Style)] = (data, typeface);
                }
                else
                {
                    data.Dispose();
                }
            }
        }

        public SKTypeface Resolve(string family, string? weight, string? style)
        {
            var key = FontKey.Create(family, ParseWeight(weight), style ?? "normal");
            if (_embedded.TryGetValue(key, out var exact))
            {
                return exact.Typeface;
            }

            var normalKey = FontKey.Create(family, 400, style ?? "normal");
            if (_embedded.TryGetValue(normalKey, out var normal))
            {
                return normal.Typeface;
            }

            var anyFamily = _embedded
                .Where(item => string.Equals(item.Key.Family, family, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => Math.Abs(item.Key.Weight - key.Weight))
                .Select(item => item.Value.Typeface)
                .FirstOrDefault();
            if (anyFamily is not null)
            {
                return anyFamily;
            }

            if (_system.TryGetValue(key, out var systemTypeface))
            {
                return systemTypeface;
            }

            var slant = string.Equals(style, "italic", StringComparison.OrdinalIgnoreCase)
                ? SKFontStyleSlant.Italic
                : SKFontStyleSlant.Upright;
            var fontStyle = new SKFontStyle(key.Weight, (int)SKFontStyleWidth.Normal, slant);
            systemTypeface = SKTypeface.FromFamilyName(
                string.IsNullOrWhiteSpace(family) ? _options.DefaultFontFamily : family,
                fontStyle);
            _system[key] = systemTypeface;
            return systemTypeface;
        }

        public void Dispose()
        {
            foreach (var (_, pair) in _embedded)
            {
                pair.Typeface.Dispose();
                pair.Data.Dispose();
            }

            foreach (var typeface in _system.Values)
            {
                typeface.Dispose();
            }
        }
    }

    private readonly record struct FontKey(string Family, int Weight, string Style)
    {
        public static FontKey Create(string family, int weight, string style)
            => new((family ?? string.Empty).Trim(), weight, (style ?? "normal").Trim().ToLowerInvariant());
    }
}
