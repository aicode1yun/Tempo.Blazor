using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Reporting.Engine.Tests.Layout;

public sealed class TextBoxLayoutTests
{
    [Fact]
    public void Layout_AppliesPaddingVerticalAlignmentAndCanGrow()
    {
        var style = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 };
        var layout = ReportTextBoxLayouter.Layout(
            new ReportTextBoxLayoutRequest
            {
                Id = "box",
                X = 10,
                Y = 20,
                Width = 52,
                Height = 18,
                Padding = new ReportThickness(4, 3, 4, 3),
                VerticalAlignment = ReportVerticalAlignment.Bottom,
                CanGrow = true,
                Runs = [new ReportRichTextRun("one two three", style)],
            },
            new FixedTextMeasurer());

        layout.Lines.Should().HaveCount(2);
        layout.ContentRectangle.X.Should().Be(14);
        layout.ContentRectangle.Y.Should().Be(23);
        layout.Lines[0].X.Should().Be(14);
        layout.ActualHeight.Should().Be(26);
        layout.Clipped.Should().BeFalse();
    }

    [Fact]
    public void Layout_WhenCannotGrow_ClipsAndEllipsizesLastVisibleLine()
    {
        var style = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 };
        var layout = ReportTextBoxLayouter.Layout(
            new ReportTextBoxLayoutRequest
            {
                Id = "box",
                X = 0,
                Y = 0,
                Width = 45,
                Height = 16,
                Padding = new ReportThickness(2),
                Border = ReportBorder.All("#111827", 1),
                CanGrow = false,
                Runs = [new ReportRichTextRun("one two three four", style)],
            },
            new FixedTextMeasurer());

        layout.Clipped.Should().BeTrue();
        layout.Lines.Should().ContainSingle();
        layout.Lines[0].Text.Should().EndWith("\u2026");

        var commands = layout.ToSnapshotCommands().ToArray();
        commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.ClipPush);
        commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.ClipPop);
        commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.Rectangle && command.Stroke == "#111827");
    }
}
