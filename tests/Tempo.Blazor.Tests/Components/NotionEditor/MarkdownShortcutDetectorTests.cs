using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Blokové markdown zkratky. Sada vzorů musí sedět s živou JS detekcí
/// (notion-editor.js:_detectMarkdownShortcut) i s NotionMarkdownImporter (`- [ ]`).
/// Detect zůstává tolerantnější než JS: matchuje prefix a vrací zbytek řádku.
/// </summary>
public class MarkdownShortcutDetectorTests
{
    private readonly MarkdownShortcutDetector _detector = new();

    [Theory]
    [InlineData("# ", BlockType.Heading1)]
    [InlineData("## ", BlockType.Heading2)]
    [InlineData("### ", BlockType.Heading3)]
    [InlineData("* ", BlockType.BulletList)]
    [InlineData("- ", BlockType.BulletList)]
    [InlineData("> ", BlockType.Quote)]
    public void Detect_RecognisesBlockPrefixes(string input, BlockType expected)
    {
        _detector.Detect(input)!.SuggestedType.Should().Be(expected);
    }

    [Theory]
    [InlineData("1. ")]
    [InlineData("2. ")]
    [InlineData("10. ")]
    [InlineData("42. ")]
    public void Detect_RecognisesAnyNumberedListPrefix(string input)
    {
        var result = _detector.Detect(input);

        result.Should().NotBeNull();
        result!.SuggestedType.Should().Be(BlockType.NumberedList);
        result.TextAfterTrigger.Should().BeEmpty();
    }

    [Theory]
    [InlineData("[] ", false)]
    [InlineData("[ ] ", false)]
    [InlineData("[x] ", true)]
    [InlineData("[X] ", true)]
    [InlineData("- [ ] ", false)]
    [InlineData("- [x] ", true)]
    public void Detect_RecognisesTodoVariants(string input, bool expectedChecked)
    {
        var result = _detector.Detect(input);

        result.Should().NotBeNull();
        result!.SuggestedType.Should().Be(BlockType.TodoItem);
        result.IsChecked.Should().Be(expectedChecked);
    }

    [Theory]
    [InlineData("```", BlockType.Code)]
    [InlineData("---", BlockType.Divider)]
    public void Detect_RecognisesExactTriggers(string input, BlockType expected)
    {
        _detector.Detect(input)!.SuggestedType.Should().Be(expected);
    }

    [Fact]
    public void Detect_ReturnsRemainderAfterPrefix()
    {
        var result = _detector.Detect("# Nadpis");

        result!.SuggestedType.Should().Be(BlockType.Heading1);
        result.TextAfterTrigger.Should().Be("Nadpis");
    }

    [Fact]
    public void Detect_TodoPrefixTakesPrecedenceOverBulletPrefix()
    {
        var result = _detector.Detect("- [x] hotovo");

        result!.SuggestedType.Should().Be(BlockType.TodoItem);
        result.IsChecked.Should().BeTrue();
        result.TextAfterTrigger.Should().Be("hotovo");
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("#no-space")]
    [InlineData("1.no-space")]
    public void Detect_ReturnsNullForNonShortcuts(string input)
    {
        _detector.Detect(input).Should().BeNull();
    }

    [Theory]
    [InlineData("heading1", BlockType.Heading1)]
    [InlineData("bullet", BlockType.BulletList)]
    [InlineData("numbered", BlockType.NumberedList)]
    [InlineData("todoDone", BlockType.TodoItem)]
    [InlineData("divider", BlockType.Divider)]
    public void FromJsShortcutKey_MapsEveryJsKey(string key, BlockType expected)
    {
        MarkdownShortcutDetector.FromJsShortcutKey(key).Should().Be(expected);
    }

    [Fact]
    public void FromJsShortcutKey_ReturnsNullForUnknownKey()
    {
        MarkdownShortcutDetector.FromJsShortcutKey("nope").Should().BeNull();
    }

    [Theory]
    [InlineData("todo_checked", true)]
    [InlineData("todoDone", true)]
    [InlineData("todo", false)]
    public void IsCheckedTodo_MatchesJsKeys(string key, bool expected)
    {
        MarkdownShortcutDetector.IsCheckedTodo(key).Should().Be(expected);
    }
}
