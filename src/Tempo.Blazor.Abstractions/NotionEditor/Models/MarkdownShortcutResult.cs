using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>
/// Result of <c>MarkdownShortcutDetector.Detect</c>.
/// </summary>
/// <param name="SuggestedType">Block type the current block should be converted to.</param>
/// <param name="TextAfterTrigger">Plain-text content that follows the matched prefix (may be empty).</param>
/// <param name="IsChecked">Meaningful only for <see cref="BlockType.TodoItem"/> — carries the initial checked state.</param>
public sealed record MarkdownShortcutResult(
    BlockType SuggestedType,
    string    TextAfterTrigger,
    bool      IsChecked = false);
