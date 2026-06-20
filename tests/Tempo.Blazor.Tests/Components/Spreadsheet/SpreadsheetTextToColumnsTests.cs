using Tempo.Blazor.Components.Spreadsheet.Data;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetTextToColumnsTests
{
    [Fact]
    public void Delimited_Comma_SplitsFields()
    {
        var options = new SpreadsheetSeparatorOptions { Comma = true };
        var result = SpreadsheetTextToColumns.Split(["Jan,Novak,Praha"], options);

        result.Should().HaveCount(1);
        result[0].Should().Equal("Jan", "Novak", "Praha");
    }

    [Fact]
    public void Delimited_Semicolon_SplitsFields()
    {
        var options = new SpreadsheetSeparatorOptions { Semicolon = true };
        var result = SpreadsheetTextToColumns.Split(["Jan;Novak;Praha"], options);

        result[0].Should().Equal("Jan", "Novak", "Praha");
    }

    [Fact]
    public void Delimited_Tab_SplitsFields()
    {
        var options = new SpreadsheetSeparatorOptions { Tab = true };
        var result = SpreadsheetTextToColumns.Split(["a\tb\tc"], options);

        result[0].Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Delimited_TextQualifier_ProtectsDelimiterInsideQuotes()
    {
        var options = new SpreadsheetSeparatorOptions { Comma = true, TextQualifier = '"' };
        var result = SpreadsheetTextToColumns.Split(["\"Novak, Jan\",Praha"], options);

        result[0].Should().Equal("Novak, Jan", "Praha");
    }

    [Fact]
    public void Delimited_DoubledQualifier_IsLiteralQuote()
    {
        var options = new SpreadsheetSeparatorOptions { Comma = true, TextQualifier = '"' };
        var result = SpreadsheetTextToColumns.Split(["\"say \"\"hi\"\"\",b"], options);

        result[0].Should().Equal("say \"hi\"", "b");
    }

    [Fact]
    public void Delimited_ConsecutiveDelimiters_ProduceEmptyFieldsByDefault()
    {
        var options = new SpreadsheetSeparatorOptions { Comma = true };
        var result = SpreadsheetTextToColumns.Split(["a,,b"], options);

        result[0].Should().Equal("a", "", "b");
    }

    [Fact]
    public void Delimited_TreatConsecutiveAsOne_CollapsesEmptyFields()
    {
        var options = new SpreadsheetSeparatorOptions { Comma = true, TreatConsecutiveAsOne = true };
        var result = SpreadsheetTextToColumns.Split(["a,,b"], options);

        result[0].Should().Equal("a", "b");
    }

    [Fact]
    public void Delimited_MultipleDelimiters_SplitOnAny()
    {
        var options = new SpreadsheetSeparatorOptions { Comma = true, Space = true };
        var result = SpreadsheetTextToColumns.Split(["a, b c"], options);

        result[0].Should().Equal("a", "", "b", "c");
    }

    [Fact]
    public void Delimited_OtherDelimiter_IsUsed()
    {
        var options = new SpreadsheetSeparatorOptions { OtherDelimiter = "|" };
        var result = SpreadsheetTextToColumns.Split(["a|b|c"], options);

        result[0].Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Delimited_NoDelimiterSelected_ReturnsWholeRow()
    {
        var options = new SpreadsheetSeparatorOptions();
        var result = SpreadsheetTextToColumns.Split(["a,b,c"], options);

        result[0].Should().Equal("a,b,c");
    }

    [Fact]
    public void FixedWidth_SlicesAtBreaks()
    {
        var options = new SpreadsheetSeparatorOptions
        {
            Mode = SpreadsheetTextToColumnsMode.FixedWidth,
            FixedWidthBreaks = [3, 6]
        };
        var result = SpreadsheetTextToColumns.Split(["abcdefghi"], options);

        result[0].Should().Equal("abc", "def", "ghi");
    }

    [Fact]
    public void FixedWidth_TrimsWhitespaceInEachField()
    {
        var options = new SpreadsheetSeparatorOptions
        {
            Mode = SpreadsheetTextToColumnsMode.FixedWidth,
            FixedWidthBreaks = [5]
        };
        var result = SpreadsheetTextToColumns.Split(["Jan  Novak"], options);

        result[0].Should().Equal("Jan", "Novak");
    }

    [Fact]
    public void FixedWidth_NoBreaks_ReturnsTrimmedRow()
    {
        var options = new SpreadsheetSeparatorOptions { Mode = SpreadsheetTextToColumnsMode.FixedWidth };
        var result = SpreadsheetTextToColumns.Split(["  hello  "], options);

        result[0].Should().Equal("hello");
    }

    [Fact]
    public void MultipleRows_AreEachSplit()
    {
        var options = new SpreadsheetSeparatorOptions { Comma = true };
        var result = SpreadsheetTextToColumns.Split(["a,b", "c,d,e"], options);

        result.Should().HaveCount(2);
        result[0].Should().Equal("a", "b");
        result[1].Should().Equal("c", "d", "e");
    }
}
