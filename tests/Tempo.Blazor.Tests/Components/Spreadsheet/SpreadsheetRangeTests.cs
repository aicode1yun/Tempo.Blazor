using FluentAssertions;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetRangeTests
{
    [Theory]
    [InlineData("A1", 0, 0, 0, 0)]
    [InlineData("B2", 1, 1, 1, 1)]
    [InlineData("Z1", 0, 25, 0, 25)]
    [InlineData("AA1", 0, 26, 0, 26)]
    [InlineData("AB10", 9, 27, 9, 27)]
    [InlineData("A1:B2", 0, 0, 1, 1)]
    [InlineData("C3:E5", 2, 2, 4, 4)]
    [InlineData("A1:Z100", 0, 0, 99, 25)]
    public void Parse_ValidRange_ReturnsCorrectIndices(string range, int startRow, int startCol, int endRow, int endCol)
    {
        var result = SpreadsheetRange.Parse(range);

        result.StartRow.Should().Be(startRow);
        result.StartCol.Should().Be(startCol);
        result.EndRow.Should().Be(endRow);
        result.EndCol.Should().Be(endCol);
    }

    [Theory]
    [InlineData("$A$1", 0, 0, 0, 0)]
    [InlineData("$A$1:$B$2", 0, 0, 1, 1)]
    public void Parse_AbsoluteReferences_StripsDollarSigns(string range, int startRow, int startCol, int endRow, int endCol)
    {
        var result = SpreadsheetRange.Parse(range);

        result.StartRow.Should().Be(startRow);
        result.StartCol.Should().Be(startCol);
        result.EndRow.Should().Be(endRow);
        result.EndCol.Should().Be(endCol);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(27, "AB")]
    [InlineData(51, "AZ")]
    [InlineData(52, "BA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void ColumnIndexToLetters_ReturnsCorrectLetters(int index, string expected)
    {
        SpreadsheetRange.ColumnIndexToLetters(index).Should().Be(expected);
    }

    [Theory]
    [InlineData("A", 0)]
    [InlineData("Z", 25)]
    [InlineData("AA", 26)]
    [InlineData("AB", 27)]
    [InlineData("AZ", 51)]
    [InlineData("BA", 52)]
    [InlineData("ZZ", 701)]
    [InlineData("AAA", 702)]
    public void ColumnLettersToIndex_ReturnsCorrectIndex(string letters, int expected)
    {
        SpreadsheetRange.ColumnLettersToIndex(letters).Should().Be(expected);
    }

    [Fact]
    public void CellRefs_SingleCell_ReturnsOneItem()
    {
        var range = SpreadsheetRange.Parse("A1");
        var refs = range.CellRefs.ToList();

        refs.Should().ContainSingle().Which.Should().Be("A1");
    }

    [Fact]
    public void CellRefs_Range_ReturnsCorrectRefsInRowMajorOrder()
    {
        var range = SpreadsheetRange.Parse("A1:B2");
        var refs = range.CellRefs.ToList();

        refs.Should().Equal("A1", "B1", "A2", "B2");
    }

    [Fact]
    public void CellRefs_LargerRange_ReturnsCorrectCount()
    {
        var range = SpreadsheetRange.Parse("A1:C3");

        range.RowCount.Should().Be(3);
        range.ColumnCount.Should().Be(3);
        range.CellCount.Should().Be(9);
        range.CellRefs.Should().HaveCount(9);
    }

    [Fact]
    public void ToString_SingleCell_ReturnsA1Notation()
    {
        var range = new SpreadsheetRange(0, 0, 0, 0);
        range.ToString().Should().Be("A1");
    }

    [Fact]
    public void ToString_Range_ReturnsA1ColonB2Notation()
    {
        var range = new SpreadsheetRange(0, 0, 1, 1);
        range.ToString().Should().Be("A1:B2");
    }

    [Fact]
    public void Parse_NullOrEmpty_ThrowsArgumentException()
    {
        Action act1 = () => SpreadsheetRange.Parse(null!);
        Action act2 = () => SpreadsheetRange.Parse("");
        Action act3 = () => SpreadsheetRange.Parse("   ");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_InvalidFormat_ThrowsFormatException()
    {
        Action act = () => SpreadsheetRange.Parse("INVALID");

        act.Should().Throw<FormatException>().WithMessage("*Invalid range format*");
    }

    [Fact]
    public void ColumnLettersToIndex_InvalidCharacters_ThrowsFormatException()
    {
        Action act = () => SpreadsheetRange.ColumnLettersToIndex("A1");

        act.Should().Throw<FormatException>().WithMessage("*Invalid column letters*");
    }

    [Fact]
    public void ColumnIndexToLetters_Negative_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => SpreadsheetRange.ColumnIndexToLetters(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
