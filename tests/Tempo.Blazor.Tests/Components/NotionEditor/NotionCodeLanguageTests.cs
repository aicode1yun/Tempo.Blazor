using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Services;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// The dropdown shows human names ("C#", "Plain Text"); Prism keys its grammars by lowercase ids
/// ("csharp", none). A wrong id means Prism silently highlights nothing.
/// </summary>
public sealed class NotionCodeLanguageTests
{
    [Theory]
    [InlineData("C#", "csharp")]
    [InlineData("C++", "cpp")]
    [InlineData("JavaScript", "javascript")]
    [InlineData("TypeScript", "typescript")]
    [InlineData("Shell", "bash")]
    [InlineData("Batch", "batch")]
    [InlineData("PowerShell", "powershell")]
    [InlineData("HTML", "markup")]
    [InlineData("XML", "markup")]
    [InlineData("Markdown", "markdown")]
    [InlineData("Plain Text", null)]
    public void TheDropdownNameMapsOntoAPrismGrammarId(string display, string? expected) =>
        NotionCodeLanguage.ToPrismId(display).Should().Be(expected);

    [Theory]
    [InlineData("Python", "python")]
    [InlineData("SQL", "sql")]
    [InlineData("YAML", "yaml")]
    [InlineData("JSON", "json")]
    public void SimpleNamesAreLowercased(string display, string expected) =>
        NotionCodeLanguage.ToPrismId(display).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoLanguageMeansNoHighlighting(string? language) =>
        NotionCodeLanguage.ToPrismId(language).Should().BeNull();

    [Fact]
    public void AnUnknownLanguageIsLowercasedRatherThanDropped() =>
        NotionCodeLanguage.ToPrismId("Brainfuck").Should().Be("brainfuck");

    [Fact]
    public void TheMappingIsCaseInsensitive() =>
        NotionCodeLanguage.ToPrismId("c#").Should().Be("csharp");
}
