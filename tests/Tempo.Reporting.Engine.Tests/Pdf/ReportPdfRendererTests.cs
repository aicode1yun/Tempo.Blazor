using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Pdf;
using Tempo.Reporting.Engine.Processing;
using Tempo.Reporting.Engine.Snapshot;
using Tempo.Reporting.Engine.Tests.Layout;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;

namespace Tempo.Reporting.Engine.Tests.Pdf;

public sealed class ReportPdfRendererTests
{
    [Fact]
    public void Render_WritesPagesMediaBoxesAndSnapshotPrimitives()
    {
        var snapshot = new ReportSnapshot
        {
            SnapshotId = "pdf-structure",
            Pages =
            [
                Page(
                    1,
                    320,
                    240,
                    ReportSnapshotCommand.Rectangle("bg", 0, 0, 320, 240, "#ffffff"),
                    ReportSnapshotCommand.Rectangle("box", 20, 20, 80, 40, "#eff6ff", "#1d4ed8", 1),
                    ReportSnapshotCommand.Line("rule", 20, 78, 120, 0, "#0f172a", 1),
                    new ReportSnapshotCommand
                    {
                        Id = "ellipse",
                        Type = ReportSnapshotCommandType.Path,
                        PathData = "M 200 50 C 220 30 260 30 280 50 C 260 70 220 70 200 50 Z",
                        Fill = "#dbeafe",
                        Stroke = "#2563eb",
                        StrokeWidth = 1,
                    },
                    ReportSnapshotCommand.ClipPush("clip", 20, 100, 120, 24),
                    ReportSnapshotCommand.TextRun("text", "Clipped PDF text", 20, 118, 96, 16, "Inter", 12, "#111827"),
                    ReportSnapshotCommand.ClipPop("clip-end"),
                    ReportSnapshotCommand.Image("png", 248, 18, 32, 32, TinyPngDataUri())),
                Page(
                    2,
                    200,
                    100,
                    ReportSnapshotCommand.Rectangle("p2-bg", 0, 0, 200, 100, "#ffffff"),
                    ReportSnapshotCommand.TextRun("p2-text", "Second page", 20, 40, 80, 14, "Inter", 11, "#111827")),
            ],
        };

        var pdf = new ReportPdfRenderer().Render(snapshot);
        var text = PdfText(pdf);

        pdf.Should().StartWith(Encoding.ASCII.GetBytes("%PDF"));
        CountPages(text).Should().Be(2);
        ExtractMediaBoxes(text).Should().Contain(["0 0 240 180", "0 0 150 75"]);
        text.Should().Contain("/Subtype /Image");
        text.Should().Contain("/ExtGState");
    }

    [Fact]
    public void Render_EmbedsSuppliedFontSubsetInsteadOfBuiltinPdfFont()
    {
        var regularFontPath = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
        var boldFontPath = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";
        File.Exists(regularFontPath).Should().BeTrue($"{regularFontPath} is available in the Linux test image");
        File.Exists(boldFontPath).Should().BeTrue($"{boldFontPath} is available in the Linux test image");
        var regularFont = File.ReadAllBytes(regularFontPath);
        var boldFont = File.ReadAllBytes(boldFontPath);
        var snapshot = new ReportSnapshot
        {
            SnapshotId = "embedded-font",
            Pages =
            [
                Page(
                    1,
                    240,
                    120,
                    ReportSnapshotCommand.Rectangle("bg", 0, 0, 240, 120, "#ffffff"),
                    ReportSnapshotCommand.TextRun("script", "Embedded subset", 20, 60, 126, 24, "Tempo F8 Script", 20, "#111827", "700")),
            ],
        };

        var pdf = new ReportPdfRenderer().Render(
            snapshot,
            new ReportPdfRendererOptions
            {
                Fonts =
                [
                    new ReportPdfFontFace("Tempo F8 Script", 400, "normal", regularFont),
                    new ReportPdfFontFace("Tempo F8 Script", 700, "normal", boldFont),
                ],
            });
        var text = PdfText(pdf);

        text.Should().Contain("/FontFile");
        Regex.IsMatch(
                text,
                @"/BaseFont\s*/[A-Z]{6}\+[^\s/]*DejaVuSans",
                RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
                TimeSpan.FromSeconds(1))
            .Should()
            .BeTrue(text);
        text.Should().NotContain("/BaseFont /Helvetica");
    }

