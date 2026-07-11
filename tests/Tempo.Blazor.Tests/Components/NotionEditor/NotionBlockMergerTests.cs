using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Services;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Backspace at the start of a non-empty block merges it into its predecessor. The seam must not
/// invent whitespace, must keep the formatting of both halves, and must tell the caller where the
/// caret belongs so the caret lands exactly between the two former blocks.
/// </summary>
public sealed class NotionBlockMergerTests
{
    [Theory]
    [InlineData("alpha", "beta", "alphabeta")]
    [InlineData("", "beta", "beta")]
    [InlineData("alpha", "", "alpha")]
    [InlineData(null, "beta", "beta")]
    [InlineData("alpha", null, "alpha")]
    public void Join_ConcatenatesWithoutInventingWhitespace(string? previous, string? html, string expected) =>
        NotionBlockMerger.Join(previous, html).Should().Be(expected);

    [Fact]
    public void Join_KeepsTheFormattingOfBothHalves()
    {
        var merged = NotionBlockMerger.Join("<strong>bold</strong>", "<em>italic</em>");

        merged.Should().Be("<strong>bold</strong><em>italic</em>");
    }

    [Fact]
    public void Join_DoesNotNestTheSecondHalfInsideTheFirstElement()
    {
        // A naive implementation that appends inside the last element would produce
        // "<strong>boldplain</strong>" and silently bold the merged-in text.
        NotionBlockMerger.Join("<strong>bold</strong>", "plain")
            .Should().Be("<strong>bold</strong>plain");
    }

    [Theory]
    [InlineData("alpha", 5)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("<strong>bold</strong>", 4)]
    [InlineData("<p>a<em>b</em>c</p>", 3)]
    [InlineData("a&amp;b", 3)]
    [InlineData("a&nbsp;b", 3)]
    public void CaretOffsetForSeam_CountsThePlainTextLengthOfThePreviousBlock(string? previous, int expected) =>
        NotionBlockMerger.CaretOffsetForSeam(previous).Should().Be(expected);

    [Fact]
    public void CaretOffsetForSeam_IgnoresAttributeValuesThatLookLikeText()
    {
        // "title" must not be counted; only the rendered characters are.
        NotionBlockMerger.CaretOffsetForSeam("""<a href="https://example.com" title="long title">hi</a>""")
            .Should().Be(2);
    }
}
