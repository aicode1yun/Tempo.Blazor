using Tempo.Blazor.Components.Spreadsheet.Data;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetAggregationTests
{
    [Fact]
    public void Compute_NumericValues_ReturnsAllAggregates()
    {
        var result = SpreadsheetAggregation.Compute(new object?[] { 1.0, 2.0, 3.0, 4.0 });

        result.Count.Should().Be(4);
        result.CountNumbers.Should().Be(4);
        result.Sum.Should().Be(10.0);
        result.Average.Should().Be(2.5);
        result.Min.Should().Be(1.0);
        result.Max.Should().Be(4.0);
        result.HasNumbers.Should().BeTrue();
    }

    [Fact]
    public void Compute_IgnoresTextAndEmptyForNumericAggregates()
    {
        var result = SpreadsheetAggregation.Compute(new object?[] { 10.0, "hello", null, "", 20.0 });

        // Count = non-empty cells (10, "hello", 20) -> 3
        result.Count.Should().Be(3);
        // CountNumbers = only numbers (10, 20) -> 2
        result.CountNumbers.Should().Be(2);
        result.Sum.Should().Be(30.0);
        result.Average.Should().Be(15.0);
        result.Min.Should().Be(10.0);
        result.Max.Should().Be(20.0);
    }

    [Fact]
    public void Compute_AllText_HidesNumericAggregates()
    {
        var result = SpreadsheetAggregation.Compute(new object?[] { "a", "b", "c" });

        result.Count.Should().Be(3);
        result.CountNumbers.Should().Be(0);
        result.HasNumbers.Should().BeFalse();
        result.Sum.Should().BeNull();
        result.Average.Should().BeNull();
        result.Min.Should().BeNull();
        result.Max.Should().BeNull();
    }

    [Fact]
    public void Compute_AllEmpty_ReturnsZeroCounts()
    {
        var result = SpreadsheetAggregation.Compute(new object?[] { null, "", null });

        result.Count.Should().Be(0);
        result.CountNumbers.Should().Be(0);
        result.HasNumbers.Should().BeFalse();
        result.Sum.Should().BeNull();
    }

    [Fact]
    public void Compute_SingleNumber_ReturnsThatValue()
    {
        var result = SpreadsheetAggregation.Compute(new object?[] { 42.0 });

        result.Count.Should().Be(1);
        result.CountNumbers.Should().Be(1);
        result.Sum.Should().Be(42.0);
        result.Average.Should().Be(42.0);
        result.Min.Should().Be(42.0);
        result.Max.Should().Be(42.0);
    }

    [Fact]
    public void Compute_NumericStringsAreNotCountedAsNumbers()
    {
        var result = SpreadsheetAggregation.Compute(new object?[] { "5", "6" });

        result.Count.Should().Be(2);
        result.CountNumbers.Should().Be(0);
        result.HasNumbers.Should().BeFalse();
    }

    [Fact]
    public void Compute_BooleansAreNotNumbers()
    {
        var result = SpreadsheetAggregation.Compute(new object?[] { true, false, 1.0 });

        result.Count.Should().Be(3);
        result.CountNumbers.Should().Be(1);
        result.Sum.Should().Be(1.0);
    }

    [Fact]
    public void Compute_NegativeAndMixedMagnitudes()
    {
        var result = SpreadsheetAggregation.Compute(new object?[] { -5.0, 0.0, 5.0, 10.0 });

        result.Sum.Should().Be(10.0);
        result.Min.Should().Be(-5.0);
        result.Max.Should().Be(10.0);
        result.Average.Should().Be(2.5);
    }

    [Fact]
    public void Compute_IntegerBoxedValues_AreNumbers()
    {
        var result = SpreadsheetAggregation.Compute(new object?[] { 1, 2L, (short)3, (byte)4 });

        result.CountNumbers.Should().Be(4);
        result.Sum.Should().Be(10.0);
    }

    [Fact]
    public void Compute_EmptySequence_ReturnsZero()
    {
        var result = SpreadsheetAggregation.Compute(Array.Empty<object?>());

        result.Count.Should().Be(0);
        result.CountNumbers.Should().Be(0);
        result.HasNumbers.Should().BeFalse();
    }
}
