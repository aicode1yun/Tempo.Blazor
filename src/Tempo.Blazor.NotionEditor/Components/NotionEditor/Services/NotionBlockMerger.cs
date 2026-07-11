namespace Tempo.Blazor.Components.NotionEditor.Services;

using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Joins two blocks when the user presses Backspace at the start of the second one.
/// </summary>
internal static partial class NotionBlockMerger
{
    /// <summary>
    /// Concatenates the two HTML fragments at the seam. Both halves keep their own markup: the
    /// second fragment is appended after the first one's last element, never inside it, so text
    /// merged into a block that ends in <c>&lt;strong&gt;</c> does not become bold.
    /// </summary>
    public static string Join(string? previousHtml, string? html) =>
        (previousHtml ?? string.Empty) + (html ?? string.Empty);

    /// <summary>
    /// Plain-text length of <paramref name="previousHtml"/> — the caret offset of the seam inside
    /// the merged block, counted the same way the browser counts characters in text nodes.
    /// </summary>
    public static int CaretOffsetForSeam(string? previousHtml)
    {
        if (string.IsNullOrEmpty(previousHtml)) return 0;

        var text = TagRegex().Replace(previousHtml, string.Empty);
        return WebUtility.HtmlDecode(text).Length;
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagRegex();
}
