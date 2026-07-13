using System.Text;
using FluentAssertions;
using Tempo.Blazor.Components.ImportExport;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Tests.Components.ImportExport;

/// <summary>Pure unit tests for the dependency-free RFC-4180-ish <see cref="CsvImportFileParser"/>.</summary>
public class CsvImportFileParserTests
{
    private static async Task<ImportParseResult> ParseAsync(string csv, ImportParseOptions? options = null)
    {
        var parser = new CsvImportFileParser();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await parser.ParseAsync(stream, options ?? new ImportParseOptions());
    }

    // ── Regression (K10 code review): blank-line phantom row, lone quote, static-Parse BOM ──

    [Fact]
    public async Task DoubleTrailingNewline_DoesNotProducePhantomRow()
    {
        var result = await ParseAsync("A,B\r\n1,2\r\n\r\n");

        result.Rows.Should().HaveCount(1);
        result.Rows[0].Should().Equal("1", "2");
    }

    [Fact]
    public async Task BlankLineBetweenRows_IsSkipped()
    {
        var result = await ParseAsync("A,B\r\n1,2\r\n\r\n3,4\r\n");

        result.Rows.Should().HaveCount(2);
        result.Rows[0].Should().Equal("1", "2");
        result.Rows[1].Should().Equal("3", "4");
    }

    [Fact]
    public async Task LoneEmptyQuotedField_IsOneEmptyCell()
    {
        var result = await ParseAsync("\"\"", new ImportParseOptions(HasHeaderRow: false));

        result.Rows.Should().HaveCount(1);
        result.Rows[0].Should().Equal("");
    }

    [Fact]
    public void StaticParse_StripsLeadingBom()
    {
        var result = CsvImportFileParser.Parse("﻿Name,Email\r\nAlice,a@x.com", new ImportParseOptions());

        result.Columns[0].Name.Should().Be("Name");
        result.Rows.Should().HaveCount(1);
        result.Rows[0].Should().Equal("Alice", "a@x.com");
    }

    [Fact]
    public async Task Parses_Header_And_Rows()
    {
        var result = await ParseAsync("Name,Email\nAlice,alice@x.com\nBob,bob@x.com");

        result.Columns.Select(c => c.Name).Should().Equal("Name", "Email");
        result.Columns.Select(c => c.Index).Should().Equal(0, 1);
        result.Rows.Should().HaveCount(2);
        result.Rows[0].Should().Equal("Alice", "alice@x.com");
        result.Rows[1].Should().Equal("Bob", "bob@x.com");
    }

    [Fact]
    public async Task Handles_Quoted_Field_With_Embedded_Delimiter()
    {
        var result = await ParseAsync("Name,Note\nAlice,\"a, b, c\"");

        result.Rows.Should().HaveCount(1);
        result.Rows[0].Should().Equal("Alice", "a, b, c");
    }

    [Fact]
    public async Task Handles_Escaped_Quotes_Inside_Quoted_Field()
    {
        var result = await ParseAsync("Name,Note\nAlice,\"She said \"\"hi\"\"\"");

        result.Rows[0].Should().Equal("Alice", "She said \"hi\"");
    }

    [Fact]
    public async Task Handles_Embedded_Newline_Inside_Quoted_Field()
    {
        var result = await ParseAsync("Name,Note\nAlice,\"line1\nline2\"\nBob,plain");

        result.Rows.Should().HaveCount(2);
        result.Rows[0].Should().Equal("Alice", "line1\nline2");
        result.Rows[1].Should().Equal("Bob", "plain");
    }

    [Fact]
    public async Task Handles_Crlf_Line_Endings()
    {
        var result = await ParseAsync("Name,Email\r\nAlice,a@x.com\r\nBob,b@x.com\r\n");

        result.Columns.Select(c => c.Name).Should().Equal("Name", "Email");
        result.Rows.Should().HaveCount(2);
        result.Rows[1].Should().Equal("Bob", "b@x.com");
    }

    [Fact]
    public async Task Trailing_Newline_Does_Not_Produce_Empty_Row()
    {
        var result = await ParseAsync("Name,Email\nAlice,a@x.com\n");

        result.Rows.Should().HaveCount(1);
        result.Rows[0].Should().Equal("Alice", "a@x.com");
    }

    [Fact]
    public async Task No_Header_Generates_Column_Names_And_Keeps_First_Row_As_Data()
    {
        var result = await ParseAsync("Alice,a@x.com\nBob,b@x.com", new ImportParseOptions(HasHeaderRow: false));

        result.Columns.Select(c => c.Name).Should().Equal("Column 1", "Column 2");
        result.Rows.Should().HaveCount(2);
        result.Rows[0].Should().Equal("Alice", "a@x.com");
    }

    [Fact]
    public async Task Ragged_Short_Row_Is_Padded_To_Column_Count()
    {
        var result = await ParseAsync("A,B,C\n1,2,3\n4,5");

        result.Columns.Should().HaveCount(3);
        result.Rows[1].Should().Equal("4", "5", "");
    }

    [Fact]
    public async Task Ragged_Wide_Row_Extends_Detected_Columns()
    {
        var result = await ParseAsync("A,B\n1,2,3");

        result.Columns.Select(c => c.Name).Should().Equal("A", "B", "Column 3");
        result.Rows[0].Should().Equal("1", "2", "3");
    }

    [Fact]
    public async Task Respects_Custom_Delimiter()
    {
        var result = await ParseAsync("Name;Email\nAlice;a@x.com", new ImportParseOptions(Delimiter: ';'));

        result.Columns.Select(c => c.Name).Should().Equal("Name", "Email");
        result.Rows[0].Should().Equal("Alice", "a@x.com");
    }

    [Fact]
    public async Task Strips_Utf8_Bom()
    {
        var parser = new CsvImportFileParser();
        var payload = Encoding.UTF8.GetBytes("Name,Email\nAlice,a@x.com");
        using var stream = new MemoryStream([.. Encoding.UTF8.GetPreamble(), .. payload]);

        var result = await parser.ParseAsync(stream, new ImportParseOptions());

        result.Columns[0].Name.Should().Be("Name");
    }

    [Fact]
    public async Task Empty_Input_Yields_No_Columns_And_No_Rows()
    {
        var result = await ParseAsync(string.Empty);

        result.Columns.Should().BeEmpty();
        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_Field_Between_Delimiters_Is_Preserved()
    {
        var result = await ParseAsync("A,B,C\n1,,3");

        result.Rows[0].Should().Equal("1", "", "3");
    }
}
