using FluentAssertions;
using Tempo.Blazor.Helpers;
using Xunit;

namespace Tempo.Blazor.Tests.Helpers;

/// <summary>TDD tests for the FormD accent-insensitive matching helper.</summary>
public class AccentInsensitiveTextTests
{
    [Theory]
    [InlineData("Ústí nad Labem", "usti", true)]
    [InlineData("Ústí nad Labem", "Ústí", true)]
    [InlineData("Ústí nad Labem", "ÚSTÍ", true)]
    [InlineData("Praha", "práha", true)]
    [InlineData("Český Krumlov", "cesky", true)]
    [InlineData("žluťoučký kůň", "zlutoucky kun", true)]
    [InlineData("Brno", "usti", false)]
    [InlineData("Ústí nad Labem", "labem", true)]
    [InlineData("Ústí nad Labem", "xyz", false)]
    public void Contains_Matches_Ignoring_Case_And_Diacritics(string source, string term, bool expected)
        => AccentInsensitiveText.Contains(source, term).Should().Be(expected);

    [Theory]
    [InlineData("", "usti", false)]
    [InlineData("Ústí", "", true)]
    [InlineData("", "", true)]
    public void Contains_Handles_Empty_Inputs(string source, string term, bool expected)
        => AccentInsensitiveText.Contains(source, term).Should().Be(expected);

    [Theory]
    [InlineData("Ústí", "Usti")]
    [InlineData("žluťoučký", "zlutoucky")]
    [InlineData("ĚŠČŘŽÝÁÍÉŮÚĎŤŇ", "ESCRZYAIEUUDTN")]
    [InlineData("no accents", "no accents")]
    public void RemoveDiacritics_Strips_Combining_Marks(string input, string expected)
        => AccentInsensitiveText.RemoveDiacritics(input).Should().Be(expected);
}
