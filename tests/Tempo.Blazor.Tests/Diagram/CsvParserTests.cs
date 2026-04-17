using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Services;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class CsvParserTests
{
    [Fact]
    public void Parse_DetectsCommaDelimiter()
    {
        const string csv = "Name,Manager\nAlice,Bob\nBob,\n";
        var result = CsvParser.Parse(csv);

        result.DetectedDelimiter.Should().Be(',');
        result.Headers.Should().BeEquivalentTo(["Name", "Manager"]);
        result.Rows.Count.Should().Be(2);
        result.Rows[0][0].Should().Be("Alice");
    }

    [Fact]
    public void Parse_DetectsSemicolonDelimiter()
    {
        const string csv = "Name;Manager\nAlice;Bob\nBob;\n";
        var result = CsvParser.Parse(csv);

        result.DetectedDelimiter.Should().Be(';');
        result.Headers.Should().BeEquivalentTo(["Name", "Manager"]);
        result.Rows.Count.Should().Be(2);
    }

    [Fact]
    public void Parse_DetectsTabDelimiter()
    {
        const string csv = "Name\tManager\nAlice\tBob\n";
        var result = CsvParser.Parse(csv);

        result.DetectedDelimiter.Should().Be('\t');
        result.Headers.Should().BeEquivalentTo(["Name", "Manager"]);
        result.Rows.Count.Should().Be(1);
    }

    [Fact]
    public void Parse_HandlesQuotedFieldsWithComma()
    {
        const string csv = "Name,Description\n\"Doe, John\",\"Manager, Engineering\"\n";
        var result = CsvParser.Parse(csv);

        result.Headers.Should().BeEquivalentTo(["Name", "Description"]);
        result.Rows[0][0].Should().Be("Doe, John");
        result.Rows[0][1].Should().Be("Manager, Engineering");
    }

    [Fact]
    public void Parse_ThrowsOnEmptyInput()
    {
        var act = () => CsvParser.Parse("   ");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_ThrowsOnSingleColumn()
    {
        var act = () => CsvParser.Parse("Name\nAlice\n");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_RespectsMaxRowsLimit()
    {
        var rows = Enumerable.Range(0, 10).Select(i => $"A{i},B{i}");
        var csv = "ColA,ColB\n" + string.Join("\n", rows);
        var result = CsvParser.Parse(csv);

        result.Rows.Count.Should().Be(10);
    }
}
