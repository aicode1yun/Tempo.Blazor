using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Layout;

namespace Tempo.Reporting.Engine.Tests.Layout;

public sealed class LineBreakerTests
{
    [Fact]
    public void BreakLines_WrapsAtSpacesHyphensCjkBoundariesAndHardBreaks()
    {
        var style = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 };
        var lines = ReportLineBreaker.BreakLines(
            [new ReportRichTextRun("Alpha beta-gamma\n日本語", style)],
            maxWidth: 25,
            new FixedTextMeasurer());

        lines.Select(line => line.Text).Should().Equal("Alpha", "beta-", "gamma", "日本", "語");
        lines.Select(line => line.Width).Should().Equal(25, 25, 25, 20, 10);
        lines[2].BreakKind.Should().Be(ReportLineBreakKind.Hard);
    }

    [Fact]
    public void BreakLines_DoesNotHyphenateLongLatinWords()
    {
        var style = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 };

        var lines = ReportLineBreaker.BreakLines(
            [new ReportRichTextRun("Supercalifragilistic", style)],
            maxWidth: 25,
            new FixedTextMeasurer());

        lines.Should().ContainSingle();
        lines[0].Text.Should().Be("Supercalifragilistic");
        lines[0].Width.Should().BeGreaterThan(25);
    }
}
