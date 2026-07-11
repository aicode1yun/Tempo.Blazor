using System.Text.RegularExpressions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Pure C# service that recognises Notion-style markdown shortcut patterns
/// typed at the beginning of a block line.
///
/// Invoke <see cref="Detect"/> after the user presses Space or Enter.
/// The <paramref name="lineText"/> argument must be the full plain-text content
/// of the current block (including the trigger character that was just appended).
///
/// Pattern table (most-specific first to avoid prefix collisions):
/// <list type="table">
///   <listheader><term>Pattern</term><description>Block type</description></listheader>
///   <item><term>### (Space)</term><description>Heading3</description></item>
///   <item><term>## (Space)</term><description>Heading2</description></item>
///   <item><term># (Space)</term><description>Heading1</description></item>
///   <item><term>- [x] (Space) or [x] (Space)</term><description>TodoItem (checked)</description></item>
///   <item><term>- [ ] (Space), [ ] (Space) or [] (Space)</term><description>TodoItem (unchecked)</description></item>
///   <item><term>* (Space) or - (Space)</term><description>BulletList</description></item>
///   <item><term>any digits followed by ". " (e.g. 1. , 12. )</term><description>NumberedList</description></item>
///   <item><term>&gt; (Space)</term><description>Quote</description></item>
///   <item><term>``` (Enter or standalone)</term><description>Code</description></item>
///   <item><term>--- (Enter or standalone)</term><description>Divider</description></item>
/// </list>
/// </summary>
public sealed partial class MarkdownShortcutDetector
{
    // Ordered from most-specific (longest prefix) to least-specific to avoid
    // false positives between overlapping prefixes such as "# " vs "## ",
    // or the task-list "- [x] " vs the bullet "- ".
    private static readonly (string Prefix, BlockType Type, bool IsChecked)[] _rules =
    [
        ("### ",   BlockType.Heading3,   false),
        ("## ",    BlockType.Heading2,   false),
        ("# ",     BlockType.Heading1,   false),
        ("- [x] ", BlockType.TodoItem,   true),
        ("- [X] ", BlockType.TodoItem,   true),
        ("- [ ] ", BlockType.TodoItem,   false),
        ("- [] ",  BlockType.TodoItem,   false),
        ("[x] ",   BlockType.TodoItem,   true),
        ("[X] ",   BlockType.TodoItem,   true),
        ("[ ] ",   BlockType.TodoItem,   false),
        ("[] ",    BlockType.TodoItem,   false),
        ("* ",     BlockType.BulletList, false),
        ("- ",     BlockType.BulletList, false),
        ("> ",     BlockType.Quote,      false),
    ];

    // Exact-match shortcuts triggered by Enter (the text IS the trigger itself, no trailing text).
    private static readonly (string Exact, BlockType Type)[] _exactRules =
    [
        ("```", BlockType.Code),
        ("---", BlockType.Divider),
    ];

    /// <summary>
    /// Analyses <paramref name="lineText"/> and returns the conversion suggestion,
    /// or <c>null</c> when no known shortcut is recognised.
    /// </summary>
    /// <param name="lineText">
    /// Full plain-text content of the block line <em>including</em> the triggering
    /// Space or Enter character that was just appended by the user.
    /// </param>
    public MarkdownShortcutResult? Detect(string lineText)
    {
        if (string.IsNullOrEmpty(lineText))
            return null;

        // Prefix rules (triggered by trailing Space).
        foreach (var (prefix, type, isChecked) in _rules)
        {
            if (lineText.StartsWith(prefix, StringComparison.Ordinal))
            {
                var remainder = lineText[prefix.Length..];
                return new MarkdownShortcutResult(type, remainder, isChecked);
            }
        }

        // Ordered list accepts any leading number, not just "1." — matches the importer.
        var numbered = NumberedPrefixRegex().Match(lineText);
        if (numbered.Success)
        {
            return new MarkdownShortcutResult(
                BlockType.NumberedList,
                lineText[numbered.Length..]);
        }

        // Exact rules (triggered by Enter — the text equals the shortcut token exactly).
        var trimmed = lineText.TrimEnd('\n', '\r');
        foreach (var (exact, type) in _exactRules)
        {
            if (string.Equals(trimmed, exact, StringComparison.Ordinal))
                return new MarkdownShortcutResult(type, string.Empty);
        }

        return null;
    }

    /// <summary>
    /// Converts the shortcut string emitted by <c>notion-editor.js</c>
    /// (e.g. <c>"heading1"</c>, <c>"bullet"</c>) into the corresponding
    /// <see cref="BlockType"/>, or <c>null</c> when the string is unrecognised.
    /// This bridges the existing JS-side detection with the C# type system.
    /// </summary>
    public static BlockType? FromJsShortcutKey(string shortcutKey) => shortcutKey switch
    {
        "heading1"      => BlockType.Heading1,
        "heading2"      => BlockType.Heading2,
        "heading3"      => BlockType.Heading3,
        "bullet"        => BlockType.BulletList,
        "numbered"      => BlockType.NumberedList,
        "todo"          => BlockType.TodoItem,
        "todo_checked"  => BlockType.TodoItem,
        "todoDone"      => BlockType.TodoItem,
        "quote"         => BlockType.Quote,
        "code"          => BlockType.Code,
        "divider"       => BlockType.Divider,
        _               => null
    };

    /// <summary>
    /// Returns <c>true</c> if <paramref name="jsShortcutKey"/> represents a checked todo
    /// shortcut (<c>"todo_checked"</c> / <c>"[x] "</c> pattern).
    /// </summary>
    public static bool IsCheckedTodo(string jsShortcutKey) =>
        string.Equals(jsShortcutKey, "todo_checked", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(jsShortcutKey, "todoDone", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^\d+\. ")]
    private static partial Regex NumberedPrefixRegex();
}
