using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Services;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class CsvToTimelineGeneratorTests
{
    [Fact]
    public void Generate_CreatesNodesSortedByDate()
    {
        var parseResult = new CsvParseResult
        {
            Headers = ["Date", "Event"],
            Rows =
            [
                ["2026-03-01", "Beta"],
                ["2026-01-01", "Alpha"],
                ["2026-02-01", "Gamma"]
            ],
            DetectedDelimiter = ','
        };

        var mappings = new List<CsvColumnMapping>
        {
            new() { SemanticField = "Date", SelectedColumn = "Date", IsRequired = true },
            new() { SemanticField = "Event", SelectedColumn = "Event", IsRequired = true }
        };

        var doc = CsvToTimelineGenerator.Generate(parseResult, mappings);

        doc.ActivePage.Nodes.Count.Should().Be(3);
        doc.ActivePage.Edges.Count.Should().Be(0);

        var names = doc.ActivePage.Nodes.Select(n => n.Data["name"].ToString()).ToList();
        names[0].Should().Contain("Alpha");
        names[1].Should().Contain("Gamma");
        names[2].Should().Contain("Beta");
    }

    [Fact]
    public void Generate_AssignsSequentialXPositions()
    {
        var parseResult = new CsvParseResult
        {
            Headers = ["Date", "Event"],
            Rows = [["2026-01-01", "Launch"]],
            DetectedDelimiter = ','
        };

        var mappings = new List<CsvColumnMapping>
        {
            new() { SemanticField = "Date", SelectedColumn = "Date", IsRequired = true },
            new() { SemanticField = "Event", SelectedColumn = "Event", IsRequired = true }
        };

        var doc = CsvToTimelineGenerator.Generate(parseResult, mappings);
        var node = doc.ActivePage.Nodes[0];
        node.X.Should().Be(40);
        node.Y.Should().Be(100);
        node.W.Should().Be(200);
        node.H.Should().Be(40);
    }

    [Fact]
    public void Generate_UsesTimelineBarStencil()
    {
        var parseResult = new CsvParseResult
        {
            Headers = ["Date", "Event"],
            Rows = [["2026-01-01", "Launch"]],
            DetectedDelimiter = ','
        };

        var mappings = new List<CsvColumnMapping>
        {
            new() { SemanticField = "Date", SelectedColumn = "Date", IsRequired = true },
            new() { SemanticField = "Event", SelectedColumn = "Event", IsRequired = true }
        };

        var doc = CsvToTimelineGenerator.Generate(parseResult, mappings);
        doc.ActivePage.Nodes[0].StencilId.Should().Be("project.timeline-bar");
    }
}
