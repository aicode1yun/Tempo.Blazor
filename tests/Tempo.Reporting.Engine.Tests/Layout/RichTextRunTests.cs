using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Reporting.Engine.Tests.Layout;

public sealed class RichTextRunTests
{
    [Fact]
    public void RichTextRun_MapsDefinitionTextStyleToMeasurementAndSnapshotCommand()
    {
        var style = new ReportTextStyle
        {
            FontFamily = "Inter",
            FontSize = 14,
            Bold = true,
            Italic = true,
            Underline = true,
            StrikeThrough = true,
            Color = "#1f2937",
            BackgroundColor = "#fef3c7",
        };
        var run = new ReportRichTextRun("Alert", style, letterSpacing: 0.5);

        var request = run.ToMeasureRequest();
        var command = run.ToSnapshotCommand("r1", x: 10, baseline: 30, width: 35, height: 14);

        request.Text.Should().Be("Alert");
        request.FontFamily.Should().Be("Inter");
        request.FontSize.Should().Be(14);
        request.Bold.Should().BeTrue();
        request.Italic.Should().BeTrue();
        request.LetterSpacing.Should().Be(0.5);

        command.Type.Should().Be(ReportSnapshotCommandType.TextRun);
        command.FontWeight.Should().Be("700");
        command.FontStyle.Should().Be("italic");
        command.Fill.Should().Be("#1f2937");
        command.Highlight.Should().Be("#fef3c7");
        command.Underline.Should().BeTrue();
        command.StrikeThrough.Should().BeTrue();
    }
}
