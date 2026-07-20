using System.Text;
using SkiaSharp;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Pdf;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Reporting.Engine.Tests.Pdf;

/// <summary>
/// End-to-end renderer tests for the bidi-aware shaped text path. These assert that right-to-left
/// content draws ink through the shaped path and that pure left-to-right content still renders,
/// without asserting exact pixels (platform advance differences).
/// </summary>
public sealed class ReportPdfRendererRtlTests
{
    private const string Hebrew = "שלום עולם"; // shalom olam

    [Fact]
    public void RenderPagePng_HebrewTextRun_DrawsInkThroughShapedPath()
    {
        var page = new ReportSnapshotPage
        {
            PageNumber = 1,
            Width = 240,
            Height = 60,
            Commands =
            [
                ReportSnapshotCommand.Rectangle("bg", 0, 0, 240, 60, "#ffffff"),
                ReportSnapshotCommand.TextRun("heb", Hebrew, 20, 40, 200, 24, "Inter", 20, "#111827"),
            ],
        };

        var png = new ReportPdfRenderer().RenderPagePng(page);

        using var bitmap = SKBitmap.Decode(png);
        CountInk(bitmap).Should().BeGreaterThan(50, "the shaped Hebrew run must draw visible glyphs");
    }

    [Fact]
    public void RenderPagePng_LatinTextRun_StillDrawsInk()
    {
        var page = new ReportSnapshotPage
        {
            PageNumber = 1,
            Width = 240,
            Height = 60,
            Commands =
            [
                ReportSnapshotCommand.Rectangle("bg", 0, 0, 240, 60, "#ffffff"),
                ReportSnapshotCommand.TextRun("lat", "Hello World", 20, 40, 200, 24, "Inter", 20, "#111827"),
            ],
        };

        var png = new ReportPdfRenderer().RenderPagePng(page);

        using var bitmap = SKBitmap.Decode(png);
        CountInk(bitmap).Should().BeGreaterThan(50);
    }

    [Fact]
    public void Render_MixedBidiText_ProducesValidPdf()
    {
        var page = new ReportSnapshotPage
        {
            PageNumber = 1,
            Width = 240,
            Height = 60,
            Commands =
            [
                ReportSnapshotCommand.Rectangle("bg", 0, 0, 240, 60, "#ffffff"),
                ReportSnapshotCommand.TextRun("mix", "Total " + Hebrew + " (42)", 10, 40, 220, 24, "Inter", 16, "#111827"),
            ],
        };

        var snapshot = new ReportSnapshot { SnapshotId = "rtl", Pages = [page] };

        var pdf = new ReportPdfRenderer().Render(snapshot);

        pdf.Should().StartWith(Encoding.ASCII.GetBytes("%PDF"));
    }

    [Fact]
    public void RenderPagePng_ExplicitRtlDirection_DrawsInk()
    {
        var page = new ReportSnapshotPage
        {
            PageNumber = 1,
            Width = 240,
            Height = 60,
            Commands =
            [
                ReportSnapshotCommand.Rectangle("bg", 0, 0, 240, 60, "#ffffff"),
                ReportSnapshotCommand.TextRun(
                    "rtl",
                    "12 34",
                    20,
                    40,
                    200,
                    24,
                    "Inter",
                    20,
                    "#111827",
                    textDirection: ReportTextDirection.Rtl),
            ],
        };

        var png = new ReportPdfRenderer().RenderPagePng(page);

        using var bitmap = SKBitmap.Decode(png);
        CountInk(bitmap).Should().BeGreaterThan(50);
    }

    private static int CountInk(SKBitmap bitmap)
    {
        var ink = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha == 0)
                {
                    continue;
                }

                if (pixel.Red < 128 && pixel.Green < 128 && pixel.Blue < 128)
                {
                    ink++;
                }
            }
        }

        return ink;
    }
}
