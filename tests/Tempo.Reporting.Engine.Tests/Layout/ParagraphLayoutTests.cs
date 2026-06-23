using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Layout;

namespace Tempo.Reporting.Engine.Tests.Layout;

public sealed class ParagraphLayoutTests
{
    [Fact]
    public void Layout_AppliesHorizontalAlignmentAndParagraphSpacing()
    {
        var style = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 };
        var layout = ReportParagraphLayouter.Layout(
            [new ReportRichTextRun("one two", style)],
            new ReportParagraphLayoutOptions
            {
                X = 10,
                Y = 20,
                Width = 60,
                HorizontalAlignment = ReportHorizontalAlignment.Center,
                SpacingBefore = 3,
                SpacingAfter = 4,
            },
            new FixedTextMeasurer());

        layout.Lines.Should().ContainSingle();
        layout.Lines[0].X.Should().Be(22.5);
        layout.Lines[0].Baseline.Should().Be(31);
        layout.TotalHeight.Should().Be(17);
    }

    [Fact]
    public void Layout_JustifiesNonFinalSoftWrappedLinesByExpandingSpacesOnly()
    {
        var style = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 };

        var layout = ReportParagraphLayouter.Layout(
            [new ReportRichTextRun("one two three four", style)],
            new ReportParagraphLayoutOptions
            {
                Width = 75,
                HorizontalAlignment = ReportHorizontalAlignment.Justify,
            },
            new FixedTextMeasurer());

        var firstLine = layout.Lines[0];
        firstLine.Text.Should().Be("one two three");
        firstLine.JustificationSpacing.Should().Be(5);
        firstLine.Runs.Where(run => !run.IsWhitespace).Select(run => (run.Text, run.X, run.Width))
            .Should().Equal(("one", 0, 15), ("two", 25, 15), ("three", 50, 25));
        layout.Lines[1].JustificationSpacing.Should().Be(0);
    }
}
