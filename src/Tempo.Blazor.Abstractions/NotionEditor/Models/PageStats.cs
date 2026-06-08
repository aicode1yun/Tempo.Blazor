using System.Net;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Immutable reading statistics for a Notion page.</summary>
public sealed record PageStats(int WordCount, int ReadingTimeMinutes)
{
    private const int DefaultWordsPerMinute = 200;
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EntityGapRegex = new("&nbsp;|&#160;", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex WordRegex = new(@"[\p{L}\p{N}]+(?:[’'-][\p{L}\p{N}]+)*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Returns zero page statistics.</summary>
    public static PageStats Empty { get; } = new(0, 0);

    /// <summary>Calculates page statistics from HTML or plain-text fragments.</summary>
    public static PageStats Calculate(IEnumerable<string?> fragments, int wordsPerMinute = DefaultWordsPerMinute)
    {
        ArgumentNullException.ThrowIfNull(fragments);
        if (wordsPerMinute <= 0)
            throw new ArgumentOutOfRangeException(nameof(wordsPerMinute), wordsPerMinute, "Words per minute must be greater than zero.");

        var wordCount = fragments.Sum(CountWords);
        var readingTime = wordCount == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(wordCount / (double)wordsPerMinute));

        return new PageStats(wordCount, readingTime);
    }

    /// <summary>Counts words in a single HTML or plain-text fragment.</summary>
    public static int CountWords(string? htmlOrText)
    {
        if (string.IsNullOrWhiteSpace(htmlOrText))
            return 0;

        var normalized = EntityGapRegex.Replace(htmlOrText, " ");
        var withoutTags = TagRegex.Replace(normalized, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);

        return string.IsNullOrWhiteSpace(decoded) ? 0 : WordRegex.Count(decoded);
    }
}
