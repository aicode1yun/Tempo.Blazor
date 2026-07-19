using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Reporting.Engine.Pdf;
using Xunit.Abstractions;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Perf gate for the Jint-hosted headless layout (phase 2): a document beyond the scale of the
/// 21-page layout parity fixture must lay out within a hard budget. Measured on the dev machine
/// 2026-07-19 (Jint 4.13, .NET 10, Debug, 54-page document): cold call (engine creation + bundle
/// evaluation + layout) ≈ 2.2 s, warm pooled call ≈ 0.9 s (≈ 17 ms/page); a 369-page stress run
/// measured 5.4 s cold / 3.8 s warm. Budgets carry ~6× CI headroom; treat a budget bust as a
/// real regression (bundle growth, measurement cache loss), not noise.
/// </summary>
public class JintDocumentLayoutBenchmarkTests(ITestOutputHelper output)
{
    private const int MinimumPages = 21;

    // Budgets are sized to catch order-of-magnitude regressions (bundle growth, measurement
    // cache loss), not scheduler noise: constrained CI containers running the full suite in
    // parallel have been observed to triple the dev-machine numbers.
    private static readonly TimeSpan ColdBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WarmBudget = TimeSpan.FromSeconds(15);

    [Fact]
    public void TwentyOnePageDocument_LaysOutWithinBudget()
    {
        using var service = new JintDocumentLayoutEngine();
        var fonts = new[]
        {
            new ReportPdfFontFace(
                "Dancing Script",
                400,
                "normal",
                File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "Fonts", "DancingScript-VariableFont_wght.ttf"))),
        };
        var document = CreateLargeDocument();

        var cold = Stopwatch.StartNew();
        var coldJson = service.GenerateLayoutSnapshotJson(document, fonts: fonts);
        cold.Stop();

        var warm = Stopwatch.StartNew();
        var warmJson = service.GenerateLayoutSnapshotJson(document, fonts: fonts);
        warm.Stop();

        using var snapshot = JsonDocument.Parse(warmJson);
        var pageCount = snapshot.RootElement.GetProperty("pageCount").GetInt32();
        output.WriteLine($"pages={pageCount} cold={cold.ElapsedMilliseconds} ms warm={warm.ElapsedMilliseconds} ms " +
            $"snapshotBytes={warmJson.Length} enginesCreated={service.CreatedEngineCount}");

        pageCount.Should().BeGreaterThanOrEqualTo(MinimumPages, "the benchmark must exercise a parity-fixture-scale document");
        warmJson.Should().Be(coldJson, "layout must stay deterministic under the benchmark load");
        service.CreatedEngineCount.Should().Be(1, "both calls must reuse one pooled engine");

        cold.Elapsed.Should().BeLessThan(ColdBudget,
            $"cold layout (engine + bundle evaluation + {pageCount} pages) must stay within {ColdBudget.TotalSeconds:0} s");
        warm.Elapsed.Should().BeLessThan(WarmBudget,
            $"warm pooled layout of {pageCount} pages must stay within {WarmBudget.TotalSeconds:0} s");
    }

    private static DocumentEditorDocument CreateLargeDocument()
    {
        var document = DocumentEditorDocument.Empty("headless-layout-benchmark");
        document.Theme.BodyFontFamily = "Dancing Script";
        document.Blocks = [];

        for (var index = 0; index < 5; index++)
        {
            document.Blocks.Add(new DocumentBlock
            {
                Type = DocumentBlockType.Heading,
                Order = document.Blocks.Count,
                Content = new HeadingBlockContent { Level = 2, Inlines = [new TextRun { Text = $"Kapitola {index + 1}" }] },
            });

            for (var paragraph = 0; paragraph < 14; paragraph++)
            {
                document.Blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Order = document.Blocks.Count,
                    Content = new ParagraphBlockContent
                    {
                        Inlines =
                        [
                            new TextRun
                            {
                                Text = $"Odstavec {paragraph + 1} kapitoly {index + 1}: příliš žluťoučký kůň úpěl " +
                                    "ďábelské ódy a pokračoval dalším textem, aby řádkový zlom měl dost práce na více řádcích.",
                            },
                        ],
                    },
                });
            }
        }

        return document;
    }
}
