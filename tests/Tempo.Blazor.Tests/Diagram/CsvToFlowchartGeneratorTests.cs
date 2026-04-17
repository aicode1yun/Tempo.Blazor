using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Services;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class CsvToFlowchartGeneratorTests
{
    [Fact]
    public void Generate_CreatesNodesAndEdges_WithLabels()
    {
        var parseResult = new CsvParseResult
        {
            Headers = ["From", "To", "Label"],
            Rows =
            [
                ["Start", "Process", "begin"],
                ["Process", "End", "finish"]
            ],
            DetectedDelimiter = ','
        };

        var mappings = new List<CsvColumnMapping>
        {
            new() { SemanticField = "From", SelectedColumn = "From", IsRequired = true },
            new() { SemanticField = "To", SelectedColumn = "To", IsRequired = true },
            new() { SemanticField = "Label", SelectedColumn = "Label", IsRequired = false }
        };

        var doc = CsvToFlowchartGenerator.Generate(parseResult, mappings);

        doc.ActivePage.Nodes.Count.Should().Be(3);
        doc.ActivePage.Edges.Count.Should().Be(2);
        doc.ActivePage.Edges.Should().Contain(e => e.Label == "begin");
    }

    [Fact]
    public void Generate_WorksWithoutLabelMapping()
    {
        var parseResult = new CsvParseResult
        {
            Headers = ["From", "To"],
            Rows = [["A", "B"]],
            DetectedDelimiter = ','
        };

        var mappings = new List<CsvColumnMapping>
        {
            new() { SemanticField = "From", SelectedColumn = "From", IsRequired = true },
            new() { SemanticField = "To", SelectedColumn = "To", IsRequired = true }
        };

        var doc = CsvToFlowchartGenerator.Generate(parseResult, mappings);
        doc.ActivePage.Nodes.Count.Should().Be(2);
        doc.ActivePage.Edges.Count.Should().Be(1);
    }

    [Fact]
    public void Generate_SkipsSelfLoops()
    {
        var parseResult = new CsvParseResult
        {
            Headers = ["From", "To"],
            Rows = [["A", "A"]],
            DetectedDelimiter = ','
        };

        var mappings = new List<CsvColumnMapping>
        {
            new() { SemanticField = "From", SelectedColumn = "From", IsRequired = true },
            new() { SemanticField = "To", SelectedColumn = "To", IsRequired = true }
        };

        var doc = CsvToFlowchartGenerator.Generate(parseResult, mappings);
        doc.ActivePage.Nodes.Count.Should().Be(1);
        doc.ActivePage.Edges.Count.Should().Be(0);
    }
}
