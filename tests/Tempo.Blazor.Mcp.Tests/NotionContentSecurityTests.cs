using System.Diagnostics;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Tests;

public sealed class NotionContentSecurityTests
{
    [Theory]
    [InlineData("  #1F4E78  ", "#1f4e78")]
    [InlineData("red", "red")]
    [InlineData("rgb(220, 38, 38)", "rgb(220, 38, 38)")]
    [InlineData("hsl(210 50% 40% / 75%)", "hsl(210 50% 40% / 75%)")]
    public void TryNormalizeColor_AcceptsOnlyLiteralColors(
        string input,
        string expected)
    {
        NotionCssNormalizer.TryNormalizeColor(input, out var normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("var(--tm-color-primary)")]
    [InlineData("url(https://evil.test/x)")]
    [InlineData("red;position:fixed")]
    [InlineData("inherit")]
    [InlineData("currentColor")]
    [InlineData("\" onmouseover=\"alert(1)")]
    public void TryNormalizeColor_RejectsExternalStateAndInjection(string input)
    {
        NotionCssNormalizer.TryNormalizeColor(input, out var normalized).Should().BeFalse();
        normalized.Should().BeNull();
    }

    [Fact]
    public void TryProject_NullCollectionsFromHistoricalProvider_ReturnStructuredIssue()
    {
        var rows = new[]
        {
            new NotionAuthoringTableRow
            {
                Cells = new NotionAuthoringTableCell[] { null! }
            }
        };

        var action = () => NotionTableGridProjector.TryProject(
            rows,
            1,
            "$.rows",
            out _,
            out var issues)
            ? issues
            : issues;

        action.Should().NotThrow();
        action().Should().ContainSingle(issue =>
            issue.Code == "table_cell_required" &&
            issue.Path == "$.rows[0].cells[0]");
    }

    [Fact]
    public void SanitizeBlockContent_DeterministicFuzzIsBoundedAndIdempotent()
    {
        const string alphabet = "<>/'\"=;:() abcdef0123456789";
        var random = new Random(0x2701);
        var stopwatch = Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            var input = new string(
                Enumerable.Range(0, random.Next(0, 512))
                    .Select(_ => alphabet[random.Next(alphabet.Length)])
                    .ToArray());
            var once = NotionHtmlSanitizer.SanitizeBlockContent(input);
            var twice = NotionHtmlSanitizer.SanitizeBlockContent(once);

            twice.Should().Be(once);
        }

        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        allocated.Should().BeLessThan(128 * 1024 * 1024);
    }
}