    [Fact]
    public void RenderPagePng_RasterizesSnapshotAtCssPixelSize()
    {
        var page = Page(
            1,
            96,
            48,
            ReportSnapshotCommand.Rectangle("bg", 0, 0, 96, 48, "#ffffff"),
            ReportSnapshotCommand.Rectangle("accent", 8, 8, 80, 24, "#2563eb"));

        var png = new ReportPdfRenderer().RenderPagePng(page);

        using var bitmap = SKBitmap.Decode(png);
        bitmap.Width.Should().Be(96);
        bitmap.Height.Should().Be(48);
        bitmap.GetPixel(12, 12).Red.Should().BeGreaterThan(20);
        bitmap.GetPixel(12, 12).Blue.Should().BeGreaterThan(120);
    }

    [Fact]
    public void RenderPagePng_RasterizesEngineDrawnChartSnapshot()
    {
        var context = ChartContext();
        var column = Chart("status-column", ReportChartType.Column, "Revenue by status", "#2563eb");
        var donut = Chart("status-donut", ReportChartType.Donut, "Status mix", "#f59e0b") with
        {
            ShowValueAxis = false,
        };
        var commands = new List<ReportSnapshotCommand>
        {
            ReportSnapshotCommand.Rectangle("bg", 0, 0, 420, 220, "#ffffff"),
        };
        commands.AddRange(ReportChartLayouter.ToSnapshotCommands(column, context, 20, 20, "column", new FixedTextMeasurer()));
        commands.AddRange(ReportChartLayouter.ToSnapshotCommands(donut, context, 230, 20, "donut", new FixedTextMeasurer()));
        var page = Page(1, 420, 220, [.. commands]);

        var png = new ReportPdfRenderer().RenderPagePng(page);

        using var bitmap = SKBitmap.Decode(png);
        var stats = CountChartPixels(bitmap);
        stats.NonWhite.Should().BeGreaterThan(1_000);
        stats.Blue.Should().BeGreaterThan(20);
        stats.Amber.Should().BeGreaterThan(20);
    }

    private static ReportSnapshotPage Page(int pageNumber, double width, double height, params ReportSnapshotCommand[] commands)
        => new()
        {
            PageNumber = pageNumber,
            Width = width,
            Height = height,
            Commands = [.. commands],
        };

    private static ReportChartElement Chart(string id, ReportChartType type, string title, string color)
        => new()
        {
            Id = id,
            Width = 170,
            Height = 150,
            ChartType = type,
            DataSetName = "Sales",
            Title = title,
            CategoryAxisTitle = "Status",
            ValueAxisTitle = "Revenue",
            ColorPalette = ["#2563eb", "#14b8a6", "#f59e0b"],
            Series =
            [
                new ReportChartSeries
                {
                    Name = "Actual",
                    CategoryExpression = "=Fields.Status",
                    ValueExpression = "=Fields.Total",
                    Color = color,
                },
            ],
        };

    private static ReportProcessingContext ChartContext()
    {
        var dataSet = new ProcessedDataSet(
            "Sales",
            [
                new ReportDataColumn("Status", DataFieldType.String),
                new ReportDataColumn("Total", DataFieldType.Number),
            ],
            [
                Row("Open", 100m),
                Row("Closed", 50m),
                Row("Open", 75m),
            ]);
        return new ReportProcessingContext(
            new ReportExecutionContext("tenant", "user", "en-US"),
            dataSets: new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal) { ["Sales"] = dataSet });
    }

    private static ProcessedDataRow Row(string status, decimal total)
        => new(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Status"] = status,
            ["Total"] = total,
        });

    private static (int NonWhite, int Blue, int Amber) CountChartPixels(SKBitmap bitmap)
    {
        var nonWhite = 0;
        var blue = 0;
        var amber = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha == 0)
                {
                    continue;
                }

                if (pixel.Red <= 245 || pixel.Green <= 245 || pixel.Blue <= 245)
                {
                    nonWhite++;
                }

                if (pixel.Blue > 150 && pixel.Red < 90 && pixel.Green < 150)
                {
                    blue++;
                }

                if (pixel.Red > 180 && pixel.Green > 100 && pixel.Blue < 80)
                {
                    amber++;
                }
            }
        }

        return (nonWhite, blue, amber);
    }

    private static int CountPages(string pdfText)
        => Regex.Matches(
                pdfText,
                @"/Type\s*/Page\b",
                RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
                TimeSpan.FromSeconds(1))
            .Count;

    private static List<string> ExtractMediaBoxes(string pdfText)
        => Regex.Matches(
                pdfText,
                @"/MediaBox\s*\[\s*(?<box>[^\]]+)\]",
                RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
                TimeSpan.FromSeconds(1))
            .Select(static match => Regex.Replace(
                match.Groups["box"].Value.Trim(),
                @"\s+",
                " ",
                RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
                TimeSpan.FromSeconds(1)))
            .ToList();

    private static string PdfText(byte[] bytes)
        => Encoding.Latin1.GetString(bytes);

    private static string TinyPngDataUri()
        => "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lIdt3wAAAABJRU5ErkJggg==";
}
