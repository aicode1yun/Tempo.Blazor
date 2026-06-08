using FluentAssertions;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class PageStatsTests
{
    [Fact]
    public void Calculate_ReturnsZeroForEmptyPage()
    {
        var stats = PageStats.Calculate([null, "", "<p><br></p>"]);

        stats.WordCount.Should().Be(0);
        stats.ReadingTimeMinutes.Should().Be(0);
    }

    [Fact]
    public void Calculate_StripsHtmlAndCountsDecodedWords()
    {
        var stats = PageStats.Calculate(["<h1>Design &amp; delivery</h1><p>Alpha&nbsp;beta gamma.</p>"]);

        stats.WordCount.Should().Be(5);
        stats.ReadingTimeMinutes.Should().Be(1);
    }

    [Fact]
    public void Calculate_CeilsLongTextReadingTime()
    {
        var longText = string.Join(' ', Enumerable.Range(1, 401).Select(i => $"word{i}"));

        var stats = PageStats.Calculate([longText]);

        stats.WordCount.Should().Be(401);
        stats.ReadingTimeMinutes.Should().Be(3);
    }
}
