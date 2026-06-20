using FluentAssertions;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetNamedRangeTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var range = new SpreadsheetNamedRange
        {
            Name = "Revenue",
            RefersTo = "A1:A10",
            Scope = NamedRangeScope.Workbook,
            Comment = "Total revenue"
        };

        range.Name.Should().Be("Revenue");
        range.RefersTo.Should().Be("A1:A10");
        range.Scope.Should().Be(NamedRangeScope.Workbook);
        range.SheetIndex.Should().BeNull();
        range.Comment.Should().Be("Total revenue");
    }

    [Theory]
    [InlineData("Sales", true)]
    [InlineData("_Total", true)]
    [InlineData("A1", false)]
    [InlineData("1Sales", false)]
    [InlineData("Sales Total", false)]
    [InlineData("Sales-Total", false)]
    [InlineData("", false)]
    public void IsValidName_ValidatesNamingRules(string name, bool expected)
    {
        SpreadsheetNamedRange.IsValidName(name).Should().Be(expected);
    }

    [Fact]
    public void IsValidName_RejectsCellReferenceCollisions()
    {
        SpreadsheetNamedRange.IsValidName("A1").Should().BeFalse();
        SpreadsheetNamedRange.IsValidName("$A$1").Should().BeFalse();
        SpreadsheetNamedRange.IsValidName("AB100").Should().BeFalse();
        SpreadsheetNamedRange.IsValidName("XFD1048576").Should().BeFalse();
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var original = new SpreadsheetNamedRange
        {
            Name = "Profit",
            RefersTo = "B1:B10",
            Scope = NamedRangeScope.Sheet,
            SheetIndex = 0,
            Comment = "Profit margin"
        };

        var clone = original.Clone();

        clone.Name.Should().Be(original.Name);
        clone.RefersTo.Should().Be(original.RefersTo);
        clone.Scope.Should().Be(original.Scope);
        clone.SheetIndex.Should().Be(original.SheetIndex);
        clone.Comment.Should().Be(original.Comment);
    }
}
